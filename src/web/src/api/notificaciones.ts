import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type { ConteoNotificacionesResponse, NotificacionResponse } from './tipos'

export const clavesNotificaciones = {
  todas: ['notificaciones'] as const,
  lista: ['notificaciones', 'lista'] as const,
  conteo: ['notificaciones', 'conteo'] as const,
}

/** La web no recibe avisos al instante: el contador se pregunta cada minuto. */
const CADA_MINUTO = 60_000

export function useConteoNotificaciones() {
  return useQuery({
    queryKey: clavesNotificaciones.conteo,
    queryFn: () => solicitar<ConteoNotificacionesResponse>('/notificaciones/conteo'),
    refetchInterval: CADA_MINUTO,
    refetchOnWindowFocus: true,
  })
}

/** Los últimos avisos, solo mientras la bandeja está abierta. */
export function useNotificaciones(habilitado: boolean) {
  return useQuery({
    queryKey: clavesNotificaciones.lista,
    queryFn: () => solicitar<NotificacionResponse[]>('/notificaciones?limite=30'),
    enabled: habilitado,
  })
}

function useRefrescarBandeja() {
  const consultas = useQueryClient()
  return () => consultas.invalidateQueries({ queryKey: clavesNotificaciones.todas })
}

export function useMarcarNotificacionLeida() {
  const refrescar = useRefrescarBandeja()
  return useMutation({
    mutationFn: (id: string) => solicitar<void>(`/notificaciones/${id}/leida`, { metodo: 'PUT' }),
    onSuccess: refrescar,
  })
}

export function useMarcarTodasLeidas() {
  const refrescar = useRefrescarBandeja()
  return useMutation({
    meta: { exito: 'Notificaciones marcadas como leídas' },
    mutationFn: () => solicitar<void>('/notificaciones/leidas', { metodo: 'PUT' }),
    onSuccess: refrescar,
  })
}

/**
 * La pantalla a la que lleva un aviso. El cliente ve todo lo suyo en su portal;
 * el personal va a la pantalla de esa operación.
 */
export function rutaDeNotificacion(
  notificacion: Pick<NotificacionResponse, 'entidad' | 'entidadId'>,
  esSoloCliente: boolean,
): string {
  switch (notificacion.entidad) {
    case 'Aprobaciones':
      return '/aprobaciones'
    case 'OrdenServicio':
      if (esSoloCliente) return '/portal'
      return notificacion.entidadId ? `/ordenes/${notificacion.entidadId}` : '/ordenes'
    case 'Cita':
      return esSoloCliente ? '/portal' : '/citas'
    case 'PedidoLima':
      return esSoloCliente ? '/portal' : '/pedidos-lima'
    default:
      return esSoloCliente ? '/portal' : '/'
  }
}
