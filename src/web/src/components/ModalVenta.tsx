import { Button, Form, InputNumber, Modal, Radio, Select, Space } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useClientes } from '../api/clientes'
import { useProductos } from '../api/inventario'
import { useCrearVenta } from '../api/ventas'
import { AvisoError } from './AvisoError'
import { soles } from '../utils/formato'

type Props = {
  abierto: boolean
  onCerrar: () => void
}

type Linea = {
  productoId?: string
  cantidad?: number
}

type Campos = {
  clienteId: string
  esCotizacion: boolean
  detalles: Linea[]
}

export function ModalVenta({ abierto, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const clientes = useClientes()
  const productos = useProductos()
  const crear = useCrearVenta()

  const lineas = Form.useWatch('detalles', formulario) ?? []

  // Total solo informativo: el precio de verdad lo pone el backend desde el catálogo.
  const totalEstimado = lineas.reduce((suma, linea) => {
    const producto = (productos.data ?? []).find((item) => item.id === linea?.productoId)
    return suma + (producto ? producto.precioVenta * (linea?.cantidad ?? 0) : 0)
  }, 0)

  const cerrar = () => {
    crear.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    await crear.mutateAsync({
      clienteId: campos.clienteId,
      ordenServicioId: null,
      esCotizacion: campos.esCotizacion,
      detalles: campos.detalles.map((linea) => ({
        productoId: linea.productoId!,
        cantidad: linea.cantidad ?? 1,
      })),
    })

    cerrar()
  }

  return (
    <Modal
      title="Nueva venta o cotización"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={crear.isPending}
      width={720}
      destroyOnHidden
    >
      <AvisoError error={crear.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{ esCotizacion: true, detalles: [{}] }}
      >
        <Form.Item label="Cliente" name="clienteId" rules={[{ required: true, message: 'Elige el cliente' }]}>
          <Select
            showSearch
            optionFilterProp="label"
            loading={clientes.isPending}
            placeholder="Buscar cliente"
            options={(clientes.data ?? []).map((cliente) => ({
              value: cliente.id,
              label: cliente.nombreCompleto,
            }))}
          />
        </Form.Item>

        <Form.Item label="Tipo" name="esCotizacion">
          <Radio.Group
            options={[
              { value: true, label: 'Cotización' },
              { value: false, label: 'Venta directa' },
            ]}
            optionType="button"
          />
        </Form.Item>
        <p className="texto-secundario">
          La cotización no toca el stock. La venta directa lo descuenta al guardarse, igual que al confirmar una
          cotización.
        </p>

        <Form.List name="detalles">
          {(campos, { add, remove }) => (
            <>
              {campos.map((campo) => (
                <Space key={campo.key} align="baseline" style={{ display: 'flex', marginBottom: 8 }}>
                  <Form.Item
                    name={[campo.name, 'productoId']}
                    rules={[{ required: true, message: 'Elige el producto' }]}
                    style={{ width: 420, marginBottom: 0 }}
                  >
                    <Select
                      showSearch
                      optionFilterProp="label"
                      loading={productos.isPending}
                      placeholder="Producto"
                      options={(productos.data ?? []).map((producto) => ({
                        value: producto.id,
                        label: `${producto.codigo} · ${producto.nombre} · ${soles(producto.precioVenta)} · stock ${producto.stockActual}`,
                      }))}
                    />
                  </Form.Item>
                  <Form.Item
                    name={[campo.name, 'cantidad']}
                    initialValue={1}
                    rules={[{ required: true, message: 'Cantidad' }]}
                    style={{ marginBottom: 0 }}
                  >
                    <InputNumber min={1} placeholder="Cant." />
                  </Form.Item>
                  {campos.length > 1 && (
                    <Button type="text" icon={<DeleteOutlined />} onClick={() => remove(campo.name)} />
                  )}
                </Space>
              ))}
              <Button type="dashed" onClick={() => add()} icon={<PlusOutlined />} block>
                Agregar producto
              </Button>
            </>
          )}
        </Form.List>

        <div className="totales" style={{ marginTop: 16 }}>
          <div>
            <div className="etiqueta">Total estimado</div>
            <div className="valor total">{soles(totalEstimado)}</div>
          </div>
        </div>
      </Form>
    </Modal>
  )
}
