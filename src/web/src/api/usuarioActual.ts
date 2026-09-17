import { useQuery } from '@tanstack/react-query'
import { solicitar } from './http'
import type { UsuarioActualResponse } from './tipos'

export const clavesUsuarioActual = {
  yo: ['auth', 'me'] as const,
}

/**
 * Usuario de la sesión con sus roles, desde GET /api/auth/me.
 *
 * El token solo trae id, correo y nombre; los roles solo llegan por aquí.
 */
export function useUsuarioActual(habilitado: boolean) {
  return useQuery({
    queryKey: clavesUsuarioActual.yo,
    queryFn: () => solicitar<UsuarioActualResponse>('/auth/me'),
    enabled: habilitado,
    staleTime: 5 * 60 * 1000,
  })
}
