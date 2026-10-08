import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type { AltaRapidaVehiculoRequest, VehiculoRequest, VehiculoResponse } from './tipos'

export const clavesVehiculos = {
  todos: ['vehiculos'] as const,
  lista: (clienteId?: string) => ['vehiculos', clienteId ?? 'todos'] as const,
}

/** `habilitado` en falso evita pedir unidades a quien no tiene `unidades.ver` y recibiría 403. */
export function useVehiculos(clienteId?: string, habilitado = true) {
  return useQuery({
    queryKey: clavesVehiculos.lista(clienteId),
    queryFn: () => solicitar<VehiculoResponse[]>(clienteId ? `/vehiculos?clienteId=${clienteId}` : '/vehiculos'),
    enabled: habilitado,
  })
}

export function useGuardarVehiculo() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Unidad guardada' },
    mutationFn: ({ id, datos }: { id?: string; datos: VehiculoRequest }) =>
      id
        ? solicitar<VehiculoResponse>(`/vehiculos/${id}`, { metodo: 'PUT', cuerpo: datos })
        : solicitar<VehiculoResponse>('/vehiculos', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesVehiculos.todos })
    },
  })
}

/** Registro con lo mínimo para abrir la orden: placa, marca, modelo y kilometraje. */
export function useAltaRapidaVehiculo() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Unidad registrada' },
    mutationFn: (datos: AltaRapidaVehiculoRequest) =>
      solicitar<VehiculoResponse>('/vehiculos/alta-rapida', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesVehiculos.todos })
    },
  })
}

export function useEliminarVehiculo() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Unidad dada de baja' },
    mutationFn: (id: string) => solicitar<void>(`/vehiculos/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesVehiculos.todos })
    },
  })
}
