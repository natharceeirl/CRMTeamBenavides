const numeroConDecimales = new Intl.NumberFormat('es-PE', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const numeroEntero = new Intl.NumberFormat('es-PE')

const numeroConUnDecimal = new Intl.NumberFormat('es-PE', {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
})

const dosDigitos = (valor: number) => String(valor).padStart(2, '0')

export const soles = (monto: number) => `S/ ${numeroConDecimales.format(monto)}`

export const importe = (monto: number) => numeroConDecimales.format(monto)

export const entero = (valor: number) => numeroEntero.format(valor)

/** Un porcentaje que ya viene en escala 0-100, como el margen del reporte de rentabilidad. */
export const porcentaje = (valor: number) => `${numeroConUnDecimal.format(valor)} %`

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

/** Solo el día, para fechas sin hora como la llegada estimada de un pedido: «18/09/2026». */
export const fechaCorta = (iso: string | null | undefined) => {
  if (!iso) return '—'
  const fecha = new Date(iso)
  if (Number.isNaN(fecha.getTime())) return '—'
  return `${dosDigitos(fecha.getDate())}/${dosDigitos(fecha.getMonth() + 1)}/${fecha.getFullYear()}`
}

/**
 * El día local en formato AAAA-MM-DD, para filtros por día y nombres de archivo.
 * No sale de toISOString: en Perú, desde las 19:00 la fecha UTC ya es la del día siguiente.
 */
export const diaLocal = (fecha: Date | string = new Date()) => {
  const valor = new Date(fecha)
  return `${valor.getFullYear()}-${dosDigitos(valor.getMonth() + 1)}-${dosDigitos(valor.getDate())}`
}

/** Como `fechaHora` pero con el año, para documentos impresos: «18/09/2026 08:45». */
export const fechaHoraConAnio = (iso: string | null | undefined) => {
  if (!iso) return '—'
  const fecha = new Date(iso)
  if (Number.isNaN(fecha.getTime())) return '—'
  return `${dosDigitos(fecha.getDate())}/${dosDigitos(fecha.getMonth() + 1)}/${fecha.getFullYear()} ${dosDigitos(fecha.getHours())}:${dosDigitos(fecha.getMinutes())}`
}

/**
 * El correlativo de la API (OT-000123). Si solo se tiene el id, o la orden es
 * anterior al correlativo, se usa el inicio del id.
 */
export const referenciaOrden = (orden: string | { id: string; numeroOrden?: string | null }) => {
  if (typeof orden === 'string') return `#${orden.slice(0, 8).toUpperCase()}`
  return orden.numeroOrden ?? `#${orden.id.slice(0, 8).toUpperCase()}`
}

/**
 * Nombre de un enum del backend en texto legible: «MantenimientoPreventivo» →
 * «Mantenimiento preventivo». Los que llevan tilde van en `conTilde`.
 */
const conTilde: Record<string, string> = {
  MotoAcuatica: 'Moto acuática',
  Diagnostico: 'Diagnóstico',
  ReclamoGarantia: 'Reclamo de garantía',
}

export function nombreDeEnum(valor: string | null | undefined): string {
  if (!valor) return '—'
  if (conTilde[valor]) return conTilde[valor]
  const palabras = valor.replaceAll(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase()
  return palabras.charAt(0).toUpperCase() + palabras.slice(1)
}
