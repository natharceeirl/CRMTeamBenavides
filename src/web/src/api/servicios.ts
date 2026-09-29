import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type {
  ActualizarServicioRequest,
  CrearServicioRequest,
  ServicioResponse,
} from './tipos'

export const clavesServicios = {
  todos: ['servicios'] as const,
  lista: (soloActivos?: boolean) => ['servicios', 'lista', soloActivos ?? true] as const,
  uno: (id: string) => ['servicios', id] as const,
}

export function useServicios(soloActivos: boolean = true) {
  return useQuery({
    queryKey: clavesServicios.lista(soloActivos),
    queryFn: () =>
      solicitar<ServicioResponse[]>(`/servicios?soloActivos=${soloActivos}`),
  })
}

export function useServicio(id: string | undefined) {
  return useQuery({
    queryKey: clavesServicios.uno(id ?? ''),
    queryFn: () => solicitar<ServicioResponse>(`/servicios/${id}`),
    enabled: Boolean(id),
  })
}

export function useCrearServicio() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (datos: CrearServicioRequest) =>
      solicitar<ServicioResponse>('/servicios', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesServicios.todos })
    },
  })
}

export function useActualizarServicio() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id: string; datos: ActualizarServicioRequest }) =>
      solicitar<ServicioResponse>(`/servicios/${id}`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesServicios.todos })
    },
  })
}

export function useEliminarServicio() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) =>
      solicitar<void>(`/servicios/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesServicios.todos })
    },
  })
}
