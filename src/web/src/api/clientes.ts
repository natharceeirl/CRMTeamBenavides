import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type { ClienteRequest, ClienteResponse } from './tipos'

export const clavesClientes = {
  todos: ['clientes'] as const,
  uno: (id: string) => ['clientes', id] as const,
}

export function useClientes() {
  return useQuery({
    queryKey: clavesClientes.todos,
    queryFn: () => solicitar<ClienteResponse[]>('/clientes'),
  })
}

export function useCliente(id: string | undefined) {
  return useQuery({
    queryKey: clavesClientes.uno(id ?? ''),
    queryFn: () => solicitar<ClienteResponse>(`/clientes/${id}`),
    enabled: Boolean(id),
  })
}

/** Crea si no hay id y actualiza si lo hay: el backend recibe los mismos campos. */
export function useGuardarCliente() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id?: string; datos: ClienteRequest }) =>
      id
        ? solicitar<ClienteResponse>(`/clientes/${id}`, { metodo: 'PUT', cuerpo: datos })
        : solicitar<ClienteResponse>('/clientes', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesClientes.todos })
    },
  })
}

export function useEliminarCliente() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => solicitar<void>(`/clientes/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesClientes.todos })
    },
  })
}
