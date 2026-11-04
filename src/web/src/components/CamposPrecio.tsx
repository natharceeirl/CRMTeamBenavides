import { Checkbox, Form, InputNumber, Select } from 'antd'
import type { MonedaPrecio } from '../api/tipos'
import { MONEDA_PRECIO, OPCIONES_MONEDA, simboloMoneda } from '../utils/precios'

type Props = {
  etiqueta: string
  nombreMonto: string
  nombreMoneda: string
  nombreIncluyeIgv: string
  /** Lo que pasa con un monto en dólares: el precio se cobra al tipo de cambio del día; el costo se convierte al guardarlo. */
  ayudaDolares: string
}

/** Moneda, monto e «Incluye IGV» en una fila, como en el sistema que usa el taller. */
export function CamposPrecio({ etiqueta, nombreMonto, nombreMoneda, nombreIncluyeIgv, ayudaDolares }: Readonly<Props>) {
  const formulario = Form.useFormInstance()
  const moneda = Form.useWatch<MonedaPrecio | undefined>(nombreMoneda, formulario) ?? MONEDA_PRECIO.pen

  return (
    <div className="campos-precio">
      <Form.Item label="Moneda" name={nombreMoneda}>
        <Select options={OPCIONES_MONEDA} />
      </Form.Item>
      <Form.Item
        label={etiqueta}
        name={nombreMonto}
        rules={[{ required: true, message: `Ingresa el ${etiqueta.toLowerCase()}` }]}
        extra={moneda === MONEDA_PRECIO.usd ? ayudaDolares : undefined}
      >
        <InputNumber min={0} precision={2} prefix={simboloMoneda(moneda)} style={{ width: '100%' }} />
      </Form.Item>
      <Form.Item name={nombreIncluyeIgv} valuePropName="checked" label=" ">
        <Checkbox>Incluye IGV</Checkbox>
      </Form.Item>
    </div>
  )
}
