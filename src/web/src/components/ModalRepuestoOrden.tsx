import { Form, InputNumber, Modal, Select } from 'antd'
import { useProductos } from '../api/inventario'
import { useAgregarDetalle } from '../api/ordenes'
import { AvisoError } from './AvisoError'
import { soles } from '../utils/formato'

type Props = {
  abierto: boolean
  ordenId: string
  onCerrar: () => void
}

type Campos = {
  productoId: string
  cantidad: number
}

/**
 * Agrega un repuesto del catálogo a la orden. El precio lo pone el backend
 * desde el producto, y el stock se descuenta ahí mismo: acá no se calcula nada.
 */
export function ModalRepuestoOrden({ abierto, ordenId, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const productos = useProductos()
  const agregar = useAgregarDetalle()

  const productoId = Form.useWatch('productoId', formulario)
  const elegido = (productos.data ?? []).find((producto) => producto.id === productoId)

  const cerrar = () => {
    agregar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    await agregar.mutateAsync({
      id: ordenId,
      datos: {
        productoId: campos.productoId,
        descripcion: null,
        cantidad: campos.cantidad,
        precioUnitario: null,
      },
    })

    cerrar()
  }

  return (
    <Modal
      title="Agregar repuesto"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Agregar"
      cancelText="Cancelar"
      confirmLoading={agregar.isPending}
      destroyOnHidden
    >
      <AvisoError error={agregar.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{ cantidad: 1 }}
      >
        <Form.Item
          label="Repuesto"
          name="productoId"
          rules={[{ required: true, message: 'Elige el repuesto' }]}
        >
          <Select
            showSearch
            optionFilterProp="label"
            loading={productos.isPending}
            placeholder="Buscar por código o nombre"
            options={(productos.data ?? []).map((producto) => ({
              value: producto.id,
              label: `${producto.codigo} · ${producto.nombre} · stock ${producto.stockActual}`,
            }))}
          />
        </Form.Item>
        {elegido && (
          <p className="texto-secundario">
            Precio {soles(elegido.precioVenta)} · stock disponible {elegido.stockActual} {elegido.unidad}
          </p>
        )}
        <Form.Item
          label="Cantidad"
          name="cantidad"
          rules={[
            { required: true, message: 'Indica la cantidad' },
            {
              validator: (_, valor: number) =>
                !elegido || valor <= elegido.stockActual
                  ? Promise.resolve()
                  : Promise.reject(new Error(`Solo hay ${elegido.stockActual} en stock`)),
            },
          ]}
        >
          <InputNumber min={1} style={{ width: '100%' }} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
