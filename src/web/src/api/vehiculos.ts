import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type { VehiculoRequest, VehiculoResponse } from './tipos'

export const clavesVehiculos = {
  todos: ['vehiculos'] as const,
  lista: (clienteId?: string) => ['vehiculos', clienteId ?? 'todos'] as const,
}

export function useVehiculos(clienteId?: string) {
  return useQuery({
    queryKey: clavesVehiculos.lista(clienteId),
    queryFn: () => solicitar<VehiculoResponse[]>(clienteId ? `/vehiculos?clienteId=${clienteId}` : '/vehiculos'),
  })
}

export function useGuardarVehiculo() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id?: string; datos: VehiculoRequest }) =>
      id
        ? solicitar<VehiculoResponse>(`/vehiculos/${id}`, { metodo: 'PUT', cuerpo: datos })
        : solicitar<VehiculoResponse>('/vehiculos', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesVehiculos.todos })
    },
  })
}

export function useEliminarVehiculo() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => solicitar<void>(`/vehiculos/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesVehiculos.todos })
    },
  })
}
