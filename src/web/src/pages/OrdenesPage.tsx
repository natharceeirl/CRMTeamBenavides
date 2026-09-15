import { useState } from 'react'
import { Button, Input, Select, Table, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link, useNavigate } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { EstadoOrdenTag } from '../components/EstadoOrdenTag'
import {
  buscarCliente,
  buscarUnidad,
  estadosOrden,
  identificadorUnidad,
  nombreUnidad,
  ordenes,
  type EstadoOrden,
  type Orden,
} from '../data/ejemplo'
import { soles } from '../utils/formato'

const opcionesEstado = (Object.keys(estadosOrden) as EstadoOrden[]).map((clave) => ({
  value: clave,
  label: estadosOrden[clave],
}))

const opcionesTecnico = [...new Set(ordenes.map((orden) => orden.tecnico))].map((tecnico) => ({
  value: tecnico,
  label: tecnico,
}))

const columnas: TableProps<Orden>['columns'] = [
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
  { title: 'Cliente', key: 'cliente', render: (_, orden) => buscarCliente(orden.clienteId)?.nombre ?? '—' },
  {
    title: 'Unidad',
    key: 'unidad',
    render: (_, orden) => {
      const unidad = buscarUnidad(orden.unidadId)
      return unidad ? `${nombreUnidad(unidad)} · ${identificadorUnidad(unidad)}` : '—'
    },
  },
  { title: 'Técnico', dataIndex: 'tecnico', className: 'sin-salto' },
  { title: 'Estado', key: 'estado', render: (_, orden) => <EstadoOrdenTag estado={orden.estado} /> },
  { title: 'Entrega', dataIndex: 'entrega', className: 'num' },
  {
    title: 'Total',
    dataIndex: 'total',
    align: 'right',
    className: 'num',
    render: (total: number) => (total > 0 ? soles(total) : '—'),
  },
]

export function OrdenesPage() {
  const navigate = useNavigate()
  const [texto, setTexto] = useState('')
  const [estado, setEstado] = useState<EstadoOrden>()
  const [tecnico, setTecnico] = useState<string>()

  const visibles = ordenes.filter((orden) => {
    const unidad = buscarUnidad(orden.unidadId)
    const cliente = buscarCliente(orden.clienteId)
    const busqueda = [orden.numero, orden.motivo, cliente?.nombre, unidad?.placa, unidad?.serie]
      .join(' ')
      .toLowerCase()
    return (
      (!texto || busqueda.includes(texto.toLowerCase())) &&
      (!estado || orden.estado === estado) &&
      (!tecnico || orden.tecnico === tecnico)
    )
  })

  return (
    <>
      <BarraSuperior
        titulo="Órdenes de servicio"
        acciones={
          <Button type="primary" onClick={() => navigate('/ordenes/nueva')}>
            Crear orden
          </Button>
        }
      />
      <div className="pagina">
        <section>
          <div className="filtros">
            <Input
              id="buscar-ordenes"
              prefix={<SearchOutlined />}
              placeholder="Buscar orden, cliente, placa o serie"
              allowClear
              value={texto}
              onChange={(evento) => setTexto(evento.target.value)}
              style={{ width: 320 }}
            />
            <Select<EstadoOrden>
              id="filtro-estado"
              allowClear
              placeholder="Estado"
              value={estado}
              onChange={setEstado}
              options={opcionesEstado}
              style={{ width: 220 }}
            />
            <Select<string>
              id="filtro-tecnico"
              allowClear
              placeholder="Técnico"
              value={tecnico}
              onChange={setTecnico}
              options={opcionesTecnico}
              style={{ width: 200 }}
            />
          </div>
          <Table rowKey="numero" columns={columnas} dataSource={visibles} pagination={false} />
        </section>
      </div>
    </>
  )
}
