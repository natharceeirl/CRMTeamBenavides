import { useQuery } from '@tanstack/react-query'
import { solicitar } from './http'
import type { PermisoResponse } from './tipos'

export const clavesPermisos = {
  todos: ['permisos'] as const,
}

/** El catálogo de permisos es de solo lectura: lo siembra el backend. */
export function usePermisos() {
  return useQuery({
    queryKey: clavesPermisos.todos,
    queryFn: () => solicitar<PermisoResponse[]>('/permisos'),
  })
}
