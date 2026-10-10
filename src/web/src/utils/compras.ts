import { MONEDA_COMPRA, TIPO_AFECTACION_IGV, type MonedaCompra } from '../api/tipos'
import { importe } from './formato'

/**
 * Vista previa de los montos de una compra mientras se escribe. Repite
 * CalculoCompra del backend (Services/CalculoCompra.cs); los montos que valen
 * son los que devuelve la API al registrarla.
 */

export type LineaParaCalcular = {
  cantidad: number
  precioUnitario: number
  tipoAfectacionIgv: number
}

export type MontosLinea = { subtotal: number; igv: number; total: number }

export type TotalesCompra = {
  gravado: number
  exonerado: number
  inafecto: number
  igv: number
  total: number
}

/**
 * A dos decimales, la mitad hacia arriba como en el backend. Se corre la coma
 * con notación exponencial porque 1.005 * 100 da 100.49999… en coma flotante.
 */
export function redondear(monto: number): number {
  if (!Number.isFinite(monto)) return 0
  const signo = monto < 0 ? -1 : 1
  const absoluto = Math.abs(monto)
  if (String(absoluto).includes('e')) return (signo * Math.round(absoluto * 100)) / 100
  return signo * Number(`${Math.round(Number(`${absoluto}e2`))}e-2`)
}

export function calcularLinea(
  linea: LineaParaCalcular,
  porcentajeIgv: number,
  preciosIncluyenIgv: boolean,
): MontosLinea {
  const importeLinea = redondear((linea.cantidad || 0) * (linea.precioUnitario || 0))

  if (linea.tipoAfectacionIgv !== TIPO_AFECTACION_IGV.gravado || porcentajeIgv <= 0) {
    return { subtotal: importeLinea, igv: 0, total: importeLinea }
  }

  if (preciosIncluyenIgv) {
    const subtotal = redondear(importeLinea / (1 + porcentajeIgv / 100))
    return { subtotal, igv: redondear(importeLinea - subtotal), total: importeLinea }
  }

  const igv = redondear((importeLinea * porcentajeIgv) / 100)
  return { subtotal: importeLinea, igv, total: redondear(importeLinea + igv) }
}

export function calcularTotales(
  lineas: LineaParaCalcular[],
  porcentajeIgv: number,
  preciosIncluyenIgv: boolean,
): TotalesCompra {
  const totales: TotalesCompra = { gravado: 0, exonerado: 0, inafecto: 0, igv: 0, total: 0 }

  for (const linea of lineas) {
    const montos = calcularLinea(linea, porcentajeIgv, preciosIncluyenIgv)
    if (linea.tipoAfectacionIgv === TIPO_AFECTACION_IGV.exonerado) totales.exonerado += montos.subtotal
    else if (linea.tipoAfectacionIgv === TIPO_AFECTACION_IGV.inafecto) totales.inafecto += montos.subtotal
    else totales.gravado += montos.subtotal
    totales.igv += montos.igv
    totales.total += montos.total
  }

  return {
    gravado: redondear(totales.gravado),
    exonerado: redondear(totales.exonerado),
    inafecto: redondear(totales.inafecto),
    igv: redondear(totales.igv),
    total: redondear(totales.total),
  }
}

/** «S/ 1 234.50» o «US$ 1 234.50», según la moneda de la compra. */
export const montoEnMoneda = (moneda: MonedaCompra, monto: number) =>
  `${moneda === MONEDA_COMPRA.usd ? 'US$' : 'S/'} ${importe(monto)}`

/** «F001-123». */
export const numeroComprobante = (compra: { serie: string; numero: string }) => `${compra.serie}-${compra.numero}`
