import { useEffect } from 'react'
import { Form, Input, InputNumber, Modal, Select } from 'antd'
import { useMetodosPago } from '../api/pagos'
import { useCajaActual } from '../api/caja'
import { useRegistrarPagoCompra } from '../api/compras'
import { MONEDA_COMPRA, type CompraResponse } from '../api/tipos'
import { montoEnMoneda, redondear } from '../utils/compras'
import { soles } from '../utils/formato'
import { AvisoError } from './AvisoError'

type Props = {
  compra: CompraResponse | null
  onCerrar: () => void
}

type Campos = {
  monto: number
  metodoPagoId: string
  referencia?: string
  observaciones?: string
}

/** Pago al proveedor en la moneda de la compra. El efectivo sale de la caja chica abierta. */
export function ModalPagoCompra({ compra, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const abierto = compra !== null
  const metodos = useMetodosPago(abierto)
  const caja = useCajaActual(abierto)
  const pagar = useRegistrarPagoCompra()

  const monto = Form.useWatch('monto', formulario)
  const metodoPagoId = Form.useWatch('metodoPagoId', formulario)
  const esEfectivo = (metodos.data ?? []).find((metodo) => metodo.id === metodoPagoId)?.codigo === 'EFECTIVO'
  const saldo = compra?.saldo ?? 0
  const enDolares = compra?.moneda === MONEDA_COMPRA.usd

  useEffect(() => {
    if (abierto) formulario.setFieldsValue({ monto: saldo })
  }, [abierto, saldo, formulario])

  const cerrar = () => {
    pagar.reset()
    formulario.resetFields()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    if (!compra) return
    await pagar.mutateAsync({
      id: compra.id,
      datos: {
        monto: campos.monto,
        metodoPagoId: campos.metodoPagoId,
        referencia: campos.referencia?.trim() || null,
        observaciones: campos.observaciones?.trim() || null,
      },
    })
    cerrar()
  }

  return (
    <Modal
      title="Registrar pago al proveedor"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Registrar"
      cancelText="Cancelar"
      confirmLoading={pagar.isPending}
      destroyOnHidden
    >
      <AvisoError error={pagar.error ?? metodos.error} />
      {compra && (
        <>
          <p className="texto-secundario" style={{ marginBottom: 16 }}>
            Saldo de {compra.numeroCompra}: <strong>{montoEnMoneda(compra.moneda, saldo)}</strong>
          </p>
          <Form<Campos> form={formulario} layout="vertical" requiredMark={false} onFinish={enviar}>
            <Form.Item
              label={enDolares ? 'Monto (US$)' : 'Monto (S/)'}
              name="monto"
              extra={
                enDolares && monto
                  ? `Equivale a ${soles(redondear(monto * compra.tipoCambio))} con el tipo de cambio de la compra (${compra.tipoCambio}).`
                  : undefined
              }
              rules={[
                { required: true, message: 'Escribe el monto' },
                {
                  validator: async (_, valor: number | undefined) => {
                    if (valor != null && valor > saldo) {
                      throw new Error(`No puede pasar el saldo (${montoEnMoneda(compra.moneda, saldo)}).`)
                    }
                  },
                },
              ]}
            >
              <InputNumber min={0.01} max={saldo} precision={2} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item
              label="Método de pago"
              name="metodoPagoId"
              rules={[{ required: true, message: 'Elige el método' }]}
              extra={
                esEfectivo
                  ? caja.data && !caja.data.tieneCajaAbierta
                    ? 'No hay caja chica abierta: ábrela antes de pagar en efectivo.'
                    : 'Sale como egreso de la caja chica abierta.'
                  : 'Transferencia, Yape o tarjeta no pasan por la caja chica.'
              }
            >
              <Select
                loading={metodos.isPending}
                placeholder="Efectivo, transferencia…"
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
        </>
      )}
    </Modal>
  )
}
