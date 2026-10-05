import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { avisoSegun, type AvisoExito } from './avisos'
import {
  ESTADO_CITA,
  type EstadoCita,
  type CitaDetalleResponse,
  type ActualizarCitaRequest,
  type CitaListResponse,
  type CrearCitaRequest,
  type ReprogramarCitaRequest,
} from './tipos'

export const nombresEstadoCita: Record<EstadoCita, string> = {
  [ESTADO_CITA.pendiente]: 'Pendiente',
  [ESTADO_CITA.confirmada]: 'Confirmada',
  [ESTADO_CITA.enTaller]: 'En taller',
  [ESTADO_CITA.completada]: 'Completada',
  [ESTADO_CITA.cancelada]: 'Cancelada',
  [ESTADO_CITA.noAsistio]: 'No asistió',
}

export const esCitaFinal = (estado: EstadoCita) =>
  estado === ESTADO_CITA.completada || estado === ESTADO_CITA.cancelada || estado === ESTADO_CITA.noAsistio

/**
 * A qué estados puede pasar una cita, con las reglas de CitaService: no se
 * retrocede desde «En taller» y una pendiente no se completa sin pasar por el
 * taller. La cancelación va aparte porque pide motivo.
 */
export function siguientesEstadosCita(estado: EstadoCita): EstadoCita[] {
  switch (estado) {
    case ESTADO_CITA.pendiente:
      return [ESTADO_CITA.confirmada, ESTADO_CITA.enTaller, ESTADO_CITA.noAsistio]
    case ESTADO_CITA.confirmada:
      return [ESTADO_CITA.enTaller, ESTADO_CITA.noAsistio]
    case ESTADO_CITA.enTaller:
      return [ESTADO_CITA.completada]
    default:
      return []
  }
}

/** Se puede cancelar o reprogramar mientras la unidad no esté en el taller. */
export const citaAntesDelTaller = (estado: EstadoCita) =>
  estado === ESTADO_CITA.pendiente || estado === ESTADO_CITA.confirmada

export type FiltrosCitas = {
  fechaInicio?: string
  fechaFin?: string
  estado?: EstadoCita
  clienteId?: string
}

export function rutaCitas(filtros: FiltrosCitas, base = '/citas'): string {
  const parametros = new URLSearchParams()
  if (filtros.fechaInicio) parametros.set('fechaInicio', filtros.fechaInicio)
  if (filtros.fechaFin) parametros.set('fechaFin', filtros.fechaFin)
  if (filtros.estado) parametros.set('estado', filtros.estado)
  if (filtros.clienteId) parametros.set('clienteId', filtros.clienteId)
  const consulta = parametros.toString()
  return consulta ? `${base}?${consulta}` : base
}

const claves = {
  todas: ['citas'] as const,
  lista: (filtros: FiltrosCitas) => ['citas', 'lista', filtros] as const,
  una: (id: string) => ['citas', id] as const,
}

export function useCitas(filtros: FiltrosCitas) {
  return useQuery({ queryKey: claves.lista(filtros), queryFn: () => solicitar<CitaListResponse[]>(rutaCitas(filtros)) })
}

export function useCita(id: string | null) {
  return useQuery({
    queryKey: claves.una(id ?? ''),
    queryFn: () => solicitar<CitaDetalleResponse>(`/citas/${id}`),
    enabled: Boolean(id),
  })
}

function useMutacionCita<V>(mutationFn: (variables: V) => Promise<CitaDetalleResponse>, exito: AvisoExito) {
  const consultas = useQueryClient()
  return useMutation({
    meta: { exito },
    mutationFn,
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: claves.todas })
    },
  })
}

export const useCrearCita = () =>
  useMutacionCita(
    (datos: CrearCitaRequest) => solicitar<CitaDetalleResponse>('/citas', { metodo: 'POST', cuerpo: datos }),
    'Cita agendada',
  )

export const useActualizarCita = () =>
  useMutacionCita(
    ({ id, datos }: { id: string; datos: ActualizarCitaRequest }) =>
      solicitar<CitaDetalleResponse>(`/citas/${id}`, { metodo: 'PUT', cuerpo: datos }),
    'Cita actualizada',
  )

/** La orden que se abrió al recibir la unidad: la cita queda «En taller» con ella. */
export const useVincularOrdenCita = () =>
  useMutacionCita(
    ({ id, ordenServicioId }: { id: string; ordenServicioId: string }) =>
      solicitar<CitaDetalleResponse>(`/citas/${id}/orden-servicio`, { metodo: 'PUT', cuerpo: { ordenServicioId } }),
    'Cita vinculada a la orden',
  )

export const useReprogramarCita = () =>
  useMutacionCita(
    ({ id, datos }: { id: string; datos: ReprogramarCitaRequest }) =>
      solicitar<CitaDetalleResponse>(`/citas/${id}/reprogramar`, { metodo: 'PUT', cuerpo: datos }),
    'Cita reprogramada',
  )

export const useCambiarEstadoCita = () =>
  useMutacionCita(
    ({ id, nuevoEstado, observacion }: { id: string; nuevoEstado: EstadoCita; observacion: string | null }) =>
      solicitar<CitaDetalleResponse>(`/citas/${id}/estado`, { metodo: 'PUT', cuerpo: { nuevoEstado, observacion } }),
    avisoSegun<{ nuevoEstado: EstadoCita }>(({ nuevoEstado }) => `Cita: ${nombresEstadoCita[nuevoEstado].toLowerCase()}`),
  )

export const useCancelarCita = () =>
  useMutacionCita(
    ({ id, motivo }: { id: string; motivo: string }) =>
      solicitar<CitaDetalleResponse>(`/citas/${id}/cancelar`, { metodo: 'PUT', cuerpo: { motivoCancelacion: motivo } }),
    'Cita cancelada',
  )
