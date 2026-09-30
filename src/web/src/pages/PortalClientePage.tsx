import { useState } from 'react'
import { Button, Modal, Table, Tabs, Timeline, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { Indicadores } from '../components/Indicadores'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { EtiquetaEstado, type TonoEstado } from '../components/EtiquetaEstado'
import { PanelAprobaciones } from '../components/PanelAprobaciones'
import { ModalFotosOrden } from '../components/ModalFotosOrden'
import { ModalFormatoAtencion } from '../components/ModalFormatoAtencion'
import { useVehiculos } from '../api/vehiculos'
import {
  PRESUPUESTO,
  fechaIngresoOrden,
  nombresEstado,
  nombresPresupuesto,
  useOrden,
  useOrdenes,
} from '../api/ordenes'
import {
  useHistorialServicioUnidad,
  usePortalComprobantes,
  usePortalResumen,
  type AtencionServicioUnidad,
  type PortalComprobante,
} from '../api/portal'
import { nombresTipoItem, type DetalleServicioResponse, type OrdenServicioResponse, type VehiculoResponse } from '../api/tipos'
import { entero, fechaHora, importe, referenciaOrden, soles } from '../utils/formato'
import { identificadorUnidad, lecturaIngresoOrden, lecturaMedidor, nombreTipoUnidad } from '../utils/unidades'

// Lo que espera respuesta del cliente llama la atención.
const tonoPresupuesto: Record<number, TonoEstado> = {
  [PRESUPUESTO.pendiente]: 'alerta',
  [PRESUPUESTO.aprobado]: 'hecho',
  [PRESUPUESTO.rechazado]: 'suave',
}

const porFecha = (a: { fechaCambio: string }, b: { fechaCambio: string }) =>
  new Date(a.fechaCambio).getTime() - new Date(b.fechaCambio).getTime()

const fila = (etiqueta: string, valor: string | number | null | undefined) =>
  valor === null || valor === undefined || valor === '' ? null : (
    <tr key={etiqueta}>
      <td>{etiqueta}</td>
      <td>{valor}</td>
    </tr>
  )

const columnasPresupuesto: TableProps<DetalleServicioResponse>['columns'] = [
  {
    title: 'Concepto',
    key: 'concepto',
    render: (_, detalle) => (
      <>
        <div>{detalle.descripcion}</div>
        <div className="texto-secundario">
          {nombresTipoItem[detalle.tipoItem ?? (detalle.esRepuesto ? 0 : 2)] ?? detalle.tipoItemNombre}
        </div>
      </>
    ),
  },
  { title: 'Cant.', dataIndex: 'cantidad', align: 'right', className: 'num' },
  { title: 'P. unit.', dataIndex: 'precioUnitario', align: 'right', className: 'num', render: (valor: number) => importe(valor) },
  {
    title: 'Total',
    key: 'total',
    align: 'right',
    className: 'num',
    render: (_, detalle) => importe(detalle.total ?? detalle.subtotal),
  },
]

/** Lo que un cliente hizo en el taller en una unidad: cada atención con sus trabajos. */
function HistorialDeUnidad({ atenciones }: Readonly<{ atenciones: AtencionServicioUnidad[] }>) {
  if (atenciones.length === 0) {
    return <p className="texto-secundario">Esta unidad todavía no tiene servicios en el taller.</p>
  }

  return (
    <>
      {atenciones.map((atencion) => (
        <section key={atencion.ordenServicioId} className="bloque-modal">
          <div className="seccion-titulo" style={{ marginBottom: 8 }}>
            <h3 style={{ margin: 0 }}>
              {atencion.numeroOrden ?? 'Atención'} · {fechaHora(atencion.fechaIngreso)}
            </h3>
            <div className="acciones">
              <EstadoOrdenApiTag estadoId={atencion.estadoId} />
              <strong className="num">{soles(atencion.total)}</strong>
            </div>
          </div>
          <table className="tabla-simple">
            <tbody>
              {fila('Falla reportada', atencion.motivoFalla)}
              {fila('Solución', atencion.solucion)}
              {fila(
                'Medidor al ingresar',
                atencion.lecturaMedidorIngreso == null
                  ? null
                  : `${entero(atencion.lecturaMedidorIngreso)} ${atencion.tipoMedidor === 'Horas' ? 'h' : 'km'}`,
              )}
              {fila('Salida', atencion.fechaSalida ? fechaHora(atencion.fechaSalida) : null)}
            </tbody>
          </table>
          {atencion.items.length > 0 && (
            <Table
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={atencion.items}
              style={{ marginTop: 12 }}
              columns={[
                { title: 'Trabajo o repuesto', dataIndex: 'descripcion' },
                { title: 'Cant.', dataIndex: 'cantidad', align: 'right', className: 'num' },
                {
                  title: 'Total',
                  dataIndex: 'total',
                  align: 'right',
                  className: 'num',
                  render: (valor: number) => importe(valor),
                },
              ]}
            />
          )}
        </section>
      ))}
    </>
  )
}

/**
 * Portal del cliente en la web: sus órdenes, sus unidades y sus comprobantes.
 * El backend filtra todo al cliente de la sesión.
 */
export function PortalClientePage() {
  const [pestana, setPestana] = useState('ordenes')
  const [unidadHistorial, setUnidadHistorial] = useState<VehiculoResponse | null>(null)
  const [ordenVista, setOrdenVista] = useState<string | null>(null)
  const [modalFotos, setModalFotos] = useState(false)
  const [modalFormato, setModalFormato] = useState(false)

  const resumen = usePortalResumen()
  const vehiculos = useVehiculos()
  const ordenes = useOrdenes()
  const comprobantes = usePortalComprobantes()
  const detalle = useOrden(ordenVista ?? undefined)
  const historial = useHistorialServicioUnidad(unidadHistorial?.id ?? null)

  const datosResumen = resumen.data
  const orden = detalle.data
  const primerNombre = datosResumen?.clienteNombre.split(' ')[0]

  const columnasOrdenes: TableProps<OrdenServicioResponse>['columns'] = [
    { title: 'Orden', key: 'orden', className: 'num', render: (_, fila) => <strong>{referenciaOrden(fila)}</strong> },
    {
      title: 'Unidad',
      key: 'unidad',
      render: (_, fila) => `${fila.vehiculoMarca} ${fila.vehiculoModelo}${fila.vehiculoPlaca ? ` · ${fila.vehiculoPlaca}` : ''}`,
    },
    { title: 'Ingreso', key: 'ingreso', className: 'num', render: (_, fila) => fechaHora(fechaIngresoOrden(fila)) },
    { title: 'Entrega estimada', key: 'entrega', className: 'num', render: (_, fila) => fechaHora(fila.fechaEstimadaEntrega) },
    { title: 'Estado', key: 'estado', render: (_, fila) => <EstadoOrdenApiTag estadoId={fila.estadoId} /> },
    {
      title: 'Presupuesto',
      key: 'presupuesto',
      render: (_, fila) => {
        const estado = fila.estadoPresupuestoClienteId ?? PRESUPUESTO.pendiente
        return <EtiquetaEstado tono={tonoPresupuesto[estado]}>{nombresPresupuesto[estado]}</EtiquetaEstado>
      },
    },
    { title: 'Total', key: 'total', align: 'right', className: 'num', render: (_, fila) => soles(fila.total ?? 0) },
    {
      title: '',
      key: 'ver',
      align: 'right',
      render: (_, fila) => (
        <Button type="link" onClick={() => setOrdenVista(fila.id)}>
          Ver
        </Button>
      ),
    },
  ]

  const columnasUnidades: TableProps<VehiculoResponse>['columns'] = [
    {
      title: 'Unidad',
      key: 'unidad',
      render: (_, unidad) => (
        <>
          <strong>
            {unidad.marca} {unidad.modelo}
          </strong>
          <div className="texto-secundario">
            {[nombreTipoUnidad(unidad), unidad.anio, unidad.color].filter(Boolean).join(' · ')}
          </div>
        </>
      ),
    },
    { title: 'Placa o serie', key: 'identificador', className: 'num', render: (_, unidad) => identificadorUnidad(unidad) },
    { title: 'Medidor', key: 'lectura', className: 'num', render: (_, unidad) => lecturaMedidor(unidad) },
    {
      title: '',
      key: 'historial',
      align: 'right',
      render: (_, unidad) => (
        <Button type="link" onClick={() => setUnidadHistorial(unidad)}>
          Historial
        </Button>
      ),
    },
  ]

  const columnasComprobantes: TableProps<PortalComprobante>['columns'] = [
    {
      title: 'Comprobante',
      key: 'comprobante',
      render: (_, comprobante) => (
        <strong>
          {comprobante.tipo} {[comprobante.serie, comprobante.numero].filter(Boolean).join('-')}
        </strong>
      ),
    },
    { title: 'Fecha', dataIndex: 'fecha', className: 'num', render: (fecha: string) => fechaHora(fecha) },
    { title: 'Orden', dataIndex: 'numeroOrden', render: (numero: string | null) => numero ?? '—' },
    { title: 'Pago', dataIndex: 'metodoPagoPrincipal', render: (metodo: string | null) => metodo ?? '—' },
    { title: 'Total', dataIndex: 'total', align: 'right', className: 'num', render: (total: number) => soles(total) },
    {
      title: '',
      key: 'estado',
      align: 'right',
      render: (_, comprobante) => (
        <EtiquetaEstado tono={comprobante.estado === 'Anulado' ? 'apagado' : 'neutro'}>{comprobante.estado}</EtiquetaEstado>
      ),
    },
  ]

  return (
    <>
      <BarraSuperior antetitulo="Mi portal" titulo={primerNombre ? `Hola, ${primerNombre}` : 'Mi portal'} />

      <div className="pagina">
        <AvisoError error={resumen.error} />
        {datosResumen && (
          <Indicadores
            items={[
              { etiqueta: 'Unidades', valor: entero(datosResumen.cantidadUnidades) },
              { etiqueta: 'En taller', valor: entero(datosResumen.cantidadOrdenesActivas) },
              {
                etiqueta: 'Por responder',
                valor: entero(datosResumen.cantidadPresupuestosPendientes),
                destacado: datosResumen.cantidadPresupuestosPendientes > 0,
              },
              {
                etiqueta: 'Saldo',
                valor: soles(datosResumen.saldoPendienteTotal),
                compacto: true,
                destacado: datosResumen.saldoPendienteTotal > 0,
              },
            ]}
          />
        )}

        <Tabs
          activeKey={pestana}
          onChange={setPestana}
          items={[
            {
              key: 'ordenes',
              label: 'Órdenes',
              children: (
                <>
                  <AvisoError error={ordenes.error} />
                  <Table
                    rowKey="id"
                    columns={columnasOrdenes}
                    dataSource={ordenes.data ?? []}
                    pagination={false}
                    loading={ordenes.isPending}
                    locale={{ emptyText: 'Todavía no tienes órdenes de servicio' }}
                  />
                </>
              ),
            },
            {
              key: 'unidades',
              label: 'Unidades',
              children: (
                <>
                  <AvisoError error={vehiculos.error} />
                  <Table
                    rowKey="id"
                    columns={columnasUnidades}
                    dataSource={vehiculos.data ?? []}
                    pagination={false}
                    loading={vehiculos.isPending}
                    locale={{ emptyText: 'Todavía no hay unidades a tu nombre' }}
                  />
                </>
              ),
            },
            {
              key: 'comprobantes',
              label: 'Comprobantes',
              children: (
                <>
                  <AvisoError error={comprobantes.error} />
                  <Table
                    rowKey="id"
                    columns={columnasComprobantes}
                    dataSource={comprobantes.data ?? []}
                    pagination={false}
                    loading={comprobantes.isPending}
                    locale={{ emptyText: 'Todavía no tienes comprobantes' }}
                  />
                </>
              ),
            },
          ]}
        />
      </div>

      <Modal
        title={
          unidadHistorial
            ? `Historial · ${unidadHistorial.marca} ${unidadHistorial.modelo} · ${identificadorUnidad(unidadHistorial)}`
            : 'Historial'
        }
        open={unidadHistorial !== null}
        onCancel={() => setUnidadHistorial(null)}
        footer={<Button onClick={() => setUnidadHistorial(null)}>Cerrar</Button>}
        width={820}
        destroyOnHidden
      >
        <AvisoError error={historial.error} />
        {historial.isPending && <p className="texto-secundario">Cargando el historial…</p>}
        {historial.data && <HistorialDeUnidad atenciones={historial.data} />}
      </Modal>

      <Modal
        title={orden ? `${referenciaOrden(orden)} · ${orden.vehiculoMarca} ${orden.vehiculoModelo}` : 'Orden'}
        open={ordenVista !== null}
        onCancel={() => setOrdenVista(null)}
        footer={[
          <Button key="fotos" onClick={() => setModalFotos(true)} disabled={!orden}>
            Fotos
          </Button>,
          <Button key="formato" onClick={() => setModalFormato(true)} disabled={!orden}>
            Formato de atención
          </Button>,
          <Button key="cerrar" type="primary" onClick={() => setOrdenVista(null)}>
            Cerrar
          </Button>,
        ]}
        width={900}
        destroyOnHidden
      >
        <AvisoError error={detalle.error} />
        {detalle.isPending && ordenVista && <p className="texto-secundario">Cargando la orden…</p>}
        {orden && (
          <>
            <div className="bloque-modal">
              <PanelAprobaciones orden={orden} />
            </div>

            <div className="bloque-modal">
              <h3>Orden</h3>
              <table className="tabla-simple">
                <tbody>
                  <tr>
                    <td>Estado</td>
                    <td>
                      <EstadoOrdenApiTag estadoId={orden.estadoId} />
                    </td>
                  </tr>
                  {fila('Unidad', `${orden.vehiculoMarca} ${orden.vehiculoModelo} · ${orden.vehiculoPlaca ?? 'sin placa'}`)}
                  {fila('Técnico', orden.tecnicoNombre ?? 'Por asignar')}
                  {fila('Ingreso', fechaHora(fechaIngresoOrden(orden)))}
                  {fila('Entrega estimada', orden.fechaEstimadaEntrega ? fechaHora(orden.fechaEstimadaEntrega) : null)}
                  {fila('Medidor al ingresar', lecturaIngresoOrden(orden))}
                  {fila('Falla reportada', orden.motivoFalla)}
                  {fila('Diagnóstico', orden.diagnostico)}
                  {fila('Solución', orden.solucion)}
                </tbody>
              </table>
            </div>

            <div className="bloque-modal">
              <h3>Presupuesto</h3>
              <Table
                rowKey="id"
                size="small"
                columns={columnasPresupuesto}
                dataSource={orden.detalles}
                pagination={false}
                locale={{ emptyText: 'El taller todavía no registra trabajos ni repuestos' }}
              />
              <div className="totales">
                <div>
                  <div className="etiqueta">Op. gravadas</div>
                  <div className="valor">{soles(orden.subtotalGravado ?? 0)}</div>
                </div>
                <div>
                  <div className="etiqueta">IGV</div>
                  <div className="valor">{soles(orden.montoIgv ?? 0)}</div>
                </div>
                <div>
                  <div className="etiqueta">Total</div>
                  <div className="valor total">{soles(orden.total)}</div>
                </div>
                <div>
                  <div className="etiqueta">Pagado</div>
                  <div className="valor">{soles(orden.totalPagado ?? 0)}</div>
                </div>
                <div>
                  <div className="etiqueta">Saldo</div>
                  <div className="valor">{soles(orden.saldo ?? orden.total)}</div>
                </div>
              </div>
            </div>

            {(orden.historial?.length ?? 0) > 0 && (
              <div className="bloque-modal">
                <h3>Historial</h3>
                <Timeline
                  items={[...(orden.historial ?? [])].sort(porFecha).map((cambio) => {
                    const cambiaEstado = cambio.estadoAnteriorId !== cambio.estadoNuevoId
                    return {
                      key: cambio.id,
                      color: cambiaEstado ? undefined : 'gray',
                      content: (
                        <div>
                          <strong>
                            {cambiaEstado
                              ? (nombresEstado[cambio.estadoNuevoId] ?? cambio.estadoNuevo)
                              : (cambio.observaciones ?? 'Actualización')}
                          </strong>
                          <div className="texto-secundario">{fechaHora(cambio.fechaCambio)}</div>
                          {cambiaEstado && cambio.observaciones && <div>{cambio.observaciones}</div>}
                        </div>
                      ),
                    }
                  })}
                />
              </div>
            )}
          </>
        )}
      </Modal>

      {orden && (
        <>
          <ModalFotosOrden
            abierto={modalFotos}
            ordenServicioId={orden.id}
            numeroOrden={orden.numeroOrden}
            soloLectura
            onCerrar={() => setModalFotos(false)}
          />
          <ModalFormatoAtencion abierto={modalFormato} ordenServicioId={orden.id} onCerrar={() => setModalFormato(false)} />
        </>
      )}
    </>
  )
}
