import { useState } from 'react'
import { Button, InputNumber, Space } from 'antd'
import { useConversionDolares } from '../api/configuracion'
import { AvisoError } from './AvisoError'
import { soles } from '../utils/formato'
import { tipoDeCambio } from '../utils/tipoCambio'

/**
 * Convierte un precio en dólares con el tipo de cambio vigente. La API redondea
 * hacia arriba al sol entero; con `onAplicar` el resultado se usa como precio.
 */
export function ConversorDolares({ onAplicar }: Readonly<{ onAplicar?: (montoPen: number) => void }>) {
  const [monto, setMonto] = useState<number | null>(null)
  const [consultado, setConsultado] = useState<number | null>(null)
  const conversion = useConversionDolares(consultado)
  const resultado = conversion.data

  return (
    <div>
      <Space.Compact>
        <InputNumber
          min={0}
          precision={2}
          prefix="US$"
          placeholder="Precio en dólares"
          value={monto}
          onChange={setMonto}
          onPressEnter={() => setConsultado(monto)}
          style={{ width: 200 }}
        />
        <Button onClick={() => setConsultado(monto)} loading={conversion.isFetching}>
          Convertir
        </Button>
      </Space.Compact>
      <AvisoError error={conversion.error} />
      {resultado && consultado === resultado.montoUsd && (
        <p className="texto-secundario" style={{ margin: '8px 0 0' }}>
          {soles(resultado.montoPen)} con tipo de cambio {tipoDeCambio(resultado.tipoCambio)}, redondeado hacia arriba al
          sol.
          {onAplicar && (
            <Button type="link" size="small" onClick={() => onAplicar(resultado.montoPen)}>
              Usar como precio
            </Button>
          )}
        </p>
      )}
    </div>
  )
}
