import type { VehiculoResponse } from '../api/tipos'
import { entero } from './formato'

/** Enum TipoUnidad del backend, en el mismo orden. */
export const TIPOS_UNIDAD = [
  { value: 0, label: 'Motocicleta' },
  { value: 1, label: 'Cuatrimoto' },
  { value: 2, label: 'Moto acuática' },
  { value: 3, label: 'Generador' },
  { value: 4, label: 'Otro' },
] as const

/** Enum TipoMedidor del backend: 0 kilómetros, 1 horas. */
export const MEDIDOR_HORAS = 1

export function nombreTipoUnidad(unidad: Pick<VehiculoResponse, 'tipoUnidadId'>): string {
  return TIPOS_UNIDAD.find((tipo) => tipo.value === unidad.tipoUnidadId)?.label ?? 'Unidad'
}

export const midePorHoras = (unidad: Pick<VehiculoResponse, 'tipoMedidorId'>) =>
  unidad.tipoMedidorId === MEDIDOR_HORAS

/** «12 450 km» o «86 h», según el medidor de la unidad. */
export function lecturaMedidor(
  unidad: Pick<VehiculoResponse, 'tipoMedidorId' | 'lecturaMedidorActual' | 'kilometraje' | 'horasUso'>,
): string {
  const lectura = midePorHoras(unidad)
    ? (unidad.lecturaMedidorActual ?? unidad.horasUso)
    : (unidad.lecturaMedidorActual ?? unidad.kilometraje)
  if (lectura == null) return '—'
  return `${entero(lectura)} ${midePorHoras(unidad) ? 'h' : 'km'}`
}

/**
 * La OS no trae el tipo de medidor de su unidad: se deduce por la lectura que
 * tenga y, si no tiene ninguna, por el tipo de unidad.
 */
export function ordenMidePorHoras(orden: {
  kilometrajeIngreso?: number | null
  horasUsoIngreso?: number | null
  lecturaMedidorIngreso?: number | null
  tipoUnidad?: string | null
}): boolean {
  if (orden.kilometrajeIngreso != null) return false
  if (orden.horasUsoIngreso != null) return true
  return orden.tipoUnidad === 'MotoAcuatica' || orden.tipoUnidad === 'Generador'
}

/** Lectura del medidor al ingresar la unidad al taller, o «—». */
export function lecturaIngresoOrden(orden: Parameters<typeof ordenMidePorHoras>[0]): string {
  const enHoras = ordenMidePorHoras(orden)
  // La lectura genérica de la API cubre las órdenes que no traen km ni horas.
  const lectura = enHoras
    ? (orden.horasUsoIngreso ?? orden.lecturaMedidorIngreso)
    : (orden.kilometrajeIngreso ?? orden.lecturaMedidorIngreso)
  return lectura == null ? '—' : `${entero(lectura)} ${enHoras ? 'h' : 'km'}`
}

/** Placa si tiene; si no, VIN o serie. Las motos acuáticas y los generadores no llevan placa. */
export function identificadorUnidad(unidad: Pick<VehiculoResponse, 'placa' | 'numeroSerieVIN'>): string {
  if (unidad.placa) return unidad.placa
  if (unidad.numeroSerieVIN) return `Serie ${unidad.numeroSerieVIN}`
  return 'Sin placa ni serie'
}
