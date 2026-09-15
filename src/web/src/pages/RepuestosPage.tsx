import { useState } from 'react'
import { Button, Input, Select, Table, Tag, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { BarraSuperior } from '../components/BarraSuperior'
import { productos, type Producto } from '../data/ejemplo'
import { colores } from '../theme/tokens'
import { entero, soles } from '../utils/formato'

const categorias = [...new Set(productos.map((producto) => producto.categoria))].map((categoria) => ({
  value: categoria,
  label: categoria,
}))

function EstadoStock({ producto }: Readonly<{ producto: Producto }>) {
  if (producto.stock === 0) {
    return <Tag style={{ background: colores.texto, color: colores.fondo, borderColor: colores.texto, marginInlineEnd: 0 }}>Agotado</Tag>
  }
  if (producto.stock <= producto.stockMinimo) {
    return <Tag style={{ background: colores.acento100, color: colores.acento800, borderColor: colores.acento200, marginInlineEnd: 0 }}>Stock bajo</Tag>
  }
  return <Tag style={{ background: colores.neutro100, color: colores.neutro800, borderColor: colores.neutro300, marginInlineEnd: 0 }}>Disponible</Tag>
}

const columnas: TableProps<Producto>['columns'] = [
  { title: 'Código', dataIndex: 'codigo', className: 'num' },
  { title: 'Código Yamaha', dataIndex: 'codigoYamaha', className: 'num' },
  { title: 'Producto', dataIndex: 'nombre', render: (nombre: string) => <strong>{nombre}</strong> },
  { title: 'Categoría', dataIndex: 'categoria' },
  { title: 'Stock', dataIndex: 'stock', align: 'right', className: 'num', render: (stock: number) => entero(stock) },
  { title: 'Mínimo', dataIndex: 'stockMinimo', align: 'right', className: 'num', render: (minimo: number) => entero(minimo) },
  { title: 'Precio', dataIndex: 'precio', align: 'right', className: 'num', render: (precio: number) => soles(precio) },
  { title: 'Estado', key: 'estado', render: (_, producto) => <EstadoStock producto={producto} /> },
]

export function RepuestosPage() {
  const [texto, setTexto] = useState('')
  const [categoria, setCategoria] = useState<string>()

  const visibles = productos.filter((producto) => {
    const busqueda = [producto.codigo, producto.codigoYamaha, producto.nombre].join(' ').toLowerCase()
    return busqueda.includes(texto.toLowerCase()) && (!categoria || producto.categoria === categoria)
  })

  return (
    <>
      <BarraSuperior
        titulo="Repuestos"
        acciones={
          <>
            <Button>Registrar entrada</Button>
            <Button type="primary">Nuevo producto</Button>
          </>
        }
      />
      <div className="pagina">
        <section>
          <div className="filtros">
            <Input
              id="buscar-repuestos"
              prefix={<SearchOutlined />}
              placeholder="Buscar por código, código Yamaha o nombre"
              allowClear
              value={texto}
              onChange={(evento) => setTexto(evento.target.value)}
              style={{ width: 360 }}
            />
            <Select<string>
              id="filtro-categoria"
              allowClear
              placeholder="Categoría"
              value={categoria}
              onChange={setCategoria}
              options={categorias}
              style={{ width: 200 }}
            />
          </div>
          <Table rowKey="codigo" columns={columnas} dataSource={visibles} pagination={false} />
        </section>
      </div>
    </>
  )
}
