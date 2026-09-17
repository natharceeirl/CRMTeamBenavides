const numeroConDecimales = new Intl.NumberFormat('es-PE', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const numeroEntero = new Intl.NumberFormat('es-PE')

const fechaCorta = new Intl.DateTimeFormat('es-PE', {
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
})

export const soles = (monto: number) => `S/ ${numeroConDecimales.format(monto)}`

export const importe = (monto: number) => numeroConDecimales.format(monto)

export const entero = (valor: number) => numeroEntero.format(valor)

/** Fechas de la API (ISO en UTC) mostradas en la hora local. */
export const fechaHora = (iso: string | null | undefined) =>
  iso ? fechaCorta.format(new Date(iso)) : '—'

/** La API todavía no da un número correlativo de orden; se usa el inicio del id. */
export const referenciaOrden = (id: string) => `#${id.slice(0, 8).toUpperCase()}`
