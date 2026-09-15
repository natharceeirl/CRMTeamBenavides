import { Button, Input, Table, Tag, type TableProps } from 'antd'
import { Link, useParams } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { EstadoOrdenTag } from '../components/EstadoOrdenTag'
import { Indicadores } from '../components/Indicadores'
import {
  buscarCliente,
  buscarUnidad,
  identificadorUnidad,
  nombreUnidad,
  ordenes,
  type LineaOrden,
} from '../data/ejemplo'
import { entero, importe, soles } from '../utils/formato'

const TASA_IGV = 0.18

const columnas: TableProps<LineaOrden>['columns'] = [
  { title: 'Concepto', dataIndex: 'concepto' },
  { title: 'Tipo', dataIndex: 'tipo' },
  {
    title: 'Cant.',
    key: 'cantidad',
    align: 'right',
    className: 'num',
    render: (_, linea) => (linea.unidad ? `${linea.cantidad} ${linea.unidad}` : linea.cantidad),
  },
  {
    title: 'P. unit.',
    dataIndex: 'precioUnitario',
    align: 'right',
    className: 'num',
    render: (precio: number) => importe(precio),
  },
  {
    title: 'Importe',
    key: 'importe',
    align: 'right',
    className: 'num',
    render: (_, linea) => importe(linea.cantidad * linea.precioUnitario),
  },
]

export function OrdenDetallePage() {
  const { numero } = useParams()
  const orden = ordenes.find((item) => item.numero === numero)

  if (!orden) {
    return (
      <>
        <BarraSuperior antetitulo="Órdenes" titulo="Orden no encontrada" />
        <div className="pagina">
          <p>
            No existe la orden {numero}. <Link to="/ordenes">Volver a órdenes</Link>
          </p>
        </div>
      </>
    )
  }

  const unidad = buscarUnidad(orden.unidadId)
  const cliente = buscarCliente(orden.clienteId)
  const lineas = orden.lineas ?? []
  const sumaPorTipo = (tipo: LineaOrden['tipo']) =>
    lineas.filter((linea) => linea.tipo === tipo).reduce((suma, linea) => suma + linea.cantidad * linea.precioUnitario, 0)
  const manoDeObra = sumaPorTipo('Mano de obra')
  const repuestos = sumaPorTipo('Repuesto')
  const total = manoDeObra + repuestos
  const gravada = total / (1 + TASA_IGV)
  const igv = total - gravada
  const bitacora = orden.bitacora ?? [{ evento: 'Unidad recibida', fecha: orden.ingreso }]
  const historial = ordenes.filter((item) => item.unidadId === orden.unidadId && item.numero !== orden.numero)

  return (
    <>
      <header className="barra-superior detalle">
        <div>
          <div className="etiqueta">{orden.numero}</div>
          <h1 className="titulo-orden">
            {unidad ? `${nombreUnidad(unidad)} · ${identificadorUnidad(unidad)}` : orden.numero}
          </h1>
          <div className="etiquetas-orden">
            <EstadoOrdenTag estado={orden.estado} />
            <Tag style={{ marginInlineEnd: 0 }}>{orden.tecnico}</Tag>
          </div>
        </div>
        <div className="acciones">
          <Button>Agregar repuesto</Button>
          <Button type="primary">
            {orden.estado === 'esperando_aprobacion' ? 'Registrar aprobación' : 'Cambiar estado'}
          </Button>
        </div>
      </header>

      <div className="pagina">
        <Indicadores
          tamano="mediano"
          items={[
            { etiqueta: 'Cliente', valor: cliente?.nombre ?? '—' },
            { etiqueta: 'Ingreso', valor: orden.ingreso },
            { etiqueta: 'Medidor', valor: unidad ? `${entero(unidad.medidor)} ${unidad.unidadMedidor}` : '—' },
            { etiqueta: 'Total', valor: total > 0 ? soles(total) : '—' },
          ]}
        />

        <div className="dos-columnas">
          <section>
            <div className="seccion-titulo">
              <h2>Trabajos y repuestos</h2>
            </div>
            <Table
              rowKey="concepto"
              columns={columnas}
              dataSource={lineas}
              pagination={false}
              locale={{ emptyText: 'Aún no hay trabajos ni repuestos registrados' }}
            />
            <div className="totales">
              <div>
                <div className="etiqueta">Mano de obra</div>
                <div className="valor">{importe(manoDeObra)}</div>
              </div>
              <div>
                <div className="etiqueta">Repuestos</div>
                <div className="valor">{importe(repuestos)}</div>
              </div>
              <div>
                <div className="etiqueta">Op. gravada</div>
                <div className="valor">{importe(gravada)}</div>
              </div>
              <div>
                <div className="etiqueta">IGV 18 %</div>
                <div className="valor">{importe(igv)}</div>
              </div>
              <div>
                <div className="etiqueta">Total</div>
                <div className="valor total">{soles(total)}</div>
              </div>
            </div>
            <Button type="primary" disabled={orden.estado !== 'lista_entrega'} style={{ marginTop: 20 }}>
              Registrar comprobante
            </Button>
          </section>

          <aside className="columna">
            <section>
              <div className="seccion-titulo">
                <h2>Bitácora</h2>
              </div>
              <table className="tabla-simple">
                <tbody>
                  {bitacora.map((registro) => (
                    <tr key={`${registro.fecha}-${registro.evento}`}>
                      <td>{registro.evento}</td>
                      <td>{registro.fecha}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
            <section>
              <div className="seccion-titulo">
                <h2>Nota del asesor</h2>
              </div>
              <Input.TextArea id="nota-asesor" rows={4} defaultValue={orden.nota} placeholder="Escribe una nota interna" />
            </section>
            <section>
              <div className="seccion-titulo">
                <h2>Historial de la unidad</h2>
              </div>
              {historial.length > 0 ? (
                <table className="tabla-simple">
                  <tbody>
                    {historial.map((item) => (
                      <tr key={item.numero}>
                        <td>
                          <Link to={`/ordenes/${item.numero}`}>{item.numero}</Link>
                        </td>
                        <td>{item.ingreso}</td>
                        <td>{item.total > 0 ? soles(item.total) : '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p className="texto-secundario">Sin órdenes anteriores.</p>
              )}
            </section>
          </aside>
        </div>
      </div>
    </>
  )
}
