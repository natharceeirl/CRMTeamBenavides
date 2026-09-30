import { useEffect } from 'react'
import { Form, Input, InputNumber, Modal, Select } from 'antd'
import { useMetodosPago } from '../api/pagos'
import type { RegistrarPagoRequest } from '../api/tipos'
import { soles } from '../utils/formato'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  titulo: string
  /** Lo que falta pagar: el monto no puede pasarlo (el backend lo rechaza). */
  saldo: number
  /** Adelanto de una orden que todavía no se liquida. */
  esAnticipo: boolean
  guardando: boolean
  error: unknown
  onRegistrar: (datos: RegistrarPagoRequest) => Promise<unknown>
  onCerrar: () => void
}

type Campos = {
  monto: number
  metodoPagoId: string
  referencia?: string
  observaciones?: string
}

export function ModalRegistrarPago({
  abierto,
  titulo,
  saldo,
  esAnticipo,
  guardando,
  error,
  onRegistrar,
  onCerrar,
}: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const metodos = useMetodosPago(abierto)

  // Por defecto se cobra todo el saldo; un adelanto suele ser parcial y se escribe.
  useEffect(() => {
    if (abierto) {
      formulario.setFieldsValue({ monto: esAnticipo ? undefined : saldo })
    }
  }, [abierto, esAnticipo, saldo, formulario])

  const enviar = async (campos: Campos) => {
    await onRegistrar({
      monto: campos.monto,
      metodoPagoId: campos.metodoPagoId,
      referencia: campos.referencia?.trim() || null,
      esAnticipo,
      observaciones: campos.observaciones?.trim() || null,
    })
    formulario.resetFields()
    onCerrar()
  }

  return (
    <Modal
      title={titulo}
      open={abierto}
      onCancel={onCerrar}
      onOk={() => formulario.submit()}
      okText="Registrar"
      cancelText="Cancelar"
      confirmLoading={guardando}
      destroyOnHidden
    >
      <AvisoError error={error ?? metodos.error} />
      <p className="texto-secundario" style={{ marginBottom: 16 }}>
        Saldo pendiente: <strong>{soles(saldo)}</strong>
      </p>
      <Form<Campos> form={formulario} layout="vertical" requiredMark={false} onFinish={enviar}>
        <Form.Item
          label="Monto (S/)"
          name="monto"
          rules={[
            { required: true, message: 'Escribe el monto' },
            {
              validator: async (_, monto: number | undefined) => {
                if (monto != null && monto > saldo) {
                  throw new Error(`No puede pasar el saldo pendiente (${soles(saldo)}).`)
                }
              },
            },
          ]}
        >
          <InputNumber min={0.01} max={saldo} precision={2} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="Método de pago" name="metodoPagoId" rules={[{ required: true, message: 'Elige el método' }]}>
          <Select
            loading={metodos.isPending}
            placeholder="Efectivo, tarjeta, Yape/Plin…"
            options={(metodos.data ?? [])
              .filter((metodo) => metodo.activo)
              .map((metodo) => ({ value: metodo.id, label: metodo.nombre }))}
          />
        </Form.Item>
        <Form.Item label="Referencia" name="referencia">
          <Input placeholder="N.° de operación o voucher (opcional)" maxLength={100} />
        </Form.Item>
        <Form.Item label="Observaciones" name="observaciones">
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
