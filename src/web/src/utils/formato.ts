const numeroConDecimales = new Intl.NumberFormat('es-PE', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const numeroEntero = new Intl.NumberFormat('es-PE')

const dosDigitos = (valor: number) => String(valor).padStart(2, '0')

export const soles = (monto: number) => `S/ ${numeroConDecimales.format(monto)}`

export const importe = (monto: number) => numeroConDecimales.format(monto)

export const entero = (valor: number) => numeroEntero.format(valor)

/**
 * Fechas de la API (ISO en UTC) en hora local, como «18/09 08:45».
 *
 * A mano y no con Intl: el formato local de es-PE sale como «18/9, 8:45 a. m.»,
 * que no es lo que muestra el diseño y además cambia entre equipos.
 */
export const fechaHora = (iso: string | null | undefined) => {
  if (!iso) {
    return '—'
  }

  const fecha = new Date(iso)
  if (Number.isNaN(fecha.getTime())) {
    return '—'
  }

  return `${dosDigitos(fecha.getDate())}/${dosDigitos(fecha.getMonth() + 1)} ${dosDigitos(fecha.getHours())}:${dosDigitos(fecha.getMinutes())}`
}

/** La API todavía no da un número correlativo de orden; se usa el inicio del id. */
export const referenciaOrden = (id: string) => `#${id.slice(0, 8).toUpperCase()}`
