import type { MonedaPrecio, ProductoResponse } from '../api/tipos'
import { importe, soles } from './formato'

/** La moneda en que se registra el precio de un repuesto (enum MonedaPrecio de la API). */
export const MONEDA_PRECIO = { pen: 0, usd: 1 } as const satisfies Record<string, MonedaPrecio>

export const OPCIONES_MONEDA = [
  { value: MONEDA_PRECIO.pen, label: 'Soles' },
  { value: MONEDA_PRECIO.usd, label: 'Dólares' },
]

export const simboloMoneda = (moneda: MonedaPrecio) => (moneda === MONEDA_PRECIO.usd ? 'US$' : 'S/')

type PrecioRegistrado = Pick<ProductoResponse, 'precioVenta' | 'monedaPrecio' | 'precioIncluyeIgv'>

/** «US$ 25,00» o «S/ 35,00», como se registró, con «+ IGV» si el precio no lo incluye. */
export function textoPrecioRegistrado(producto: PrecioRegistrado) {
  const monto = `${simboloMoneda(producto.monedaPrecio)} ${importe(producto.precioVenta)}`
  return producto.precioIncluyeIgv ? monto : `${monto} + IGV`
}

/**
 * El precio de lista en soles de hoy, en los términos en que se escribe una línea: con
 * IGV si el repuesto lo incluye. Null si está en dólares y no hay tipo de cambio.
 */
export const precioListaSoles = (producto?: Pick<ProductoResponse, 'precioListaSoles'> | null) =>
  producto?.precioListaSoles ?? null

/** «S/ 94,00», o «S/ 29,66 + IGV»; «sin tipo de cambio» si está en dólares y falta registrarlo. */
export function textoPrecioLista(producto: Pick<ProductoResponse, 'precioListaSoles' | 'precioIncluyeIgv'>) {
  const lista = precioListaSoles(producto)
  if (lista == null) return 'sin tipo de cambio'
  return producto.precioIncluyeIgv ? soles(lista) : `${soles(lista)} + IGV`
}

/** El total de una línea con el precio escrito en los términos del repuesto. */
export const totalDeLinea = (precio: number, cantidad: number, incluyeIgv: boolean, porcentajeIgv = 18) =>
  incluyeIgv ? precio * cantidad : precio * cantidad * (1 + porcentajeIgv / 100)

/** El precio que se muestra y se edita en una línea: con IGV si se cobró así. */
export const precioDeLinea = (linea: { precioUnitario: number; precioUnitarioConIgv?: number | null }) =>
  linea.precioUnitarioConIgv ?? linea.precioUnitario
