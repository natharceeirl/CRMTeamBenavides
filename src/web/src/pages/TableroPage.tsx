import { useState } from 'react'
import { Button, Input, Segmented, Table, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link, useNavigate } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { EstadoOrdenTag } from '../components/EstadoOrdenTag'
import { Indicadores } from '../components/Indicadores'
import {
  buscarUnidad,
  documentosVenta,
  estadosEnTaller,
  hoy,
  identificadorUnidad,
  nombreUnidad,
  ordenes,
  type Orden,
} from '../data/ejemplo'
import { entero, soles } from '../utils/formato'

const fechaHoy = '15/09'
type Filtro = 'Todas' | 'Entregas de hoy'

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
  {
    title: 'Unidad',
    key: 'unidad',
    render: (_, orden) => {
      const unidad = buscarUnidad(orden.unidadId)
      return unidad ? `${nombreUnidad(unidad)} · ${identificadorUnidad(unidad)}` : '—'
    },
  },
  { title: 'Motivo', dataIndex: 'motivo' },
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

export function TableroPage() {
  const navigate = useNavigate()
  const [filtro, setFiltro] = useState<Filtro>('Todas')

  const activas = ordenes.filter((orden) => orden.estado !== 'entregada' && orden.estado !== 'anulada')
  const visibles = filtro === 'Todas' ? activas : activas.filter((orden) => orden.entrega.startsWith(fechaHoy))
  const vendidoHoy = documentosVenta
    .filter((doc) => doc.tipo === 'Venta' && doc.estado === 'Pagada' && doc.fecha === fechaHoy)
    .reduce((suma, doc) => suma + doc.total, 0)

  return (
    <>
      <BarraSuperior
        antetitulo="Tablero"
        titulo={hoy}
        acciones={
          <>
            <Input
              id="buscar-tablero"
              prefix={<SearchOutlined />}
              placeholder="Buscar placa, serie u orden"
              allowClear
              style={{ width: 280 }}
            />
            <Button type="primary" onClick={() => navigate('/ordenes/nueva')}>
              Crear orden
            </Button>
          </>
        }
      />
      <div className="pagina">
        <Indicadores
          items={[
            { etiqueta: 'En taller', valor: ordenes.filter((o) => estadosEnTaller.includes(o.estado)).length },
            {
              etiqueta: 'Esperando repuesto',
              valor: ordenes.filter((o) => o.estado === 'esperando_repuesto').length,
              destacado: true,
            },
            { etiqueta: 'Listas para entrega', valor: ordenes.filter((o) => o.estado === 'lista_entrega').length },
            { etiqueta: 'Vendido hoy', valor: `S/ ${entero(vendidoHoy)}`, compacto: true },
          ]}
        />
        <section>
          <div className="seccion-titulo">
            <h2>Órdenes activas</h2>
            <div className="acciones">
              <Segmented<Filtro> options={['Todas', 'Entregas de hoy']} value={filtro} onChange={setFiltro} />
            </div>
          </div>
          <Table rowKey="numero" columns={columnas} dataSource={visibles} pagination={false} />
        </section>
      </div>
    </>
  )
}
