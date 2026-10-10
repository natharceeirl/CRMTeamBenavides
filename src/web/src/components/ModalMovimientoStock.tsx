import { Form, Input, InputNumber, Modal } from 'antd'
import { useMovimientoDeStock } from '../api/inventario'
import type { ProductoResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

export type TipoMovimiento = 'entradas' | 'salidas' | 'ajustes'

type Props = {
  abierto: boolean
  tipo: TipoMovimiento
  producto?: ProductoResponse | null
  onCerrar: () => void
}

type Campos = {
  cantidad?: number
  nuevoStock?: number
  costoUnitario?: number | null
  motivo: string
}

const titulos: Record<TipoMovimiento, string> = {
  entradas: 'Registrar entrada',
  salidas: 'Registrar salida',
  ajustes: 'Ajustar stock',
}

export function ModalMovimientoStock({ abierto, tipo, producto, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const mover = useMovimientoDeStock()

  const esAjuste = tipo === 'ajustes'

  const cerrar = () => {
    mover.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    if (!producto) {
      return
    }

    const costo =
      campos.costoUnitario != null && !Number.isNaN(campos.costoUnitario)
        ? campos.costoUnitario
        : null

    await mover.mutateAsync({
      id: producto.id,
      tipo,
      datos: esAjuste
        ? { nuevoStock: campos.nuevoStock ?? 0, motivo: campos.motivo.trim(), costoUnitario: costo }
        : { cantidad: campos.cantidad ?? 0, motivo: campos.motivo.trim(), costoUnitario: costo },
    })

    cerrar()
  }

  return (
    <Modal
      title={`${titulos[tipo]}: ${producto?.nombre ?? ''}`}
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={mover.isPending}
      destroyOnHidden
    >
      <AvisoError error={mover.error} />
      <p className="texto-secundario">
        Stock actual: {producto?.stockActual ?? 0} {producto?.unidad ?? ''}
      </p>
      {tipo === 'entradas' && (
        <p className="texto-secundario" style={{ marginTop: 8 }}>
          Lo que se compra a un proveedor va en Compras: ahí queda el comprobante, el costo y el pago.
        </p>
      )}
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{
          cantidad: 1,
          nuevoStock: producto?.stockActual ?? 0,
          costoUnitario: producto?.costo ?? undefined,
          motivo: '',
        }}
      >
        {esAjuste ? (
          <Form.Item
            label="Nuevo stock"
            name="nuevoStock"
            rules={[{ required: true, message: 'Indica el stock que quedará' }]}
          >
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        ) : (
          <Form.Item
            label="Cantidad"
            name="cantidad"
            rules={[{ required: true, message: 'Indica la cantidad' }]}
          >
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>
        )}
        {(tipo === 'entradas' || esAjuste) && (
          <Form.Item
            label="Costo unitario"
            name="costoUnitario"
            help="Opcional. Si se deja vacío, tomará el costo registrado del producto."
          >
            <InputNumber
              min={0}
              precision={2}
              style={{ width: '100%' }}
              placeholder={producto?.costo != null ? `S/ ${producto.costo}` : 'S/ 0.00'}
            />
          </Form.Item>
        )}
        <Form.Item
          label="Motivo"
          name="motivo"
          rules={[{ required: true, message: 'El motivo queda en el kardex' }]}
        >
          <Input placeholder="Merma, conteo físico, devolución…" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
