import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type {
  ActualizarOrdenRequest,
  AgregarDetalleRequest,
  AperturaOrdenRequest,
  CambiarEstadoRequest,
  DetalleServicioResponse,
  DiagnosticoRequest,
  OrdenServicioDetalleResponse,
  OrdenServicioResponse,
} from './tipos'

/**
 * Estados del backend (enum EstadoOrdenServicio). Se mandan como número porque
 * la API no tiene conversor de enums a texto; en las respuestas vienen los dos:
 * `estado` con el nombre y `estadoId` con el número.
 *
 * El cliente decidió mantener estos siete estados (resumen de cambios del 25/09).
 */
export const ESTADO = {
  abierta: 0,
  diagnostico: 1,
  aprobada: 2,
  enProceso: 3,
  lista: 4,
  entregada: 5,
  cancelada: 6,
} as const

export const nombresEstado: Record<number, string> = {
  0: 'Abierta',
  1: 'Diagnóstico',
  2: 'Aprobada',
  3: 'En proceso',
  4: 'Lista',
  5: 'Entregada',
  6: 'Cancelada',
}

/** Misma matriz que valida el backend en OrdenServicioService.CambiarEstadoAsync. */
export const transicionesValidas: Record<number, number[]> = {
  0: [1, 6],
  1: [2, 6],
  2: [3, 6],
  3: [4, 6],
  4: [5, 3, 6],
  5: [],
  6: [],
}

/**
 * El técnico no hace la aprobación final, la entrega ni la anulación: el backend
 * lo rechaza en OrdenServicioService.CambiarEstadoAsync.
 */
export const estadosVedadosAlTecnico: readonly number[] = [ESTADO.aprobada, ESTADO.entregada, ESTADO.cancelada]

export const esEstadoTerminal = (estadoId: number) =>
  estadoId === ESTADO.entregada || estadoId === ESTADO.cancelada

/** La orden se puede editar (diagnóstico y detalles) salvo en Lista o terminal. */
export const permiteEditarDetalles = (estadoId: number) =>
  !esEstadoTerminal(estadoId) && estadoId !== ESTADO.lista

/** Enums TipoAtencion, ModalidadAtencion y TipoFalla del backend, en el mismo orden. */
export const TIPOS_ATENCION = [
  { value: 0, label: 'Mantenimiento preventivo' },
  { value: 1, label: 'Mantenimiento correctivo' },
  { value: 2, label: 'Reclamo de garantía' },
  { value: 3, label: 'Gratuito' },
]

export const MODALIDADES_ATENCION = [
  { value: 0, label: 'En taller' },
  { value: 1, label: 'En sitio' },
]

export const TIPOS_FALLA = [
  { value: 0, label: 'Menor' },
  { value: 1, label: 'Mayor' },
]

export const etiquetaDe = (opciones: { value: number; label: string }[], valor: number | null | undefined) =>
  opciones.find((opcion) => opcion.value === valor)?.label ?? '—'

/** Filtros que acepta GET /api/ordenes-servicio. La búsqueda y las fechas las resuelve la API. */
export type FiltrosOrdenes = {
  estado?: number
  vehiculoId?: string
  clienteId?: string
  tecnicoId?: string
  busqueda?: string
  fechaDesde?: string
  fechaHasta?: string
}

export const clavesOrdenes = {
  todas: ['ordenes'] as const,
  lista: (filtros: FiltrosOrdenes) => ['ordenes', 'lista', filtros] as const,
  una: (id: string) => ['ordenes', id] as const,
}

/** `habilitado` en falso evita pedir órdenes a quien no puede verlas y recibiría 403. */
export function useOrdenes(filtros: FiltrosOrdenes = {}, habilitado = true) {
  return useQuery({
    queryKey: clavesOrdenes.lista(filtros),
    queryFn: () => {
      const parametros = new URLSearchParams()
      if (filtros.estado !== undefined) parametros.set('estado', String(filtros.estado))
      if (filtros.vehiculoId) parametros.set('vehiculoId', filtros.vehiculoId)
      if (filtros.clienteId) parametros.set('clienteId', filtros.clienteId)
      if (filtros.tecnicoId) parametros.set('tecnicoId', filtros.tecnicoId)
      if (filtros.busqueda?.trim()) parametros.set('busqueda', filtros.busqueda.trim())
      if (filtros.fechaDesde) parametros.set('fechaDesde', filtros.fechaDesde)
      if (filtros.fechaHasta) parametros.set('fechaHasta', filtros.fechaHasta)
      const consulta = parametros.toString()
      return solicitar<OrdenServicioResponse[]>(
        `/ordenes-servicio${consulta ? `?${consulta}` : ''}`,
      )
    },
    enabled: habilitado,
    placeholderData: (anterior) => anterior,
  })
}

export function useOrden(id: string | undefined) {
  return useQuery({
    queryKey: clavesOrdenes.una(id ?? ''),
    queryFn: () => solicitar<OrdenServicioDetalleResponse>(`/ordenes-servicio/${id}`),
    enabled: Boolean(id),
  })
}

export function useAbrirOrden() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (datos: AperturaOrdenRequest) =>
      solicitar<OrdenServicioResponse>('/ordenes-servicio', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}

/** PUT /api/ordenes-servicio/{id}: datos de recepción y seguimiento de la orden. */
export function useActualizarOrden() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id: string; datos: ActualizarOrdenRequest }) =>
      solicitar<OrdenServicioResponse>(`/ordenes-servicio/${id}`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}

export function useRegistrarDiagnostico() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id: string; datos: DiagnosticoRequest }) =>
      solicitar<OrdenServicioResponse>(`/ordenes-servicio/${id}/diagnostico`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}

export function useAgregarDetalle() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id: string; datos: AgregarDetalleRequest }) =>
      solicitar<DetalleServicioResponse>(`/ordenes-servicio/${id}/detalles`, {
        metodo: 'POST',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}

export function useEliminarDetalle() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, detalleId }: { id: string; detalleId: string }) =>
      solicitar<{ message: string }>(`/ordenes-servicio/${id}/detalles/${detalleId}`, {
        metodo: 'DELETE',
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}

export function useCambiarEstado() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id: string; datos: CambiarEstadoRequest }) =>
      solicitar<OrdenServicioResponse>(`/ordenes-servicio/${id}/estado`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}
