import { MONEDA_COMPRA, TIPO_AFECTACION_IGV, esAfectacionGratuita, type MonedaCompra } from '../api/tipos'
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

/** `valorReferencial`: lo que se habría pagado por una línea gratuita, con IGV. */
export type MontosLinea = { subtotal: number; igv: number; total: number; valorReferencial?: number }

export type TotalesCompra = {
  gravado: number
  exonerado: number
  inafecto: number
  /** Lo que valen las líneas gratuitas (bonificaciones). No se suma al total. */
  gratuito: number
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
  // Una bonificación del proveedor no se paga: vale lo que costaría como gravada.
  if (esAfectacionGratuita(linea.tipoAfectacionIgv)) {
    const comoGravada = calcularLinea({ ...linea, tipoAfectacionIgv: TIPO_AFECTACION_IGV.gravado }, porcentajeIgv, preciosIncluyenIgv)
    return { subtotal: 0, igv: 0, total: 0, valorReferencial: comoGravada.total }
  }

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
  const totales: TotalesCompra = { gravado: 0, exonerado: 0, inafecto: 0, gratuito: 0, igv: 0, total: 0 }

  for (const linea of lineas) {
    const montos = calcularLinea(linea, porcentajeIgv, preciosIncluyenIgv)
    if (esAfectacionGratuita(linea.tipoAfectacionIgv)) totales.gratuito += montos.valorReferencial ?? 0
    else if (linea.tipoAfectacionIgv === TIPO_AFECTACION_IGV.exonerado) totales.exonerado += montos.subtotal
    else if (linea.tipoAfectacionIgv === TIPO_AFECTACION_IGV.inafecto) totales.inafecto += montos.subtotal
    else totales.gravado += montos.subtotal
    totales.igv += montos.igv
    totales.total += montos.total
  }

  return {
    gravado: redondear(totales.gravado),
    exonerado: redondear(totales.exonerado),
    inafecto: redondear(totales.inafecto),
    gratuito: redondear(totales.gratuito),
    igv: redondear(totales.igv),
    total: redondear(totales.total),
  }
}

/** «S/ 1 234.50» o «US$ 1 234.50», según la moneda de la compra. */
export const montoEnMoneda = (moneda: MonedaCompra, monto: number) =>
  `${moneda === MONEDA_COMPRA.usd ? 'US$' : 'S/'} ${importe(monto)}`

/** «F001-123». */
export const numeroComprobante = (compra: { serie: string; numero: string }) => `${compra.serie}-${compra.numero}`
