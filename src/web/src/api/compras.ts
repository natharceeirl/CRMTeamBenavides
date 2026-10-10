import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { clavesInventario } from './inventario'
import { clavesCaja } from './caja'
import type {
  ActualizarCompraRequest,
  CompraResponse,
  CompraResumenResponse,
  CrearCompraRequest,
  EstadoCompra,
  GuardarProveedorRequest,
  ProveedorResponse,
  RegistrarPagoCompraRequest,
  TipoComprobanteCompra,
} from './tipos'

export const nombresTipoComprobanteCompra: Record<TipoComprobanteCompra, string> = {
  0: 'Factura',
  1: 'Boleta',
  2: 'Ticket',
  3: 'Nota de venta',
  4: 'Recibo por honorarios',
  5: 'Otro',
}

/** Estados de pago que calcula el backend y los dos filtros extra de la lista. */
export const ESTADO_PAGO_COMPRA = {
  pendiente: 'Pendiente',
  parcial: 'Parcial',
  pagada: 'Pagada',
  porPagar: 'PorPagar',
  vencida: 'Vencida',
} as const

export type FiltrosCompras = {
  proveedorId?: string
  estado?: EstadoCompra
  estadoPago?: string
  fechaDesde?: string
  fechaHasta?: string
  busqueda?: string
}

export function rutaCompras(filtros: FiltrosCompras, base = '/compras'): string {
  const parametros = new URLSearchParams()
  if (filtros.proveedorId) parametros.set('proveedorId', filtros.proveedorId)
  if (filtros.estado !== undefined) parametros.set('estado', String(filtros.estado))
  if (filtros.estadoPago) parametros.set('estadoPago', filtros.estadoPago)
  if (filtros.fechaDesde) parametros.set('fechaDesde', filtros.fechaDesde)
  if (filtros.fechaHasta) parametros.set('fechaHasta', filtros.fechaHasta)
  if (filtros.busqueda?.trim()) parametros.set('busqueda', filtros.busqueda.trim())
  const consulta = parametros.toString()
  return consulta ? `${base}?${consulta}` : base
}

export const clavesCompras = {
  todas: ['compras'] as const,
  lista: (filtros: FiltrosCompras) => ['compras', 'lista', filtros] as const,
  una: (id: string) => ['compras', id] as const,
  proveedores: ['proveedores'] as const,
}

export function useCompras(filtros: FiltrosCompras) {
  return useQuery({
    queryKey: clavesCompras.lista(filtros),
    queryFn: () => solicitar<CompraResumenResponse[]>(rutaCompras(filtros)),
  })
}

export function useCompra(id: string | null) {
  return useQuery({
    queryKey: clavesCompras.una(id ?? ''),
    queryFn: () => solicitar<CompraResponse>(`/compras/${id}`),
    enabled: Boolean(id),
  })
}

export function useProveedores(habilitado = true) {
  return useQuery({
    queryKey: clavesCompras.proveedores,
    queryFn: () => solicitar<ProveedorResponse[]>('/proveedores'),
    enabled: habilitado,
  })
}

/**
 * Registrar o anular mueve stock y costo, y un pago en efectivo mueve la caja:
 * cada mutación refresca lo que pudo cambiar además de las compras.
 */
function useMutacionCompra<V>(
  mutationFn: (variables: V) => Promise<CompraResponse>,
  exito: string,
  { stock, caja }: { stock: boolean; caja: boolean },
) {
  const consultas = useQueryClient()
  return useMutation({
    meta: { exito },
    mutationFn,
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesCompras.todas }),
        stock && consultas.invalidateQueries({ queryKey: clavesInventario.productos }),
        stock && consultas.invalidateQueries({ queryKey: clavesInventario.movimientos }),
        caja && consultas.invalidateQueries({ queryKey: clavesCaja.todas }),
      ])
    },
  })
}

export const useRegistrarCompra = () =>
  useMutacionCompra(
    (datos: CrearCompraRequest) => solicitar<CompraResponse>('/compras', { metodo: 'POST', cuerpo: datos }),
    'Compra registrada',
    { stock: true, caja: true },
  )

export const useEditarCompra = () =>
  useMutacionCompra(
    ({ id, datos }: { id: string; datos: ActualizarCompraRequest }) =>
      solicitar<CompraResponse>(`/compras/${id}`, { metodo: 'PUT', cuerpo: datos }),
    'Datos del comprobante guardados',
    { stock: false, caja: false },
  )

export const useRegistrarPagoCompra = () =>
  useMutacionCompra(
    ({ id, datos }: { id: string; datos: RegistrarPagoCompraRequest }) =>
      solicitar<CompraResponse>(`/compras/${id}/pagos`, { metodo: 'POST', cuerpo: datos }),
    'Pago al proveedor registrado',
    { stock: false, caja: true },
  )

export const useAnularPagoCompra = () =>
  useMutacionCompra(
    ({ id, pagoId, motivo }: { id: string; pagoId: string; motivo: string }) =>
      solicitar<CompraResponse>(`/compras/${id}/pagos/${pagoId}/anular`, { metodo: 'PUT', cuerpo: { motivo } }),
    'Pago anulado',
    { stock: false, caja: true },
  )

export const useAnularCompra = () =>
  useMutacionCompra(
    ({ id, motivo }: { id: string; motivo: string }) =>
      solicitar<CompraResponse>(`/compras/${id}/anular`, { metodo: 'PUT', cuerpo: { motivo } }),
    'Compra anulada',
    { stock: true, caja: true },
  )

export function useGuardarProveedor() {
  const consultas = useQueryClient()
  return useMutation({
    meta: { exito: 'Proveedor guardado' },
    mutationFn: ({ id, datos }: { id?: string; datos: GuardarProveedorRequest }) =>
      id
        ? solicitar<ProveedorResponse>(`/proveedores/${id}`, { metodo: 'PUT', cuerpo: datos })
        : solicitar<ProveedorResponse>('/proveedores', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesCompras.proveedores }),
        // El nombre del proveedor aparece en la lista de compras.
        consultas.invalidateQueries({ queryKey: clavesCompras.todas }),
      ])
    },
  })
}

export function useEliminarProveedor() {
  const consultas = useQueryClient()
  return useMutation({
    meta: { exito: 'Proveedor dado de baja' },
    mutationFn: (id: string) => solicitar<void>(`/proveedores/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesCompras.proveedores })
    },
  })
}
