import { useState } from 'react'
import { Button, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { TIPO_PRODUCTO, useProductos } from '../api/inventario'
import { ESTADO_MOTO, useIngresarMoto, useMotos } from '../api/motos'
import type { MotoResponse, ProductoResponse } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { AvisoError } from './AvisoError'
import { ModalProducto } from './ModalProducto'
import { entero, fechaHora, soles } from '../utils/formato'
import { textoPrecioRegistrado } from '../utils/precios'

type CamposIngreso = {
  productoId: string
  numeroSerieVIN: string
  numeroMotor?: string
  anio?: number | null
  color?: string
  dua?: string
  itemDua?: string
  costo: number
}

const opcionesEstado = [
  { value: ESTADO_MOTO.enStock, label: 'En stock' },
  { value: ESTADO_MOTO.vendida, label: 'Vendidas' },
]

const texto = (valor?: string) => (valor?.trim() ? valor.trim() : null)

/**
 * Las motos del inventario. Un modelo (YB125, FZ 25…) lleva el precio y el stock; cada
 * moto, su serie. Entran por Compras y salen por Ventas; aquí se ingresan las que ya
 * estaban en la tienda.
 */
export function PanelVehiculos() {
  const { tienePermiso } = useSesion()
  const puedeCrear = tienePermiso(PERMISOS.inventarioCrear)
  const puedeEditar = tienePermiso(PERMISOS.inventarioEditar)
  const puedeIngresar = tienePermiso(PERMISOS.inventarioAjustar)

  const [estado, setEstado] = useState<number | undefined>(ESTADO_MOTO.enStock)
  const [busqueda, setBusqueda] = useState('')
  const [modeloEnEdicion, setModeloEnEdicion] = useState<ProductoResponse | null>(null)
  const [modalModelo, setModalModelo] = useState(false)
  const [ingresoPara, setIngresoPara] = useState<string | null | undefined>(undefined)

  const modelos = useProductos({ tipo: TIPO_PRODUCTO.vehiculo })
  const motos = useMotos({ estado: estado as 0 | 1 | undefined, busqueda: busqueda || undefined })

  const columnasModelos: TableProps<ProductoResponse>['columns'] = [
    { title: 'Código', dataIndex: 'codigo', className: 'num' },
    {
      title: 'Modelo',
      key: 'modelo',
      render: (_, modelo) => (
        <>
          <strong>{modelo.nombre}</strong>
          {modelo.marca && <div className="texto-secundario">{modelo.marca}</div>}
        </>
      ),
    },
    { title: 'Precio', key: 'precio', render: (_, modelo) => textoPrecioRegistrado(modelo) },
    { title: 'En stock', dataIndex: 'stockActual', align: 'right', className: 'num', render: (stock: number) => entero(stock) },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, modelo) => (
        <Space size="small">
          {puedeIngresar && (
            <Button type="link" onClick={() => setIngresoPara(modelo.id)}>
              Ingresar moto
            </Button>
          )}
          {puedeEditar && (
            <Button
              type="link"
              onClick={() => {
                setModeloEnEdicion(modelo)
                setModalModelo(true)
              }}
            >
              Editar
            </Button>
          )}
        </Space>
      ),
    },
  ]

  const columnasMotos: TableProps<MotoResponse>['columns'] = [
    {
      title: 'Serie',
      key: 'serie',
      render: (_, moto) => (
        <>
          <strong className="num">{moto.numeroSerieVIN}</strong>
          {moto.numeroMotor && <div className="texto-secundario">Motor {moto.numeroMotor}</div>}
        </>
      ),
    },
    { title: 'Modelo', dataIndex: 'productoNombre' },
    {
      title: 'Año y color',
      key: 'anio',
      render: (_, moto) => [moto.anio, moto.color].filter(Boolean).join(' · ') || '—',
    },
    {
      title: 'DUA',
      key: 'dua',
      render: (_, moto) => (moto.dua ? `${moto.dua}${moto.itemDua ? ` · ítem ${moto.itemDua}` : ''}` : '—'),
    },
    {
      title: 'Estado',
      key: 'estado',
      render: (_, moto) =>
        moto.estadoId === ESTADO_MOTO.enStock ? (
          <Tag style={{ marginInlineEnd: 0 }}>En stock</Tag>
        ) : (
          <>
            <Tag style={{ marginInlineEnd: 0 }}>Vendida</Tag>
            <div className="texto-secundario">
              {moto.clienteNombre}
              {moto.fechaVenta ? ` · ${fechaHora(moto.fechaVenta)}` : ''}
            </div>
          </>
        ),
    },
    {
      title: 'Ingresó por',
      key: 'origen',
      render: (_, moto) => (
        <>
          {moto.numeroCompra ? `Compra ${moto.numeroCompra}` : 'Inventario inicial'}
          <div className="texto-secundario">Costo {soles(moto.costoUnitario)} sin IGV</div>
        </>
      ),
    },
  ]

  return (
    <>
      <AvisoError error={modelos.error ?? motos.error} />
      <section>
        <div className="seccion-titulo">
          <h2>Modelos</h2>
          {puedeCrear && (
            <Button
              onClick={() => {
                setModeloEnEdicion(null)
                setModalModelo(true)
              }}
            >
              Nuevo modelo
            </Button>
          )}
        </div>
        <Table
          rowKey="id"
          columns={columnasModelos}
          dataSource={modelos.data ?? []}
          pagination={false}
          loading={modelos.isPending}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: 'Todavía no hay modelos de moto: créalos con «Nuevo modelo».' }}
        />
      </section>

      <section style={{ marginTop: 32 }}>
        <div className="seccion-titulo">
          <h2>Motos</h2>
          {puedeIngresar && (modelos.data?.length ?? 0) > 0 && (
            <Button onClick={() => setIngresoPara(null)}>Ingresar moto</Button>
          )}
        </div>
        <div className="filtros">
          <Input.Search
            aria-label="Buscar moto"
            allowClear
            prefix={<SearchOutlined />}
            placeholder="Serie, motor o modelo"
            onSearch={(valor) => setBusqueda(valor.trim())}
            style={{ width: 300 }}
          />
          <Select<number>
            aria-label="Filtrar motos por estado"
            allowClear
            placeholder="Todas"
            value={estado}
            onChange={setEstado}
            options={opcionesEstado}
            style={{ width: 170 }}
          />
        </div>
        <Table
          rowKey="id"
          columns={columnasMotos}
          dataSource={motos.data ?? []}
          pagination={{ pageSize: 20, hideOnSinglePage: true }}
          loading={motos.isPending}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: 'No hay motos con estos filtros. Entran por Compras, con su serie.' }}
        />
        <p className="texto-secundario" style={{ marginTop: 16 }}>
          Cada moto entra por una compra con su serie y sale por una venta. Al venderla se crea la unidad del cliente
          con la misma serie, para que el taller la reconozca. Una cotización no aparta la moto.
        </p>
      </section>

      <ModalProducto abierto={modalModelo} producto={modeloEnEdicion} moto onCerrar={() => setModalModelo(false)} />
      <ModalIngresarMoto
        abierto={ingresoPara !== undefined}
        productoId={ingresoPara ?? undefined}
        modelos={modelos.data ?? []}
        onCerrar={() => setIngresoPara(undefined)}
      />
    </>
  )
}

function ModalIngresarMoto({
  abierto,
  productoId,
  modelos,
  onCerrar,
}: Readonly<{ abierto: boolean; productoId?: string; modelos: ProductoResponse[]; onCerrar: () => void }>) {
  const [formulario] = Form.useForm<CamposIngreso>()
  const ingresar = useIngresarMoto()

  const cerrar = () => {
    ingresar.reset()
    onCerrar()
  }

  const enviar = async (campos: CamposIngreso) => {
    await ingresar.mutateAsync({
      productoId: campos.productoId,
      datos: {
        moto: {
          numeroSerieVIN: campos.numeroSerieVIN.trim(),
          numeroMotor: texto(campos.numeroMotor),
          anio: campos.anio ?? null,
          color: texto(campos.color),
          dua: texto(campos.dua),
          itemDua: texto(campos.itemDua),
        },
        costo: campos.costo,
      },
    })
    cerrar()
  }

  return (
    <Modal
      title="Ingresar moto"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Ingresar"
      cancelText="Cancelar"
      confirmLoading={ingresar.isPending}
      destroyOnHidden
    >
      <p className="texto-secundario">
        Para las motos que ya estaban en la tienda. Las que llegan de un proveedor se registran en Compras.
      </p>
      <AvisoError error={ingresar.error} />
      <Form<CamposIngreso> form={formulario} layout="vertical" requiredMark={false} onFinish={enviar} initialValues={{ productoId }}>
        <Form.Item label="Modelo" name="productoId" rules={[{ required: true, message: 'Elige el modelo' }]}>
          <Select
            showSearch={{ optionFilterProp: 'label' }}
            options={modelos.map((modelo) => ({ value: modelo.id, label: modelo.nombre }))}
          />
        </Form.Item>
        <div className="formulario-grid">
          <Form.Item
            label="Serie (VIN o chasis)"
            name="numeroSerieVIN"
            rules={[{ required: true, whitespace: true, message: 'Escribe la serie' }]}
          >
            <Input maxLength={50} style={{ textTransform: 'uppercase' }} />
          </Form.Item>
          <Form.Item label="Número de motor" name="numeroMotor">
            <Input maxLength={50} style={{ textTransform: 'uppercase' }} />
          </Form.Item>
          <Form.Item label="Año" name="anio">
            <InputNumber min={1950} max={2100} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Color" name="color">
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item label="DUA" name="dua">
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item label="Ítem de la DUA" name="itemDua">
            <Input maxLength={20} />
          </Form.Item>
          <Form.Item
            label="Costo (soles, sin IGV)"
            name="costo"
            rules={[{ required: true, message: 'Escribe el costo' }]}
          >
            <InputNumber min={0} precision={2} style={{ width: '100%' }} />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}
