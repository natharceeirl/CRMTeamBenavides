import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type { ActualizarUsuarioRequest, CrearUsuarioRequest, RolResponse, UsuarioResponse } from './tipos'

export const clavesUsuarios = {
  todos: ['usuarios'] as const,
  roles: (usuarioId: string) => ['usuarios', usuarioId, 'roles'] as const,
}

export function useUsuarios() {
  return useQuery({
    queryKey: clavesUsuarios.todos,
    queryFn: () => solicitar<UsuarioResponse[]>('/usuarios'),
  })
}

/** La lista de usuarios trae los nombres de los roles; esto trae también sus ids. */
export function useRolesDeUsuario(usuarioId: string | undefined) {
  return useQuery({
    queryKey: clavesUsuarios.roles(usuarioId ?? ''),
    queryFn: () => solicitar<RolResponse[]>(`/usuarios/${usuarioId}/roles`),
    enabled: Boolean(usuarioId),
  })
}

export function useCrearUsuario() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (datos: CrearUsuarioRequest) =>
      solicitar<UsuarioResponse>('/usuarios', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesUsuarios.todos })
    },
  })
}

export function useActualizarUsuario() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id: string; datos: ActualizarUsuarioRequest }) =>
      solicitar<UsuarioResponse>(`/usuarios/${id}`, { metodo: 'PUT', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesUsuarios.todos })
    },
  })
}

export function useEliminarUsuario() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => solicitar<void>(`/usuarios/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesUsuarios.todos })
    },
  })
}

export function useAsignarRolAUsuario() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ usuarioId, rolId }: { usuarioId: string; rolId: string }) =>
      solicitar<void>(`/usuarios/${usuarioId}/roles`, { metodo: 'POST', cuerpo: { rolId } }),
    onSuccess: async (_datos, { usuarioId }) => {
      await consultas.invalidateQueries({ queryKey: clavesUsuarios.roles(usuarioId) })
      await consultas.invalidateQueries({ queryKey: clavesUsuarios.todos })
    },
  })
}

export function useQuitarRolAUsuario() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ usuarioId, rolId }: { usuarioId: string; rolId: string }) =>
      solicitar<void>(`/usuarios/${usuarioId}/roles/${rolId}`, { metodo: 'DELETE' }),
    onSuccess: async (_datos, { usuarioId }) => {
      await consultas.invalidateQueries({ queryKey: clavesUsuarios.roles(usuarioId) })
      await consultas.invalidateQueries({ queryKey: clavesUsuarios.todos })
    },
  })
}
