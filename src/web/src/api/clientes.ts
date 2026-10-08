import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ErrorApi, solicitar } from './http'
import type { AltaRapidaClienteRequest, ClienteRequest, ClienteResponse } from './tipos'

export const clavesClientes = {
  todos: ['clientes'] as const,
  uno: (id: string) => ['clientes', id] as const,
  documento: (numero: string) => ['clientes', 'documento', numero] as const,
}

/** GET /api/clientes/documento/{dni}. Devuelve `null` si no hay un cliente con ese documento. */
export function useClientePorDocumento(numero: string | null) {
  return useQuery({
    queryKey: clavesClientes.documento(numero ?? ''),
    queryFn: async () => {
      try {
        return await solicitar<ClienteResponse>(`/clientes/documento/${encodeURIComponent(numero ?? '')}`)
      } catch (fallo) {
        if (fallo instanceof ErrorApi && fallo.estado === 404) return null
        throw fallo
      }
    },
    enabled: Boolean(numero),
    retry: false,
  })
}

/** Registro con lo mínimo para atender: documento, nombre y contacto. */
export function useAltaRapidaCliente() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Cliente registrado' },
    mutationFn: (datos: AltaRapidaClienteRequest) =>
      solicitar<ClienteResponse>('/clientes/alta-rapida', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesClientes.todos })
    },
  })
}

/** `habilitado` en falso evita pedir clientes a quien no tiene `clientes.ver` y recibiría 403. */
export function useClientes(habilitado = true) {
  return useQuery({
    queryKey: clavesClientes.todos,
    queryFn: () => solicitar<ClienteResponse[]>('/clientes'),
    enabled: habilitado,
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
    meta: { exito: 'Cliente guardado' },
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
    meta: { exito: 'Cliente dado de baja' },
    mutationFn: (id: string) => solicitar<void>(`/clientes/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesClientes.todos })
    },
  })
}
