const numeroConDecimales = new Intl.NumberFormat('es-PE', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const numeroEntero = new Intl.NumberFormat('es-PE')

export const soles = (monto: number) => `S/ ${numeroConDecimales.format(monto)}`

export const importe = (monto: number) => numeroConDecimales.format(monto)

export const entero = (valor: number) => numeroEntero.format(valor)
