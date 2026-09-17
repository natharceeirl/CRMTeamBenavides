import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type { PermisoResponse, RolRequest, RolResponse } from './tipos'

export const clavesRoles = {
  todos: ['roles'] as const,
  permisos: (rolId: string) => ['roles', rolId, 'permisos'] as const,
}

export function useRoles() {
  return useQuery({
    queryKey: clavesRoles.todos,
    queryFn: () => solicitar<RolResponse[]>('/roles'),
  })
}

export function usePermisosDeRol(rolId: string | undefined) {
  return useQuery({
    queryKey: clavesRoles.permisos(rolId ?? ''),
    queryFn: () => solicitar<PermisoResponse[]>(`/roles/${rolId}/permisos`),
    enabled: Boolean(rolId),
  })
}

export function useGuardarRol() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id?: string; datos: RolRequest }) =>
      id
        ? solicitar<RolResponse>(`/roles/${id}`, { metodo: 'PUT', cuerpo: datos })
        : solicitar<RolResponse>('/roles', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesRoles.todos })
    },
  })
}

export function useEliminarRol() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => solicitar<void>(`/roles/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesRoles.todos })
    },
  })
}

export function useAsignarPermiso() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ rolId, permisoId }: { rolId: string; permisoId: string }) =>
      solicitar<void>(`/roles/${rolId}/permisos`, { metodo: 'POST', cuerpo: { permisoId } }),
    onSuccess: async (_datos, { rolId }) => {
      await consultas.invalidateQueries({ queryKey: clavesRoles.permisos(rolId) })
    },
  })
}

export function useQuitarPermiso() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ rolId, permisoId }: { rolId: string; permisoId: string }) =>
      solicitar<void>(`/roles/${rolId}/permisos/${permisoId}`, { metodo: 'DELETE' }),
    onSuccess: async (_datos, { rolId }) => {
      await consultas.invalidateQueries({ queryKey: clavesRoles.permisos(rolId) })
    },
  })
}
