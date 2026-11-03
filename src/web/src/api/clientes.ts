import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ErrorApi, solicitar } from './http'
import type { ClienteRequest, ClienteResponse, CodigoActivacionResponse, CuentaAppClienteResponse } from './tipos'

export const clavesClientes = {
  todos: ['clientes'] as const,
  uno: (id: string) => ['clientes', id] as const,
  documento: (numero: string) => ['clientes', 'documento', numero] as const,
  cuentaApp: (id: string) => ['clientes', id, 'cuenta-app'] as const,
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

/** GET /api/clientes/{id}/cuenta-app: si el cliente ya entra a la app y, si no, hasta cuándo sirve su código. */
export function useCuentaAppCliente(id: string | undefined, habilitado = true) {
  return useQuery({
    queryKey: clavesClientes.cuentaApp(id ?? ''),
    queryFn: () => solicitar<CuentaAppClienteResponse>(`/clientes/${id}/cuenta-app`),
    enabled: Boolean(id) && habilitado,
  })
}

/** El código se ve una sola vez: la API guarda solo su hash. Uno nuevo invalida el anterior. */
export function useGenerarCodigoActivacion() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) =>
      solicitar<CodigoActivacionResponse>(`/clientes/${id}/codigo-activacion`, { metodo: 'POST' }),
    onSuccess: async (_, id) => {
      await consultas.invalidateQueries({ queryKey: clavesClientes.cuentaApp(id) })
    },
  })
}
