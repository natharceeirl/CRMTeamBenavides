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

/** `habilitado` en falso evita pedir el catálogo a quien no tiene `servicios.ver` y recibiría 403. */
export function useServicios(soloActivos: boolean = true, habilitado = true) {
  return useQuery({
    queryKey: clavesServicios.lista(soloActivos),
    queryFn: () =>
      solicitar<ServicioResponse[]>(`/servicios?soloActivos=${soloActivos}`),
    enabled: habilitado,
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
    meta: { exito: 'Servicio creado' },
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
    meta: { exito: 'Servicio guardado' },
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
    meta: { exito: 'Servicio dado de baja' },
    mutationFn: (id: string) =>
      solicitar<void>(`/servicios/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesServicios.todos })
    },
  })
}
