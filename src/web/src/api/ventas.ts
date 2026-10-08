import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { avisoSegun } from './avisos'
import { clavesInventario } from './inventario'
import { clavesOrdenes } from './ordenes'
import type {
  ActualizarVentaRequest,
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

/** GET /api/ventas/exportar-excel con los mismos filtros de la lista. */
export const rutaExportarVentas = (filtros: FiltrosVentas = {}) =>
  rutaVentas(filtros).replace('/ventas', '/ventas/exportar-excel')

export const clavesVentas = {
  todas: ['ventas'] as const,
  pendientesComprobante: ['ventas', 'pendientes-comprobante'] as const,
  lista: (filtros: FiltrosVentas) =>
    ['ventas', filtros.estado ?? 'todos', filtros.clienteId ?? 'todos', filtros.ordenServicioId ?? 'todas'] as const,
  una: (id: string) => ['ventas', id] as const,
}

export function useVentas(filtros: FiltrosVentas = {}) {
  return useQuery({
    queryKey: clavesVentas.lista(filtros),
    queryFn: () => solicitar<VentaResponse[]>(rutaVentas(filtros)),
  })
}

/** Ventas confirmadas que todavía no tienen comprobante: se emite después sin duplicar venta, caja ni stock. */
export function useVentasPendientesComprobante(habilitado = true) {
  return useQuery({
    queryKey: clavesVentas.pendientesComprobante,
    queryFn: () => solicitar<VentaResponse[]>('/ventas/pendientes-comprobante'),
    enabled: habilitado,
  })
}

export function useVenta(id: string | undefined) {
  return useQuery({
    queryKey: clavesVentas.una(id ?? ''),
    queryFn: () => solicitar<VentaDetalleResponse>(`/ventas/${id}`),
    enabled: Boolean(id),
  })
}

/**
 * Crear, confirmar y anular pueden mover stock: refrescan también inventario.
 * Una venta que liquida una orden cambia el saldo y la venta de esa orden.
 */
function refrescarVentasEInventario(consultas: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    consultas.invalidateQueries({ queryKey: clavesVentas.todas }),
    consultas.invalidateQueries({ queryKey: clavesInventario.productos }),
    consultas.invalidateQueries({ queryKey: clavesInventario.movimientos }),
    consultas.invalidateQueries({ queryKey: clavesOrdenes.todas }),
    // Un precio fuera de lista abre una solicitud a Gerencia. La clave se escribe
    // aquí porque api/aprobaciones importa este archivo.
    consultas.invalidateQueries({ queryKey: ['aprobaciones'] }),
  ])
}

/** Corrige las líneas de una cotización, por ejemplo después de un rechazo de Gerencia. */
export function useActualizarVenta() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Cotización actualizada' },
    mutationFn: ({ id, datos }: { id: string; datos: ActualizarVentaRequest }) =>
      solicitar<VentaDetalleResponse>(`/ventas/${id}`, { metodo: 'PUT', cuerpo: datos }),
    onSuccess: async () => {
      await refrescarVentasEInventario(consultas)
    },
  })
}

export function useCrearVenta() {
  const consultas = useQueryClient()

  return useMutation({
    meta: {
      exito: avisoSegun<CrearVentaRequest>((datos) => {
        if (datos.ordenServicioId) return 'Orden liquidada: venta registrada'
        return datos.esCotizacion ? 'Cotización creada' : 'Venta registrada'
      }),
    },
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
    meta: { exito: 'Venta confirmada' },
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
    meta: { exito: 'Venta anulada' },
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
    meta: { exito: 'Comprobante registrado' },
    mutationFn: ({ ventaId, datos }: { ventaId: string; datos: RegistrarComprobanteRequest }) =>
      solicitar<ComprobanteResponse>(`/ventas/${ventaId}/comprobante`, { metodo: 'POST', cuerpo: datos }),
    onSuccess: async (_, variables) => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesVentas.todas }),
        consultas.invalidateQueries({ queryKey: clavesVentas.una(variables.ventaId) }),
        consultas.invalidateQueries({ queryKey: clavesOrdenes.todas }),
      ])
    },
  })
}

export function useAnularComprobante() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Comprobante anulado' },
    mutationFn: (ventaId: string) =>
      solicitar<ComprobanteResponse>(`/ventas/${ventaId}/comprobante/anular`, { metodo: 'PUT' }),
    onSuccess: async (_, ventaId) => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesVentas.todas }),
        consultas.invalidateQueries({ queryKey: clavesVentas.una(ventaId) }),
        consultas.invalidateQueries({ queryKey: clavesOrdenes.todas }),
      ])
    },
  })
}
