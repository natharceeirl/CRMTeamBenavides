import { useQuery } from '@tanstack/react-query'
import { solicitar } from './http'
import type { EventoAuditoriaResponse } from './tipos'

export type FiltrosAuditoria = {
  entidad?: string
  accion?: string
  fechaDesde?: string
  fechaHasta?: string
  /** El backend devuelve entre 1 y 200; por defecto 50. */
  limite?: number
}

export function rutaAuditoria(filtros: FiltrosAuditoria): string {
  const parametros = new URLSearchParams()
  if (filtros.entidad) parametros.set('entidad', filtros.entidad)
  if (filtros.accion) parametros.set('accion', filtros.accion)
  if (filtros.fechaDesde) parametros.set('fechaDesde', filtros.fechaDesde)
  if (filtros.fechaHasta) parametros.set('fechaHasta', filtros.fechaHasta)
  if (filtros.limite) parametros.set('limite', String(filtros.limite))
  const consulta = parametros.toString()
  return `/auditoria${consulta ? `?${consulta}` : ''}`
}

/** Solo con `auditoria.ver`: la ruta y el menú ya lo exigen. */
export function useAuditoria(filtros: FiltrosAuditoria) {
  return useQuery({
    queryKey: ['auditoria', filtros],
    queryFn: () => solicitar<EventoAuditoriaResponse[]>(rutaAuditoria(filtros)),
  })
}
