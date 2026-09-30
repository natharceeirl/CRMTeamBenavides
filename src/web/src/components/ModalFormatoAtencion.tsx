import { useState } from 'react'
import { Button, Modal, Table, type TableProps } from 'antd'
import { useFormatoAtencionOrden } from '../api/ordenes'
import type { FormatoAtencionItemDto } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { EtiquetaEstado } from './EtiquetaEstado'
import { entero, fechaHora, importe, nombreDeEnum, soles } from '../utils/formato'
import { nombresEstado } from '../api/ordenes'
import { abrirDocumento } from '../utils/impresion'
import { htmlFormatoAtencion } from '../utils/formatoAtencion'

type Props = {
  abierto: boolean
  ordenServicioId: string
  onCerrar: () => void
}

const columnas: TableProps<FormatoAtencionItemDto>['columns'] = [
  {
    title: 'Concepto',
    key: 'concepto',
    render: (_, item) => (
      <>
        <div>{item.descripcion}</div>
        <div className="texto-secundario">
          {[item.tipoItemNombre, item.afectacionIgv === 'Gravado' ? null : item.afectacionIgv].filter(Boolean).join(' · ')}
        </div>
      </>
    ),
  },
  { title: 'Cant.', dataIndex: 'cantidad', align: 'right', className: 'num' },
  { title: 'P. unit.', dataIndex: 'precioUnitario', align: 'right', className: 'num', render: (valor: number) => importe(valor) },
  { title: 'Total', dataIndex: 'total', align: 'right', className: 'num', render: (valor: number) => importe(valor) },
]

/** Una fila de la tabla simple, solo si hay dato. */
const fila = (etiqueta: string, valor: string | number | null | undefined) =>
  valor === null || valor === undefined || valor === '' ? null : (
    <tr key={etiqueta}>
      <td>{etiqueta}</td>
      <td>{valor}</td>
    </tr>
  )

/** Vista previa del formato de atención; la hoja para imprimir se arma con los mismos datos. */
export function ModalFormatoAtencion({ abierto, ordenServicioId, onCerrar }: Readonly<Props>) {
  const query = useFormatoAtencionOrden(abierto ? ordenServicioId : null)
  const datos = query.data
  const [errorImpresion, setErrorImpresion] = useState<unknown>(null)

  // La hoja se arma en la web con el estilo de la marca, igual que la ficha
  // del comprobante: no hace falta volver a pedir nada a la API.
  const handleImprimir = () => {
    if (!datos) return
    setErrorImpresion(null)
    abrirDocumento(async () => htmlFormatoAtencion(datos)).catch(setErrorImpresion)
  }

  const unidadMedida = datos?.unidad.tipoMedidor === 'Horas' ? 'h' : 'km'

  return (
    <Modal
      title={datos ? `Formato de atención · ${datos.orden.numeroOrden}` : 'Formato de atención'}
      open={abierto}
      onCancel={onCerrar}
      footer={[
        <Button key="cerrar" onClick={onCerrar}>
          Cerrar
        </Button>,
        <Button key="imprimir" type="primary" onClick={handleImprimir} disabled={!datos}>
          Imprimir o guardar PDF
        </Button>,
      ]}
      width={860}
      destroyOnHidden
    >
      <AvisoError error={query.error ?? errorImpresion} />
      {query.isPending && <p className="texto-secundario">Cargando el formato…</p>}

      {datos && (
        <>
          <div className="bloque-modal">
            <h2 style={{ fontSize: 21 }}>{datos.empresa.nombreTaller}</h2>
            <p className="texto-secundario">
              {[
                datos.empresa.razonSocial,
                datos.empresa.ruc ? `RUC ${datos.empresa.ruc}` : null,
                datos.empresa.direccion,
                datos.empresa.telefono,
              ]
                .filter(Boolean)
                .join(' · ')}
            </p>
          </div>

          <div className="dos-bloques bloque-modal">
            <div>
              <h3>Cliente</h3>
              <table className="tabla-simple">
                <tbody>
                  {fila('Nombre', datos.cliente.nombreCompleto)}
                  {fila(datos.cliente.tipoDocumento ?? 'Documento', datos.cliente.numeroDocumento)}
                  {fila('Teléfono', datos.cliente.telefono)}
                  {fila('Dirección', datos.cliente.direccion)}
                </tbody>
              </table>
            </div>
            <div>
              <h3>Unidad</h3>
              <table className="tabla-simple">
                <tbody>
                  {fila('Unidad', `${nombreDeEnum(datos.unidad.tipoUnidad)} · ${datos.unidad.marca} ${datos.unidad.modelo}`)}
                  {fila('Placa', datos.unidad.placa)}
                  {fila('VIN o serie', datos.unidad.numeroSerieVIN)}
                  {fila(
                    'Medidor al ingresar',
                    datos.unidad.lecturaIngreso == null ? null : `${entero(datos.unidad.lecturaIngreso)} ${unidadMedida}`,
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div className="bloque-modal">
            <h3>Atención</h3>
            <table className="tabla-simple">
              <tbody>
                <tr>
                  <td>Estado</td>
                  <td>
                    <EtiquetaEstado tono="neutro">
                      {nombresEstado[datos.orden.estadoId] ?? nombreDeEnum(datos.orden.estadoNombre)}
                    </EtiquetaEstado>
                  </td>
                </tr>
                {fila('Ingreso', fechaHora(datos.orden.fechaIngreso))}
                {fila('Entrega estimada', datos.orden.fechaEstimadaEntrega ? fechaHora(datos.orden.fechaEstimadaEntrega) : null)}
                {fila('Tipo de atención', `${nombreDeEnum(datos.orden.tipoAtencion)} · ${nombreDeEnum(datos.orden.modalidadAtencion)}`)}
                {fila('Técnico', datos.trabajo.tecnicoResponsable ?? 'Sin asignar')}
                {fila('Falla reportada', datos.trabajo.motivoFalla)}
                {fila('Diagnóstico', datos.trabajo.diagnostico)}
                {fila('Solución', datos.trabajo.solucion)}
              </tbody>
            </table>
          </div>

          <div className="bloque-modal">
            <h3>Trabajos y repuestos</h3>
            <Table
              rowKey="id"
              size="small"
              columns={columnas}
              dataSource={datos.items}
              pagination={false}
              locale={{ emptyText: 'Sin trabajos ni repuestos' }}
            />
            <div className="totales">
              <div>
                <div className="etiqueta">Op. gravadas</div>
                <div className="valor">{soles(datos.financiero.subtotalGravado)}</div>
              </div>
              {datos.financiero.subtotalExonerado > 0 && (
                <div>
                  <div className="etiqueta">Exoneradas</div>
                  <div className="valor">{soles(datos.financiero.subtotalExonerado)}</div>
                </div>
              )}
              {datos.financiero.subtotalInafecto > 0 && (
                <div>
                  <div className="etiqueta">Inafectas</div>
                  <div className="valor">{soles(datos.financiero.subtotalInafecto)}</div>
                </div>
              )}
              <div>
                <div className="etiqueta">IGV ({datos.financiero.porcentajeIgv} %)</div>
                <div className="valor">{soles(datos.financiero.montoIgv)}</div>
              </div>
              <div>
                <div className="etiqueta">Total</div>
                <div className="valor total">{soles(datos.financiero.total)}</div>
              </div>
              <div>
                <div className="etiqueta">Pagado</div>
                <div className="valor">{soles(datos.financiero.totalPagado)}</div>
              </div>
              <div>
                <div className="etiqueta">Saldo</div>
                <div className="valor">{soles(datos.financiero.saldoPendiente)}</div>
              </div>
            </div>
          </div>
        </>
      )}
    </Modal>
  )
}
