import { TIPO_DOCUMENTO_CLIENTE, type TipoDocumentoCliente, type TipoMedidor } from '../api/tipos'
import { esEstadoTerminal } from '../api/ordenes'
import { MEDIDOR_HORAS } from './unidades'

/** Enum TipoUnidad del backend: moto acuática y generador miden horas de uso. */
const TIPO_UNIDAD_MOTO_ACUATICA = 2
const TIPO_UNIDAD_GENERADOR = 3

/** El medidor que se propone según el tipo de unidad; se puede cambiar. */
export function medidorPorTipoUnidad(tipoUnidad: number): TipoMedidor {
  return tipoUnidad === TIPO_UNIDAD_MOTO_ACUATICA || tipoUnidad === TIPO_UNIDAD_GENERADOR ? MEDIDOR_HORAS : 0
}

/** DNI si tiene 8 dígitos, RUC si tiene 11; lo demás (carné de extranjería, pasaporte) va como «Otro». */
export function tipoDocumentoSugerido(numero: string): TipoDocumentoCliente {
  const limpio = numero.trim()
  if (/^\d{8}$/.test(limpio)) return TIPO_DOCUMENTO_CLIENTE.dni
  if (/^\d{11}$/.test(limpio)) return TIPO_DOCUMENTO_CLIENTE.ruc
  return TIPO_DOCUMENTO_CLIENTE.otro
}

/** El mismo control que hace el backend: DNI de 8 dígitos y RUC de 11. */
export function motivoDocumentoInvalido(tipoDocumento: number, numero: string): string | null {
  const limpio = numero.trim()
  if (tipoDocumento === TIPO_DOCUMENTO_CLIENTE.dni && !/^\d{8}$/.test(limpio)) return 'El DNI tiene 8 dígitos.'
  if (tipoDocumento === TIPO_DOCUMENTO_CLIENTE.ruc && !/^\d{11}$/.test(limpio)) return 'El RUC tiene 11 dígitos.'
  if (limpio.length > 20) return 'El documento no puede pasar de 20 caracteres.'
  return null
}

/**
 * La orden de la unidad que sigue abierta (ni entregada ni cancelada). Una
 * unidad tiene una sola a la vez: el backend no deja abrir otra.
 */
export function ordenAbiertaDeUnidad<T extends { estadoId: number }>(ordenes: T[] | undefined): T | null {
  return (ordenes ?? []).find((orden) => !esEstadoTerminal(orden.estadoId)) ?? null
}
