import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { avisoSegun } from './avisos'
import { clavesOrdenes, GERENCIA } from './ordenes'
import { clavesVentas } from './ventas'
import { clavesPedidosLima } from './pedidosLima'

/** Valores exactos que usa el backend: no se inventan otros. */
export const TIPO_APROBACION = { cambioPrecio: 'CambioPrecio' } as const
export const ENTIDAD_APROBACION = {
  ordenServicio: 'OrdenServicio',
  venta: 'Venta',
  pedidoLima: 'PedidoLima',
} as const

export const nombresEntidadAprobacion: Record<string, string> = {
  [ENTIDAD_APROBACION.ordenServicio]: 'Orden de servicio',
  [ENTIDAD_APROBACION.venta]: 'Venta',
  [ENTIDAD_APROBACION.pedidoLima]: 'Pedido a Lima',
}

/** GET /api/aprobaciones/pendientes y /api/aprobaciones */
export type SolicitudAprobacionResponse = {
  id: string
  tipo: string
  entidad: string
  entidadId: string
  usuarioSolicitanteId: string | null
  usuarioSolicitanteNombre: string | null
  fechaSolicitud: string
  estadoId: number
  estado: string
  detalleCambio: string
  valorAnterior: number | null
  valorSolicitado: number | null
  motivo: string | null
  usuarioAprobadorId: string | null
  usuarioAprobadorNombre: string | null
  fechaRespuesta: string | null
  observacionesRespuesta: string | null
}

export type FiltrosAprobaciones = {
  entidad?: string
  estado?: number
  fechaDesde?: string
  fechaHasta?: string
}

export const clavesAprobaciones = {
  todas: ['aprobaciones'] as const,
  pendientes: ['aprobaciones', 'pendientes'] as const,
  historial: (filtros: FiltrosAprobaciones) => ['aprobaciones', 'historial', filtros] as const,
}

export function rutaAprobaciones(filtros: FiltrosAprobaciones): string {
  const parametros = new URLSearchParams()
  if (filtros.entidad) parametros.set('entidad', filtros.entidad)
  if (filtros.estado != null) parametros.set('estado', String(filtros.estado))
  if (filtros.fechaDesde) parametros.set('fechaDesde', filtros.fechaDesde)
  if (filtros.fechaHasta) parametros.set('fechaHasta', filtros.fechaHasta)
  const consulta = parametros.toString()
  return consulta ? `/aprobaciones?${consulta}` : '/aprobaciones'
}

export function useAprobacionesPendientes(habilitado = true) {
  return useQuery({
    queryKey: clavesAprobaciones.pendientes,
    queryFn: () => solicitar<SolicitudAprobacionResponse[]>('/aprobaciones/pendientes'),
    enabled: habilitado,
  })
}

export function useHistorialAprobaciones(filtros: FiltrosAprobaciones, habilitado = true) {
  return useQuery({
    queryKey: clavesAprobaciones.historial(filtros),
    queryFn: () => solicitar<SolicitudAprobacionResponse[]>(rutaAprobaciones(filtros)),
    enabled: habilitado,
  })
}

type Resolucion = { id: string; estado: number; observaciones: string | null }

/**
 * Aprobar o rechazar una solicitud. La decisión cambia el estado de la orden, la
 * venta o el pedido de origen, así que también se refrescan esas listas.
 */
export function useResolverAprobacion() {
  const consultas = useQueryClient()

  return useMutation({
    meta: {
      exito: avisoSegun<Resolucion>(({ estado }) =>
        estado === GERENCIA.aprobado ? 'Cambio aprobado' : 'Cambio rechazado',
      ),
    },
    mutationFn: ({ id, estado, observaciones }: Resolucion) =>
      solicitar<SolicitudAprobacionResponse>(`/aprobaciones/${id}/resolver`, {
        metodo: 'POST',
        cuerpo: { estado, observaciones },
      }),
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesAprobaciones.todas }),
        consultas.invalidateQueries({ queryKey: clavesOrdenes.todas }),
        consultas.invalidateQueries({ queryKey: clavesVentas.todas }),
        consultas.invalidateQueries({ queryKey: clavesPedidosLima.todas }),
      ])
    },
  })
}
