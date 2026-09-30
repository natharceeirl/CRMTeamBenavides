import type { TipoCambioResponse } from '../api/tipos'
import { fechaHora } from './formato'

const cuatroDecimales = new Intl.NumberFormat('es-PE', { minimumFractionDigits: 3, maximumFractionDigits: 4 })

/** «3.750»: el tipo de cambio se lee con tres o cuatro decimales. */
export const tipoDeCambio = (valor: number | null | undefined) => (valor == null ? '—' : cuatroDecimales.format(valor))

/** Si el tipo de cambio vigente se registró hoy (hora local). */
export function esDeHoy(fecha: string | null | undefined, hoy = new Date()): boolean {
  if (!fecha) return false
  const registrada = new Date(fecha)
  return (
    registrada.getFullYear() === hoy.getFullYear() &&
    registrada.getMonth() === hoy.getMonth() &&
    registrada.getDate() === hoy.getDate()
  )
}

/**
 * Texto corto para la barra lateral: «US$ 3.750», y la fecha si no es de hoy,
 * para que nadie cotice con un valor viejo sin darse cuenta.
 */
export function resumenTipoCambio(tipoCambio: TipoCambioResponse | undefined, hoy = new Date()): string | null {
  const valor = tipoCambio?.valorVenta ?? tipoCambio?.tipoCambio
  if (!tipoCambio?.configurado || valor == null) return null
  const texto = `US$ ${tipoDeCambio(valor)}`
  return esDeHoy(tipoCambio.fechaActualizacion, hoy) ? texto : `${texto} · del ${fechaHora(tipoCambio.fechaActualizacion).split(' ')[0]}`
}
