import { Form, Input, InputNumber, Modal } from 'antd'
import { useProductos } from '../api/inventario'
import { useServicios } from '../api/servicios'
import { useActualizarDetalle } from '../api/ordenes'
import { TIPO_ITEM_SERVICIO, type ActualizarDetalleRequest, type DetalleServicioResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { AvisoPrecioGerencia } from './AvisoPrecioGerencia'

type Campos = {
  descripcion: string
  cantidad: number
  precioUnitario: number
}

/**
 * Corrige cantidad, precio o descripción de un ítem de la orden. Solo se envía lo
 * que cambió: así una corrección de cantidad no abre una solicitud de precio.
 */
export function ModalEditarItemOrden({
  ordenId,
  detalle,
  onCerrar,
}: Readonly<{ ordenId: string; detalle: DetalleServicioResponse; onCerrar: () => void }>) {
  const [formulario] = Form.useForm<Campos>()
  const actualizar = useActualizarDetalle()
  const esRepuesto = detalle.tipoItem === TIPO_ITEM_SERVICIO.repuesto || detalle.esRepuesto
  const esServicio = detalle.tipoItem === TIPO_ITEM_SERVICIO.servicio
  const productos = useProductos()
  const servicios = useServicios(true, esServicio)

  const precioDeLista = esRepuesto
    ? (productos.data ?? []).find((producto) => producto.id === detalle.productoId)?.precioVenta
    : esServicio
      ? (servicios.data ?? []).find((servicio) => servicio.id === detalle.servicioId)?.precioSugerido
      : undefined
  const precioUnitario = Form.useWatch('precioUnitario', formulario)
  // Mano de obra y terceros se describen a mano; repuestos y servicios llevan su nombre de catálogo.
  const descripcionEditable = !esRepuesto && !esServicio

  const cerrar = () => {
    actualizar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    const datos: ActualizarDetalleRequest = {}
    if (campos.cantidad !== detalle.cantidad) datos.cantidad = campos.cantidad
    if (Math.abs(campos.precioUnitario - detalle.precioUnitario) > 0.005) datos.precioUnitario = campos.precioUnitario
    if (descripcionEditable && campos.descripcion.trim() !== detalle.descripcion) {
      datos.descripcion = campos.descripcion.trim()
    }

    if (Object.keys(datos).length > 0) {
      await actualizar.mutateAsync({ id: ordenId, detalleId: detalle.id, datos })
    }
    cerrar()
  }

  return (
    <Modal
      title="Editar ítem"
      open
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={actualizar.isPending}
      destroyOnHidden
    >
      <AvisoError error={actualizar.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{
          descripcion: detalle.descripcion,
          cantidad: detalle.cantidad,
          precioUnitario: detalle.precioUnitario,
        }}
      >
        {descripcionEditable ? (
          <Form.Item
            label="Descripción"
            name="descripcion"
            rules={[{ required: true, whitespace: true, message: 'Describe el trabajo o concepto' }]}
          >
            <Input />
          </Form.Item>
        ) : (
          <p style={{ marginTop: 0 }}>{detalle.descripcion}</p>
        )}
        <div className="formulario-grid">
          <Form.Item label="Cantidad" name="cantidad" rules={[{ required: true, message: 'Indica la cantidad' }]}>
            <InputNumber min={1} precision={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            label="Precio unitario (PEN, sin IGV)"
            name="precioUnitario"
            rules={[{ required: true, message: 'Indica el precio' }]}
          >
            <InputNumber min={0} step={0.5} style={{ width: '100%' }} />
          </Form.Item>
        </div>
        <AvisoPrecioGerencia precio={precioUnitario} deLista={precioDeLista} />
      </Form>
    </Modal>
  )
}
