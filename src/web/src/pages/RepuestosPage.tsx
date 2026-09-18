import { useState } from 'react'
import { Button, Checkbox, Input, Popconfirm, Select, Space, Table, Tabs, Tag, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoStock } from '../components/EstadoStock'
import { ModalProducto } from '../components/ModalProducto'
import { ModalCategoria } from '../components/ModalCategoria'
import { ModalMovimientoStock, type TipoMovimiento } from '../components/ModalMovimientoStock'
import {
  MOVIMIENTO,
  useCategoriasProducto,
  useEliminarCategoria,
  useEliminarProducto,
  useMovimientos,
  useProductos,
} from '../api/inventario'
import type { CategoriaProductoResponse, MovimientoInventarioResponse, ProductoResponse } from '../api/tipos'
import { colores } from '../theme/tokens'
import { entero, fechaHora, soles } from '../utils/formato'

export function RepuestosPage() {
  const [texto, setTexto] = useState('')
  const [categoriaId, setCategoriaId] = useState<string>()
  const [soloBajoStock, setSoloBajoStock] = useState(false)

  const [productoEnEdicion, setProductoEnEdicion] = useState<ProductoResponse | null>(null)
  const [modalProducto, setModalProducto] = useState(false)
  const [productoEnMovimiento, setProductoEnMovimiento] = useState<ProductoResponse | null>(null)
  const [tipoMovimiento, setTipoMovimiento] = useState<TipoMovimiento>('entradas')
  const [modalMovimiento, setModalMovimiento] = useState(false)
  const [categoriaEnEdicion, setCategoriaEnEdicion] = useState<CategoriaProductoResponse | null>(null)
  const [modalCategoria, setModalCategoria] = useState(false)

  // La búsqueda, la categoría y el stock bajo los filtra la API.
  const productos = useProductos({ categoriaId, busqueda: texto, bajoStock: soloBajoStock })
  const categorias = useCategoriasProducto()
  const movimientos = useMovimientos()
  const eliminarProducto = useEliminarProducto()
  const eliminarCategoria = useEliminarCategoria()

  const abrirMovimiento = (producto: ProductoResponse, tipo: TipoMovimiento) => {
    setProductoEnMovimiento(producto)
    setTipoMovimiento(tipo)
    setModalMovimiento(true)
  }

  const columnasProductos: TableProps<ProductoResponse>['columns'] = [
    { title: 'Código', dataIndex: 'codigo', className: 'num' },
    { title: 'Producto', dataIndex: 'nombre', render: (nombre: string) => <strong>{nombre}</strong> },
    { title: 'Categoría', dataIndex: 'categoriaNombre' },
    {
      title: 'Stock',
      dataIndex: 'stockActual',
      align: 'right',
      className: 'num',
      render: (stock: number, producto) => `${entero(stock)} ${producto.unidad}`,
    },
    {
      title: 'Mínimo',
      dataIndex: 'stockMinimo',
      align: 'right',
      className: 'num',
      render: (minimo: number) => entero(minimo),
    },
    {
      title: 'Precio',
      dataIndex: 'precioVenta',
      align: 'right',
      className: 'num',
      render: (precio: number) => soles(precio),
    },
    { title: 'Estado', key: 'estado', render: (_, producto) => <EstadoStock producto={producto} /> },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, producto) => (
        <Space size="small">
          <Button type="link" onClick={() => abrirMovimiento(producto, 'entradas')}>
            Entrada
          </Button>
          <Button type="link" onClick={() => abrirMovimiento(producto, 'salidas')}>
            Salida
          </Button>
          <Button type="link" onClick={() => abrirMovimiento(producto, 'ajustes')}>
            Ajustar
          </Button>
          <Button
            type="link"
            onClick={() => {
              setProductoEnEdicion(producto)
              setModalProducto(true)
            }}
          >
            Editar
          </Button>
          <Popconfirm
            title="Dar de baja el producto"
            okText="Dar de baja"
            cancelText="Cancelar"
            onConfirm={() => eliminarProducto.mutate(producto.id)}
          >
            <Button type="link">Dar de baja</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  const columnasMovimientos: TableProps<MovimientoInventarioResponse>['columns'] = [
    {
      title: 'Fecha',
      dataIndex: 'fechaCreacion',
      className: 'num',
      render: (fecha: string) => fechaHora(fecha),
    },
    {
      title: 'Producto',
      key: 'producto',
      render: (_, movimiento) => `${movimiento.productoCodigo} · ${movimiento.productoNombre}`,
    },
    {
      title: 'Tipo',
      key: 'tipo',
      render: (_, movimiento) => (
        <Tag
          style={{
            marginInlineEnd: 0,
            background: movimiento.tipoId === MOVIMIENTO.salida ? colores.acento100 : colores.neutro100,
            color: movimiento.tipoId === MOVIMIENTO.salida ? colores.acento800 : colores.neutro800,
            borderColor: movimiento.tipoId === MOVIMIENTO.salida ? colores.acento200 : colores.neutro300,
          }}
        >
          {movimiento.tipo}
        </Tag>
      ),
    },
    { title: 'Cantidad', dataIndex: 'cantidad', align: 'right', className: 'num' },
    { title: 'Motivo', dataIndex: 'motivo', render: (motivo: string | null) => motivo ?? '—' },
  ]

  const columnasCategorias: TableProps<CategoriaProductoResponse>['columns'] = [
    { title: 'Categoría', dataIndex: 'nombre', render: (nombre: string) => <strong>{nombre}</strong> },
    { title: 'Productos', dataIndex: 'cantidadProductos', align: 'right', className: 'num' },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, categoria) => (
        <Space size="small">
          <Button
            type="link"
            onClick={() => {
              setCategoriaEnEdicion(categoria)
              setModalCategoria(true)
            }}
          >
            Editar
          </Button>
          <Popconfirm
            title="Eliminar la categoría"
            description="Solo se puede si no tiene productos."
            okText="Eliminar"
            cancelText="Cancelar"
            onConfirm={() => eliminarCategoria.mutate(categoria.id)}
          >
            <Button type="link">Eliminar</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <>
      <BarraSuperior
        titulo="Repuestos e inventario"
        acciones={
          <Button
            type="primary"
            onClick={() => {
              setProductoEnEdicion(null)
              setModalProducto(true)
            }}
          >
            Registrar producto
          </Button>
        }
      />
      <div className="pagina">
        <AvisoError
          error={productos.error ?? movimientos.error ?? eliminarProducto.error ?? eliminarCategoria.error}
        />
        <Tabs
          items={[
            {
              key: 'productos',
              label: 'Productos',
              children: (
                <>
                  <div className="filtros">
                    <Input
                      id="buscar-repuestos"
                      prefix={<SearchOutlined />}
                      placeholder="Buscar por código o nombre"
                      allowClear
                      value={texto}
                      onChange={(evento) => setTexto(evento.target.value)}
                      style={{ width: 340 }}
                    />
                    <Select<string>
                      id="filtro-categoria"
                      allowClear
                      placeholder="Categoría"
                      value={categoriaId}
                      onChange={setCategoriaId}
                      loading={categorias.isPending}
                      options={(categorias.data ?? []).map((categoria) => ({
                        value: categoria.id,
                        label: categoria.nombre,
                      }))}
                      style={{ width: 220 }}
                    />
                    <Checkbox
                      checked={soloBajoStock}
                      onChange={(evento) => setSoloBajoStock(evento.target.checked)}
                    >
                      Solo stock bajo
                    </Checkbox>
                  </div>
                  <Table
                    rowKey="id"
                    columns={columnasProductos}
                    dataSource={productos.data ?? []}
                    pagination={false}
                    loading={productos.isPending}
                    locale={{ emptyText: 'No hay productos que coincidan' }}
                  />
                </>
              ),
            },
            {
              key: 'movimientos',
              label: 'Kardex',
              children: (
                <>
                  <p className="texto-secundario" style={{ marginBottom: 16 }}>
                    Cada entrada, salida y ajuste queda registrado, incluidos los repuestos que consumen las órdenes de
                    servicio y las ventas.
                  </p>
                  <Table
                    rowKey="id"
                    columns={columnasMovimientos}
                    dataSource={movimientos.data ?? []}
                    pagination={{ pageSize: 20 }}
                    loading={movimientos.isPending}
                    locale={{ emptyText: 'Todavía no hay movimientos' }}
                  />
                </>
              ),
            },
            {
              key: 'categorias',
              label: 'Categorías',
              children: (
                <>
                  <div className="filtros">
                    <Button
                      type="primary"
                      onClick={() => {
                        setCategoriaEnEdicion(null)
                        setModalCategoria(true)
                      }}
                    >
                      Nueva categoría
                    </Button>
                  </div>
                  <Table
                    rowKey="id"
                    columns={columnasCategorias}
                    dataSource={categorias.data ?? []}
                    pagination={false}
                    loading={categorias.isPending}
                    locale={{ emptyText: 'Todavía no hay categorías' }}
                  />
                </>
              ),
            },
          ]}
        />
      </div>
      <ModalProducto
        abierto={modalProducto}
        producto={productoEnEdicion}
        onCerrar={() => setModalProducto(false)}
      />
      <ModalMovimientoStock
        abierto={modalMovimiento}
        tipo={tipoMovimiento}
        producto={productoEnMovimiento}
        onCerrar={() => setModalMovimiento(false)}
      />
      <ModalCategoria
        abierto={modalCategoria}
        categoria={categoriaEnEdicion}
        onCerrar={() => setModalCategoria(false)}
      />
    </>
  )
}
