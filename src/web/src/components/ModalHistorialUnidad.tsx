import { Button, Modal, Table } from 'antd'
import { Link } from 'react-router'
import { AvisoError } from './AvisoError'
import { EstadoOrdenApiTag } from './EstadoOrdenApiTag'
import { nombresEstado, nombresEstadoCliente } from '../api/ordenes'
import { useHistorialServicioUnidad, type AtencionServicioUnidad, type HistorialServicioItem } from '../api/portal'
import { nombresTipoItem, type VehiculoResponse } from '../api/tipos'
import { entero, fechaHora, importe, soles } from '../utils/formato'
import { identificadorUnidad } from '../utils/unidades'

const fila = (etiqueta: string, valor: string | number | null | undefined) =>
  valor === null || valor === undefined || valor === '' ? null : (
    <tr key={etiqueta}>
      <td>{etiqueta}</td>
      <td>{valor}</td>
    </tr>
  )

// En las tablas, en el teléfono, se desliza de lado en vez de cortar columnas.
const deslizable = { x: 'max-content' } as const

type PropsHistorial = {
  atenciones: AtencionServicioUnidad[]
  /** El personal ve los estados internos, el tipo de cada ítem y el enlace a la orden; el cliente, los suyos. */
  paraPersonal: boolean
  puedeVerOrdenes: boolean
}

/** Lo que se hizo en el taller en una unidad: cada atención con sus trabajos. */
function HistorialDeUnidad({ atenciones, paraPersonal, puedeVerOrdenes }: Readonly<PropsHistorial>) {
  if (atenciones.length === 0) {
    return <p className="texto-secundario">Esta unidad todavía no tiene servicios en el taller.</p>
  }

  return (
    <>
      {atenciones.map((atencion) => {
        const referencia = atencion.numeroOrden ?? 'Atención'
        const unidadMedidor = atencion.tipoMedidor === 'Horas' ? 'h' : 'km'
        return (
          <section key={atencion.ordenServicioId} className="bloque-modal">
            <div className="seccion-titulo" style={{ marginBottom: 8 }}>
              <h3 style={{ margin: 0 }}>
                {paraPersonal && puedeVerOrdenes ? (
                  <Link to={`/ordenes/${atencion.ordenServicioId}`}>{referencia}</Link>
                ) : (
                  referencia
                )}{' '}
                · {fechaHora(atencion.fechaIngreso)}
              </h3>
              <div className="acciones">
                <EstadoOrdenApiTag
                  estadoId={atencion.estadoId}
                  nombres={paraPersonal ? nombresEstado : nombresEstadoCliente}
                />
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
                    : `${entero(atencion.lecturaMedidorIngreso)} ${unidadMedidor}`,
                )}
                {fila('Salida', atencion.fechaSalida ? fechaHora(atencion.fechaSalida) : null)}
                {paraPersonal && fila('Observaciones del cliente', atencion.observacionesCliente)}
              </tbody>
            </table>
            {atencion.items.length > 0 && (
              <Table<HistorialServicioItem>
                rowKey="id"
                size="small"
                pagination={false}
                dataSource={atencion.items}
                scroll={deslizable}
                style={{ marginTop: 12 }}
                columns={[
                  {
                    title: 'Trabajo o repuesto',
                    key: 'descripcion',
                    render: (_, item) => (
                      <>
                        <div>{item.descripcion}</div>
                        {paraPersonal && (
                          <div className="texto-secundario">
                            {[nombresTipoItem[item.tipoItem] ?? item.tipoItemNombre, item.productoCodigo]
                              .filter(Boolean)
                              .join(' · ')}
                          </div>
                        )}
                      </>
                    ),
                  },
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
        )
      })}
    </>
  )
}

type Props = {
  /** La unidad cuyo historial se muestra; null cierra el modal. */
  unidad: VehiculoResponse | null
  onCerrar: () => void
  paraPersonal?: boolean
  /** Solo para el personal: enlaza cada atención con su orden. */
  puedeVerOrdenes?: boolean
}

/**
 * Historial de servicio de una unidad (GET /vehiculos/{id}/historial-servicio). Lo usan el
 * personal, en Unidades, y el cliente, en su portal; el backend filtra al cliente lo suyo.
 */
export function ModalHistorialUnidad({ unidad, onCerrar, paraPersonal = false, puedeVerOrdenes = false }: Readonly<Props>) {
  const historial = useHistorialServicioUnidad(unidad?.id ?? null)

  return (
    <Modal
      title={unidad ? `Historial · ${unidad.marca} ${unidad.modelo} · ${identificadorUnidad(unidad)}` : 'Historial'}
      open={unidad !== null}
      onCancel={onCerrar}
      footer={<Button onClick={onCerrar}>Cerrar</Button>}
      width={820}
      destroyOnHidden
    >
      <AvisoError error={historial.error} />
      {historial.isPending && <p className="texto-secundario">Cargando el historial…</p>}
      {historial.data && (
        <HistorialDeUnidad atenciones={historial.data} paraPersonal={paraPersonal} puedeVerOrdenes={puedeVerOrdenes} />
      )}
    </Modal>
  )
}
