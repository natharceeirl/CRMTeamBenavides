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

type LecturaDeUnidad = Pick<VehiculoResponse, 'tipoMedidorId' | 'lecturaMedidorActual' | 'kilometraje' | 'horasUso'>

/** Lectura actual del medidor de la unidad, en km u horas según su medidor. */
export function lecturaActualUnidad(unidad: LecturaDeUnidad): number | null {
  return (
    (midePorHoras(unidad)
      ? (unidad.lecturaMedidorActual ?? unidad.horasUso)
      : (unidad.lecturaMedidorActual ?? unidad.kilometraje)) ?? null
  )
}

/** «12 450 km» o «86 h». */
export const textoLectura = (lectura: number, enHoras: boolean) => `${entero(lectura)} ${enHoras ? 'h' : 'km'}`

/** «12 450 km» o «86 h», según el medidor de la unidad. */
export function lecturaMedidor(unidad: LecturaDeUnidad): string {
  const lectura = lecturaActualUnidad(unidad)
  return lectura == null ? '—' : textoLectura(lectura, midePorHoras(unidad))
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

type LecturaDeOrden = Parameters<typeof ordenMidePorHoras>[0]

/** Lectura con que ingresó la unidad, en km u horas. */
function lecturaDeOrden(orden: LecturaDeOrden, enHoras: boolean): number | null {
  const propia = enHoras ? orden.horasUsoIngreso : orden.kilometrajeIngreso
  // La lectura genérica de la API cubre las órdenes que no traen km ni horas.
  return propia ?? orden.lecturaMedidorIngreso ?? null
}

/** Lectura del medidor al ingresar la unidad al taller, o «—». */
export function lecturaIngresoOrden(orden: LecturaDeOrden): string {
  const enHoras = ordenMidePorHoras(orden)
  const lectura = lecturaDeOrden(orden, enHoras)
  return lectura == null ? '—' : textoLectura(lectura, enHoras)
}

/**
 * La mayor lectura ya registrada para la unidad: la suya y la de sus órdenes
 * anteriores. Un medidor no retrocede, así que una lectura nueva no puede ser
 * menor.
 */
export function ultimaLecturaRegistrada(
  enHoras: boolean,
  lecturaUnidad: number | null,
  ordenesAnteriores: LecturaDeOrden[],
): number | null {
  const lecturas = [lecturaUnidad, ...ordenesAnteriores.map((orden) => lecturaDeOrden(orden, enHoras))].filter(
    (lectura): lectura is number => lectura != null,
  )
  return lecturas.length === 0 ? null : Math.max(...lecturas)
}

/** Placa si tiene; si no, VIN o serie. Las motos acuáticas y los generadores no llevan placa. */
export function identificadorUnidad(unidad: Pick<VehiculoResponse, 'placa' | 'numeroSerieVIN'>): string {
  if (unidad.placa) return unidad.placa
  if (unidad.numeroSerieVIN) return `Serie ${unidad.numeroSerieVIN}`
  return 'Sin placa ni serie'
}
