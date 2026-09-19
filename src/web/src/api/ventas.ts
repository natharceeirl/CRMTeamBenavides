import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { clavesInventario } from './inventario'
import type {
  ComprobanteResponse,
  CrearVentaRequest,
  RegistrarComprobanteRequest,
  VentaDetalleResponse,
  VentaResponse,
} from './tipos'

/** Estados del backend (enum EstadoVenta). */
export const ESTADO_VENTA = {
  cotizacion: 0,
  confirmada: 1,
  anulada: 2,
} as const

export const nombresEstadoVenta: Record<number, string> = {
  0: 'Cotización',
  1: 'Confirmada',
  2: 'Anulada',
}

/** Solo se confirma lo que sigue siendo cotización. */
export const puedeConfirmar = (estadoId: number) => estadoId === ESTADO_VENTA.cotizacion

/** Se anula cualquier cosa que no esté ya anulada; si estaba confirmada, el stock vuelve. */
export const puedeAnular = (estadoId: number) => estadoId !== ESTADO_VENTA.anulada

/** Solo lleva comprobante una venta confirmada que todavía no tiene uno. */
export const puedeRegistrarComprobante = (estadoId: number, tieneComprobante: boolean) =>
  estadoId === ESTADO_VENTA.confirmada && !tieneComprobante

/** Se anula el comprobante emitido; uno ya anulado no se toca. */
export const puedeAnularComprobante = (estadoComprobante: string | null | undefined) =>
  estadoComprobante === 'Emitido'

export type FiltrosVentas = {
  estado?: number
  clienteId?: string
  ordenServicioId?: string
}

export function rutaVentas(filtros: FiltrosVentas = {}): string {
  const parametros = new URLSearchParams()
  if (filtros.estado !== undefined) parametros.set('estado', String(filtros.estado))
  if (filtros.clienteId) parametros.set('clienteId', filtros.clienteId)
  if (filtros.ordenServicioId) parametros.set('ordenServicioId', filtros.ordenServicioId)

  const consulta = parametros.toString()
  return `/ventas${consulta ? `?${consulta}` : ''}`
}

export const clavesVentas = {
  todas: ['ventas'] as const,
  lista: (filtros: FiltrosVentas) =>
    ['ventas', filtros.estado ?? 'todos', filtros.clienteId ?? 'todos'] as const,
  una: (id: string) => ['ventas', id] as const,
}

export function useVentas(filtros: FiltrosVentas = {}) {
  return useQuery({
    queryKey: clavesVentas.lista(filtros),
    queryFn: () => solicitar<VentaResponse[]>(rutaVentas(filtros)),
  })
}

export function useVenta(id: string | undefined) {
  return useQuery({
    queryKey: clavesVentas.una(id ?? ''),
    queryFn: () => solicitar<VentaDetalleResponse>(`/ventas/${id}`),
    enabled: Boolean(id),
  })
}

/** Crear, confirmar y anular pueden mover stock: refrescan también inventario. */
function refrescarVentasEInventario(consultas: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    consultas.invalidateQueries({ queryKey: clavesVentas.todas }),
    consultas.invalidateQueries({ queryKey: clavesInventario.productos }),
    consultas.invalidateQueries({ queryKey: clavesInventario.movimientos }),
  ])
}

export function useCrearVenta() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (datos: CrearVentaRequest) =>
      solicitar<VentaDetalleResponse>('/ventas', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await refrescarVentasEInventario(consultas)
    },
  })
}

export function useConfirmarVenta() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) =>
      solicitar<VentaDetalleResponse>(`/ventas/${id}/confirmar`, { metodo: 'PUT' }),
    onSuccess: async () => {
      await refrescarVentasEInventario(consultas)
    },
  })
}

export function useAnularVenta() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) =>
      solicitar<VentaDetalleResponse>(`/ventas/${id}/anular`, { metodo: 'PUT' }),
    onSuccess: async () => {
      await refrescarVentasEInventario(consultas)
    },
  })
}

export function useRegistrarComprobante() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ ventaId, datos }: { ventaId: string; datos: RegistrarComprobanteRequest }) =>
      solicitar<ComprobanteResponse>(`/ventas/${ventaId}/comprobante`, { metodo: 'POST', cuerpo: datos }),
    onSuccess: async (_, variables) => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesVentas.todas }),
        consultas.invalidateQueries({ queryKey: clavesVentas.una(variables.ventaId) }),
      ])
    },
  })
}

export function useAnularComprobante() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (ventaId: string) =>
      solicitar<ComprobanteResponse>(`/ventas/${ventaId}/comprobante/anular`, { metodo: 'PUT' }),
    onSuccess: async (_, ventaId) => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesVentas.todas }),
        consultas.invalidateQueries({ queryKey: clavesVentas.una(ventaId) }),
      ])
    },
  })
}
