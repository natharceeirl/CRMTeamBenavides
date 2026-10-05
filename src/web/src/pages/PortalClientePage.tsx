import { useState } from 'react'
import { Button, Modal, Table, Tabs, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { Indicadores } from '../components/Indicadores'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { EtiquetaEstado } from '../components/EtiquetaEstado'
import { EstadoCitaTag } from '../components/EstadoCitaTag'
import { EstadoPedidoLimaTag } from '../components/EstadoPedidoLimaTag'
import { PanelAprobaciones } from '../components/PanelAprobaciones'
import { AvanceOrden } from '../components/AvanceOrden'
import { ModalFotosOrden } from '../components/ModalFotosOrden'
import { ModalFormatoAtencion } from '../components/ModalFormatoAtencion'
import { ModalCita } from '../components/ModalCita'
import { ModalDetalleCita } from '../components/ModalDetalleCita'
import { useVehiculos } from '../api/vehiculos'
import {
  PRESUPUESTO,
  esEstadoTerminal,
  esperaRespuestaDelCliente,
  etiquetaPresupuesto,
  fechaIngresoOrden,
  nombresEstadoCliente,
  useOrden,
  useOrdenes,
} from '../api/ordenes'
import { useCitas } from '../api/citas'
import { nombresEstadoPedidoCliente, usePedidosLima } from '../api/pedidosLima'
import {
  useHistorialServicioUnidad,
  usePortalComprobantes,
  usePortalResumen,
  type AtencionServicioUnidad,
  type PortalComprobante,
} from '../api/portal'
import {
  nombresTipoItem,
  type CitaListResponse,
  type DetalleServicioResponse,
  type OrdenServicioResponse,
  type PedidoLimaResponse,
  type VehiculoResponse,
} from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { entero, fechaCorta, fechaHora, importe, referenciaOrden, soles } from '../utils/formato'
import { identificadorUnidad, lecturaIngresoOrden, lecturaMedidor, nombreTipoUnidad } from '../utils/unidades'
import { textoDuracion } from '../utils/agenda'

const fila = (etiqueta: string, valor: string | number | null | undefined) =>
  valor === null || valor === undefined || valor === '' ? null : (
    <tr key={etiqueta}>
      <td>{etiqueta}</td>
      <td>{valor}</td>
    </tr>
  )

// En las tablas, en el teléfono, se desliza de lado en vez de cortar columnas.
const deslizable = { x: 'max-content' } as const

const unidadDe = (orden: { vehiculoMarca: string; vehiculoModelo: string; vehiculoPlaca?: string | null }) =>
  `${orden.vehiculoMarca} ${orden.vehiculoModelo}${orden.vehiculoPlaca ? ` · ${orden.vehiculoPlaca}` : ''}`

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
              <EstadoOrdenApiTag estadoId={atencion.estadoId} nombres={nombresEstadoCliente} />
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
              scroll={deslizable}
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

/** Una orden en curso: dónde está, cuándo se entrega y si espera algo del cliente. */
function TarjetaOrden({ orden, onVer }: Readonly<{ orden: OrdenServicioResponse; onVer: () => void }>) {
  const responder = esperaRespuestaDelCliente(orden)
  return (
    <article className="tarjeta-orden">
      <div className="tarjeta-orden-cabecera">
        <strong className="num">{referenciaOrden(orden)}</strong>
        <EstadoOrdenApiTag estadoId={orden.estadoId} nombres={nombresEstadoCliente} />
      </div>
      <div>{unidadDe(orden)}</div>
      {orden.fechaEstimadaEntrega && (
        <div className="texto-secundario">Entrega estimada: {fechaHora(orden.fechaEstimadaEntrega)}</div>
      )}
      {responder && <div className="tarjeta-orden-aviso">Tu presupuesto espera respuesta</div>}
      <Button type={responder ? 'primary' : 'default'} onClick={onVer}>
        {responder ? 'Ver presupuesto' : 'Ver avance'}
      </Button>
    </article>
  )
}

/**
 * Portal del cliente en la web, con lo mismo que su app: sus órdenes en curso con
 * su avance, el presupuesto por responder, sus unidades, citas, repuestos
 * encargados y comprobantes. El backend filtra todo al cliente de la sesión.
 */
export function PortalClientePage() {
  const { tienePermiso } = useSesion()
  const veCitas = tienePermiso(PERMISOS.citasVer)
  const puedeAgendar = tienePermiso(PERMISOS.citasCrear)
  const vePedidos = tienePermiso(PERMISOS.pedidosLimaVer)

  const [pestana, setPestana] = useState('ordenes')
  const [unidadHistorial, setUnidadHistorial] = useState<VehiculoResponse | null>(null)
  const [ordenVista, setOrdenVista] = useState<string | null>(null)
  const [citaVista, setCitaVista] = useState<string | null>(null)
  const [agendando, setAgendando] = useState(false)
  const [modalFotos, setModalFotos] = useState(false)
  const [modalFormato, setModalFormato] = useState(false)

  const resumen = usePortalResumen()
  const vehiculos = useVehiculos()
  const ordenes = useOrdenes()
  const comprobantes = usePortalComprobantes()
  const citas = useCitas({})
  const pedidos = usePedidosLima({})
  const detalle = useOrden(ordenVista ?? undefined)
  const historial = useHistorialServicioUnidad(unidadHistorial?.id ?? null)

  const datosResumen = resumen.data
  const orden = detalle.data
  const primerNombre = datosResumen?.clienteNombre.split(' ')[0]
  // Primero lo que espera respuesta del cliente; después, lo más reciente.
  const enCurso = (ordenes.data ?? [])
    .filter((item) => !esEstadoTerminal(item.estadoId))
    .sort(
      (una, otra) =>
        Number(esperaRespuestaDelCliente(otra)) - Number(esperaRespuestaDelCliente(una)) ||
        new Date(fechaIngresoOrden(otra)).getTime() - new Date(fechaIngresoOrden(una)).getTime(),
    )

  const columnasOrdenes: TableProps<OrdenServicioResponse>['columns'] = [
    { title: 'Orden', key: 'orden', className: 'num', render: (_, item) => <strong>{referenciaOrden(item)}</strong> },
    { title: 'Unidad', key: 'unidad', render: (_, item) => unidadDe(item) },
    { title: 'Ingreso', key: 'ingreso', className: 'num', render: (_, item) => fechaHora(fechaIngresoOrden(item)) },
    {
      title: 'Estado',
      key: 'estado',
      render: (_, item) => <EstadoOrdenApiTag estadoId={item.estadoId} nombres={nombresEstadoCliente} />,
    },
    {
      title: 'Presupuesto',
      key: 'presupuesto',
      render: (_, item) => {
        const etiqueta = etiquetaPresupuesto(
          item.estadoId,
          item.estadoPresupuestoClienteId ?? PRESUPUESTO.pendiente,
          item.total ?? 0,
        )
        return <EtiquetaEstado tono={etiqueta.tono}>{etiqueta.texto}</EtiquetaEstado>
      },
    },
    { title: 'Total', key: 'total', align: 'right', className: 'num', render: (_, item) => soles(item.total ?? 0) },
    {
      title: '',
      key: 'ver',
      align: 'right',
      render: (_, item) => (
        <Button type="link" onClick={() => setOrdenVista(item.id)}>
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

  const columnasCitas: TableProps<CitaListResponse>['columns'] = [
    {
      title: 'Fecha y hora',
      dataIndex: 'fechaHoraProgramada',
      className: 'num',
      render: (fecha: string) => fechaHora(fecha),
    },
    {
      title: 'Unidad',
      key: 'unidad',
      render: (_, cita) => `${cita.vehiculoModelo}${cita.vehiculoPlaca ? ` · ${cita.vehiculoPlaca}` : ''}`,
    },
    { title: 'Motivo', dataIndex: 'motivo' },
    { title: 'Duración', dataIndex: 'duracionMinutos', render: (minutos: number) => textoDuracion(minutos) },
    { title: 'Estado', dataIndex: 'estado', render: (estado: CitaListResponse['estado']) => <EstadoCitaTag estado={estado} /> },
    {
      title: '',
      key: 'ver',
      align: 'right',
      render: (_, cita) => (
        <Button type="link" onClick={() => setCitaVista(cita.id)}>
          Ver
        </Button>
      ),
    },
  ]

  const columnasPedidos: TableProps<PedidoLimaResponse>['columns'] = [
    { title: 'Pedido', dataIndex: 'numeroPedido', className: 'num', render: (numero: string) => <strong>{numero}</strong> },
    {
      title: 'Repuestos',
      key: 'repuestos',
      render: (_, pedido) => pedido.detalles.map((item) => `${item.cantidad} × ${item.productoNombre}`).join(', '),
    },
    {
      title: 'Estado',
      dataIndex: 'estado',
      render: (estado: PedidoLimaResponse['estado']) => (
        <EstadoPedidoLimaTag estado={estado} nombres={nombresEstadoPedidoCliente} />
      ),
    },
    {
      title: 'Llegada estimada',
      dataIndex: 'fechaEstimadaLlegada',
      className: 'num',
      render: (fecha: string | null) => fechaCorta(fecha),
    },
    { title: 'Total', dataIndex: 'total', align: 'right', className: 'num', render: (total: number) => soles(total) },
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

  const pestanas = [
    {
      key: 'ordenes',
      label: 'Historial de órdenes',
      children: (
        <>
          <AvisoError error={ordenes.error} />
          <Table
            rowKey="id"
            columns={columnasOrdenes}
            dataSource={ordenes.data ?? []}
            pagination={{ pageSize: 10, hideOnSinglePage: true }}
            loading={ordenes.isPending}
            scroll={deslizable}
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
            scroll={deslizable}
            locale={{ emptyText: 'Todavía no hay unidades a tu nombre' }}
          />
        </>
      ),
    },
    ...(veCitas
      ? [
          {
            key: 'citas',
            label: 'Citas',
            children: (
              <>
                <AvisoError error={citas.error} />
                {puedeAgendar && (
                  <div className="filtros">
                    <Button type="primary" onClick={() => setAgendando(true)}>
                      Agendar cita
                    </Button>
                  </div>
                )}
                <Table
                  rowKey="id"
                  columns={columnasCitas}
                  dataSource={citas.data ?? []}
                  pagination={{ pageSize: 10, hideOnSinglePage: true }}
                  loading={citas.isPending}
                  scroll={deslizable}
                  locale={{ emptyText: 'Todavía no tienes citas' }}
                />
              </>
            ),
          },
        ]
      : []),
    ...(vePedidos
      ? [
          {
            key: 'pedidos',
            label: 'Repuestos encargados',
            children: (
              <>
                <AvisoError error={pedidos.error} />
                <Table
                  rowKey="id"
                  columns={columnasPedidos}
                  dataSource={pedidos.data ?? []}
                  pagination={{ pageSize: 10, hideOnSinglePage: true }}
                  loading={pedidos.isPending}
                  scroll={deslizable}
                  locale={{ emptyText: 'No tienes repuestos encargados' }}
                />
              </>
            ),
          },
        ]
      : []),
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
            scroll={deslizable}
            locale={{ emptyText: 'Todavía no tienes comprobantes' }}
          />
        </>
      ),
    },
  ]

  return (
    <>
      <BarraSuperior antetitulo="Mi portal" titulo={primerNombre ? `Hola, ${primerNombre}` : 'Mi portal'} />

      <div className="pagina portal-cliente">
        <AvisoError error={resumen.error} />
        {datosResumen && (
          <Indicadores
            items={[
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

        {enCurso.length > 0 && (
          <section>
            <div className="seccion-titulo">
              <h2>{enCurso.length === 1 ? 'Tu orden en curso' : 'Tus órdenes en curso'}</h2>
            </div>
            <div className="tarjetas-orden">
              {enCurso.map((item) => (
                <TarjetaOrden key={item.id} orden={item} onVer={() => setOrdenVista(item.id)} />
              ))}
            </div>
          </section>
        )}

        <Tabs activeKey={pestana} onChange={setPestana} items={pestanas} />
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
              <h3>Avance</h3>
              <AvanceOrden orden={orden} />
            </div>

            <div className="bloque-modal">
              <h3>Presupuesto</h3>
              {orden.diagnostico && (
                <p style={{ marginTop: 0 }}>
                  <strong>Diagnóstico:</strong> {orden.diagnostico}
                </p>
              )}
              <Table
                rowKey="id"
                size="small"
                columns={columnasPresupuesto}
                dataSource={orden.detalles}
                pagination={false}
                scroll={deslizable}
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

            <div className="bloque-modal">
              <PanelAprobaciones orden={orden} titulo="Tu respuesta" />
            </div>

            <div className="bloque-modal">
              <h3>Orden</h3>
              <table className="tabla-simple">
                <tbody>
                  {fila('Unidad', unidadDe(orden))}
                  {fila('Técnico', orden.tecnicoNombre ?? 'Por asignar')}
                  {fila('Ingreso', fechaHora(fechaIngresoOrden(orden)))}
                  {fila('Entrega estimada', orden.fechaEstimadaEntrega ? fechaHora(orden.fechaEstimadaEntrega) : null)}
                  {fila('Medidor al ingresar', lecturaIngresoOrden(orden))}
                  {fila('Falla reportada', orden.motivoFalla)}
                  {fila('Solución', orden.solucion)}
                </tbody>
              </table>
            </div>
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

      <ModalCita
        abierto={agendando}
        paraCliente
        onCerrar={() => setAgendando(false)}
        onCreada={(cita) => {
          setPestana('citas')
          setCitaVista(cita.id)
        }}
      />
      <ModalDetalleCita citaId={citaVista} onCerrar={() => setCitaVista(null)} />
    </>
  )
}
