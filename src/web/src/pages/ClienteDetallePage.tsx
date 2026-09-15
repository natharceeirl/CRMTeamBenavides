import { Button, Table, type TableProps } from 'antd'
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
  tiposUnidad,
  unidades,
  type Orden,
  type Unidad,
} from '../data/ejemplo'
import { entero, soles } from '../utils/formato'

const columnasUnidades: TableProps<Unidad>['columns'] = [
  { title: 'Unidad', key: 'unidad', render: (_, unidad) => nombreUnidad(unidad) },
  { title: 'Tipo', key: 'tipo', render: (_, unidad) => tiposUnidad[unidad.tipo] },
  { title: 'Placa o serie', key: 'identificador', className: 'num', render: (_, unidad) => identificadorUnidad(unidad) },
  {
    title: 'Medidor',
    key: 'medidor',
    align: 'right',
    className: 'num',
    render: (_, unidad) => `${entero(unidad.medidor)} ${unidad.unidadMedidor}`,
  },
]

const columnasOrdenes: TableProps<Orden>['columns'] = [
  {
    title: 'Orden',
    dataIndex: 'numero',
    className: 'num',
    render: (numero: string) => (
      <Link to={`/ordenes/${numero}`} style={{ fontWeight: 600 }}>
        {numero}
      </Link>
    ),
  },
  {
    title: 'Unidad',
    key: 'unidad',
    render: (_, orden) => {
      const unidad = buscarUnidad(orden.unidadId)
      return unidad ? nombreUnidad(unidad) : '—'
    },
  },
  { title: 'Estado', key: 'estado', render: (_, orden) => <EstadoOrdenTag estado={orden.estado} /> },
  { title: 'Ingreso', dataIndex: 'ingreso', className: 'num' },
  {
    title: 'Total',
    dataIndex: 'total',
    align: 'right',
    className: 'num',
    render: (total: number) => (total > 0 ? soles(total) : '—'),
  },
]

export function ClienteDetallePage() {
  const { id } = useParams()
  const cliente = id ? buscarCliente(id) : undefined

  if (!cliente) {
    return (
      <>
        <BarraSuperior antetitulo="Clientes" titulo="Cliente no encontrado" />
        <div className="pagina">
          <p>
            No existe el cliente solicitado. <Link to="/clientes">Volver a clientes</Link>
          </p>
        </div>
      </>
    )
  }

  const unidadesCliente = unidades.filter((unidad) => unidad.clienteId === cliente.id)
  const ordenesCliente = ordenes.filter((orden) => orden.clienteId === cliente.id)

  return (
    <>
      <BarraSuperior
        antetitulo="Clientes"
        titulo={cliente.nombre}
        acciones={
          <>
            <Button>Editar datos</Button>
            <Button type="primary">Invitar a la app</Button>
          </>
        }
      />
      <div className="pagina">
        <Indicadores
          tamano="mediano"
          items={[
            { etiqueta: 'Documento', valor: `${cliente.tipoDocumento} ${cliente.documento}` },
            { etiqueta: 'Teléfono', valor: cliente.telefono },
            { etiqueta: 'Unidades', valor: unidadesCliente.length },
            { etiqueta: 'Órdenes', valor: ordenesCliente.length },
          ]}
        />
        <div className="dos-columnas">
          <div className="columna">
            <section>
              <div className="seccion-titulo">
                <h2>Unidades</h2>
                <div className="acciones">
                  <Button>Agregar unidad</Button>
                </div>
              </div>
              <Table rowKey="id" columns={columnasUnidades} dataSource={unidadesCliente} pagination={false} />
            </section>
            <section>
              <div className="seccion-titulo">
                <h2>Historial de órdenes</h2>
              </div>
              <Table
                rowKey="numero"
                columns={columnasOrdenes}
                dataSource={ordenesCliente}
                pagination={false}
                locale={{ emptyText: 'Este cliente todavía no tiene órdenes' }}
              />
            </section>
          </div>
          <aside>
            <div className="seccion-titulo">
              <h2>Contacto</h2>
            </div>
            <table className="tabla-simple">
              <tbody>
                <tr>
                  <td>Correo</td>
                  <td>{cliente.correo ?? '—'}</td>
                </tr>
                <tr>
                  <td>Dirección</td>
                  <td>{cliente.direccion ?? '—'}</td>
                </tr>
              </tbody>
            </table>
          </aside>
        </div>
      </div>
    </>
  )
}
