import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { clavesInventario } from './inventario'
import type { IngresarMotoRequest, MotoResponse } from './tipos'

/** Enum EstadoUnidadInventario del backend. Una moto no se separa: está en stock hasta venderse. */
export const ESTADO_MOTO = { enStock: 0, vendida: 1 } as const

export type FiltrosMotos = {
  productoId?: string
  estado?: (typeof ESTADO_MOTO)[keyof typeof ESTADO_MOTO]
  busqueda?: string
}

export function rutaMotos(filtros: FiltrosMotos = {}): string {
  const parametros = new URLSearchParams()
  if (filtros.productoId) parametros.set('productoId', filtros.productoId)
  if (filtros.estado !== undefined) parametros.set('estado', String(filtros.estado))
  if (filtros.busqueda?.trim()) parametros.set('busqueda', filtros.busqueda.trim())
  const consulta = parametros.toString()
  return `/motos${consulta ? `?${consulta}` : ''}`
}

export const clavesMotos = {
  todas: ['motos'] as const,
  lista: (filtros: FiltrosMotos) => ['motos', rutaMotos(filtros)] as const,
}

/** Las motos del inventario; con `habilitado` en false no consulta (la línea no es una moto). */
export function useMotos(filtros: FiltrosMotos = {}, habilitado = true) {
  return useQuery({
    queryKey: clavesMotos.lista(filtros),
    queryFn: () => solicitar<MotoResponse[]>(rutaMotos(filtros)),
    enabled: habilitado,
  })
}

/** Da de baja un modelo de moto; la API lo impide si todavía tiene motos en stock. */
export function useDarDeBajaModelo() {
  const consultas = useQueryClient()
  return useMutation({
    meta: { exito: 'Modelo dado de baja' },
    mutationFn: (productoId: string) => solicitar<void>(`/productos/${productoId}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesInventario.productos })
      await consultas.invalidateQueries({ queryKey: clavesMotos.todas })
    },
  })
}

/** Ingresa una moto que ya estaba en la tienda: suma 1 al stock de su modelo. */
export function useIngresarMoto() {
  const consultas = useQueryClient()
  return useMutation({
    mutationFn: ({ productoId, datos }: { productoId: string; datos: IngresarMotoRequest }) =>
      solicitar<MotoResponse>(`/productos/${productoId}/motos`, { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesMotos.todas })
      await consultas.invalidateQueries({ queryKey: clavesInventario.productos })
    },
  })
}
