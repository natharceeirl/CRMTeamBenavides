import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type {
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
 * Son siete y no coinciden con los nueve del mapa funcional. El Ingeniero
 * todavía tiene que cerrar el flujo definitivo con el cliente.
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

export const esEstadoTerminal = (estadoId: number) =>
  estadoId === ESTADO.entregada || estadoId === ESTADO.cancelada

/** La orden se puede editar (diagnóstico y detalles) salvo en Lista o terminal. */
export const permiteEditarDetalles = (estadoId: number) =>
  !esEstadoTerminal(estadoId) && estadoId !== ESTADO.lista

export const clavesOrdenes = {
  todas: ['ordenes'] as const,
  lista: (estado?: number, vehiculoId?: string) =>
    ['ordenes', 'lista', estado ?? 'todos', vehiculoId ?? 'todos'] as const,
  una: (id: string) => ['ordenes', id] as const,
}

export function useOrdenes(filtros: { estado?: number; vehiculoId?: string } = {}) {
  const { estado, vehiculoId } = filtros

  return useQuery({
    queryKey: clavesOrdenes.lista(estado, vehiculoId),
    queryFn: () => {
      const parametros = new URLSearchParams()
      if (estado !== undefined) parametros.set('estado', String(estado))
      if (vehiculoId) parametros.set('vehiculoId', vehiculoId)
      const consulta = parametros.toString()
      return solicitar<OrdenServicioResponse[]>(
        `/ordenes-servicio${consulta ? `?${consulta}` : ''}`,
      )
    },
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
