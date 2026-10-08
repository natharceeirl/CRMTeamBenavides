import type { Dayjs } from 'dayjs'
import dayjs from 'dayjs'
import { Button, DatePicker, Form, Input, InputNumber, Modal, Select, Space } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useClientes } from '../api/clientes'
import { useProductos } from '../api/inventario'
import { useCrearPedidoLima } from '../api/pedidosLima'
import type { PedidoLimaResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { soles } from '../utils/formato'

type Props = {
  abierto: boolean
  onCerrar: () => void
  /** Recibe el pedido creado, para abrir su detalle. */
  onCreado?: (pedido: PedidoLimaResponse) => void
}

type Linea = {
  productoId?: string
  cantidad?: number
}

type Campos = {
  clienteId: string
  detalles: Linea[]
  empresaTransporte?: string
  numeroGuia?: string
  fechaEstimadaLlegada?: Dayjs | null
  observaciones?: string
}

const textoOpcional = (valor: string | undefined) => valor?.trim() || null

export function ModalPedidoLima({ abierto, onCerrar, onCreado }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const clientes = useClientes()
  const productos = useProductos()
  const crear = useCrearPedidoLima()

  const lineas = Form.useWatch('detalles', formulario) ?? []

  // Solo informativo: el precio de catálogo y el IGV los pone el backend.
  const totalEstimado = lineas.reduce((suma, linea) => {
    const producto = (productos.data ?? []).find((item) => item.id === linea?.productoId)
    return suma + (producto ? producto.precioVenta * (linea?.cantidad ?? 0) : 0)
  }, 0)

  const cerrar = () => {
    crear.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    const pedido = await crear.mutateAsync({
      clienteId: campos.clienteId,
      empresaTransporte: textoOpcional(campos.empresaTransporte),
      numeroGuia: textoOpcional(campos.numeroGuia),
      fechaEstimadaLlegada: campos.fechaEstimadaLlegada?.startOf('day').toISOString() ?? null,
      observaciones: textoOpcional(campos.observaciones),
      detalles: campos.detalles.map((linea) => ({
        productoId: linea.productoId!,
        cantidad: linea.cantidad ?? 1,
      })),
    })

    cerrar()
    onCreado?.(pedido)
  }

  return (
    <Modal
      title="Nuevo pedido a Lima"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Registrar pedido"
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
        initialValues={{ detalles: [{}] }}
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

        <Form.List name="detalles">
          {(campos, { add, remove }) => (
            <>
              {campos.map((campo) => (
                <Space key={campo.key} align="baseline" className="linea-detalle" style={{ display: 'flex', marginBottom: 8 }}>
                  <Form.Item
                    name={[campo.name, 'productoId']}
                    rules={[{ required: true, message: 'Elige el repuesto' }]}
                    style={{ width: 420, marginBottom: 0 }}
                  >
                    <Select
                      showSearch
                      optionFilterProp="label"
                      loading={productos.isPending}
                      placeholder="Repuesto"
                      options={(productos.data ?? []).map((producto) => ({
                        value: producto.id,
                        label: `${producto.codigo} · ${producto.nombre} · ${soles(producto.precioVenta)}`,
                      }))}
                    />
                  </Form.Item>
                  <Form.Item
                    name={[campo.name, 'cantidad']}
                    initialValue={1}
                    rules={[{ required: true, message: 'Cantidad' }]}
                    style={{ marginBottom: 0 }}
                  >
                    <InputNumber min={1} precision={0} placeholder="Cant." />
                  </Form.Item>
                  {campos.length > 1 && (
                    <Button
                      type="text"
                      aria-label="Quitar repuesto"
                      icon={<DeleteOutlined />}
                      onClick={() => remove(campo.name)}
                    />
                  )}
                </Space>
              ))}
              <Button type="dashed" onClick={() => add()} icon={<PlusOutlined />} block>
                Agregar repuesto
              </Button>
            </>
          )}
        </Form.List>

        <div className="formulario-grid" style={{ marginTop: 16 }}>
          <Form.Item label="Empresa de transporte" name="empresaTransporte">
            <Input maxLength={100} placeholder="Ej. Shalom" />
          </Form.Item>
          <Form.Item label="Número de guía" name="numeroGuia">
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item label="Llegada estimada" name="fechaEstimadaLlegada">
            <DatePicker
              format="DD/MM/YYYY"
              disabledDate={(dia) => dia.isBefore(dayjs(), 'day')}
              style={{ width: '100%' }}
            />
          </Form.Item>
          <Form.Item label="Observaciones" name="observaciones" className="ancho-completo">
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
        </div>

        <div className="totales">
          <div>
            <div className="etiqueta">Subtotal estimado, sin IGV</div>
            <div className="valor total">{soles(totalEstimado)}</div>
          </div>
        </div>
      </Form>
    </Modal>
  )
}
