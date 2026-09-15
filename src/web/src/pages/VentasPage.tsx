import { useState } from 'react'
import { Button, Segmented, Table, Tag, type TableProps } from 'antd'
import type { CSSProperties } from 'react'
import { BarraSuperior } from '../components/BarraSuperior'
import { buscarCliente, documentosVenta, type Documento } from '../data/ejemplo'
import { colores } from '../theme/tokens'
import { soles } from '../utils/formato'

type Filtro = 'Todos' | 'Cotizaciones' | 'Ventas'

const estilosEstado: Record<Documento['estado'], CSSProperties> = {
  Enviada: { background: 'transparent', color: colores.acento700, borderColor: colores.acento },
  Aceptada: { background: colores.acento100, color: colores.acento800, borderColor: colores.acento200 },
  Pagada: { background: colores.neutro100, color: colores.neutro800, borderColor: colores.neutro300 },
  Vencida: { background: 'transparent', color: colores.textoSecundario, borderColor: colores.neutro300 },
  Anulada: {
    background: 'transparent',
    color: colores.textoSecundario,
    borderColor: colores.neutro300,
    textDecoration: 'line-through',
  },
}

const columnas: TableProps<Documento>['columns'] = [
  { title: 'Número', dataIndex: 'numero', className: 'num', render: (numero: string) => <strong>{numero}</strong> },
  { title: 'Tipo', dataIndex: 'tipo' },
  { title: 'Cliente', key: 'cliente', render: (_, documento) => buscarCliente(documento.clienteId)?.nombre ?? '—' },
  { title: 'Fecha', dataIndex: 'fecha', className: 'num' },
  {
    title: 'Estado',
    dataIndex: 'estado',
    render: (estado: Documento['estado']) => (
      <Tag style={{ ...estilosEstado[estado], marginInlineEnd: 0, fontWeight: 600 }}>{estado}</Tag>
    ),
  },
  { title: 'Total', dataIndex: 'total', align: 'right', className: 'num', render: (total: number) => soles(total) },
]

export function VentasPage() {
  const [filtro, setFiltro] = useState<Filtro>('Todos')

  const visibles = documentosVenta.filter((documento) => {
    if (filtro === 'Cotizaciones') return documento.tipo === 'Cotización'
    if (filtro === 'Ventas') return documento.tipo === 'Venta'
    return true
  })

  return (
    <>
      <BarraSuperior
        titulo="Ventas y cotizaciones"
        acciones={
          <>
            <Button>Nueva cotización</Button>
            <Button type="primary">Venta de mostrador</Button>
          </>
        }
      />
      <div className="pagina">
        <section>
          <div className="filtros">
            <Segmented<Filtro> options={['Todos', 'Cotizaciones', 'Ventas']} value={filtro} onChange={setFiltro} />
          </div>
          <Table rowKey="numero" columns={columnas} dataSource={visibles} pagination={false} />
        </section>
      </div>
    </>
  )
}
