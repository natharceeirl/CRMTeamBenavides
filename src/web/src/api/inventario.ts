import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type {
  ActualizarProductoRequest,
  AjusteRequest,
  CategoriaProductoRequest,
  CategoriaProductoResponse,
  CrearProductoRequest,
  EntradaRequest,
  MovimientoInventarioResponse,
  ProductoResponse,
  SalidaRequest,
} from './tipos'

/** Tipos de movimiento del backend (enum TipoMovimientoInventario). */
export const MOVIMIENTO = {
  entrada: 0,
  salida: 1,
  ajuste: 2,
} as const

export const nombresMovimiento: Record<number, string> = {
  0: 'Entrada',
  1: 'Salida',
  2: 'Ajuste',
}

export type FiltrosProductos = {
  categoriaId?: string
  busqueda?: string
  bajoStock?: boolean
}

/** Arma la ruta con los filtros que acepta GET /api/productos. */
export function rutaProductos(filtros: FiltrosProductos = {}): string {
  const parametros = new URLSearchParams()
  if (filtros.categoriaId) parametros.set('categoriaId', filtros.categoriaId)
  if (filtros.busqueda?.trim()) parametros.set('busqueda', filtros.busqueda.trim())
  if (filtros.bajoStock) parametros.set('bajoStock', 'true')

  const consulta = parametros.toString()
  return `/productos${consulta ? `?${consulta}` : ''}`
}

export const clavesInventario = {
  productos: ['productos'] as const,
  listaProductos: (filtros: FiltrosProductos) =>
    ['productos', filtros.categoriaId ?? 'todas', filtros.busqueda ?? '', filtros.bajoStock ?? false] as const,
  categorias: ['categorias-producto'] as const,
  movimientos: ['movimientos-inventario'] as const,
  movimientosDeProducto: (productoId: string) => ['movimientos-inventario', productoId] as const,
}

export function useProductos(filtros: FiltrosProductos = {}) {
  return useQuery({
    queryKey: clavesInventario.listaProductos(filtros),
    queryFn: () => solicitar<ProductoResponse[]>(rutaProductos(filtros)),
  })
}

export function useCategoriasProducto() {
  return useQuery({
    queryKey: clavesInventario.categorias,
    queryFn: () => solicitar<CategoriaProductoResponse[]>('/categorias-producto'),
  })
}

export function useMovimientos() {
  return useQuery({
    queryKey: clavesInventario.movimientos,
    queryFn: () => solicitar<MovimientoInventarioResponse[]>('/inventario/movimientos'),
  })
}

export function useMovimientosDeProducto(productoId: string | undefined) {
  return useQuery({
    queryKey: clavesInventario.movimientosDeProducto(productoId ?? ''),
    queryFn: () => solicitar<MovimientoInventarioResponse[]>(`/productos/${productoId}/movimientos`),
    enabled: Boolean(productoId),
  })
}

export function useGuardarProducto() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({
      id,
      datos,
    }: {
      id?: string
      datos: CrearProductoRequest | ActualizarProductoRequest
    }) =>
      id
        ? solicitar<ProductoResponse>(`/productos/${id}`, { metodo: 'PUT', cuerpo: datos })
        : solicitar<ProductoResponse>('/productos', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesInventario.productos })
    },
  })
}

export function useEliminarProducto() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => solicitar<void>(`/productos/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesInventario.productos })
    },
  })
}

/** Entrada, salida y ajuste mueven stock: refrescan productos y movimientos. */
export function useMovimientoDeStock() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({
      id,
      tipo,
      datos,
    }: {
      id: string
      tipo: 'entradas' | 'salidas' | 'ajustes'
      datos: EntradaRequest | SalidaRequest | AjusteRequest
    }) => solicitar<ProductoResponse>(`/productos/${id}/${tipo}`, { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesInventario.productos })
      await consultas.invalidateQueries({ queryKey: clavesInventario.movimientos })
    },
  })
}

export function useGuardarCategoria() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id?: string; datos: CategoriaProductoRequest }) =>
      id
        ? solicitar<CategoriaProductoResponse>(`/categorias-producto/${id}`, {
            metodo: 'PUT',
            cuerpo: datos,
          })
        : solicitar<CategoriaProductoResponse>('/categorias-producto', {
            metodo: 'POST',
            cuerpo: datos,
          }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesInventario.categorias })
      await consultas.invalidateQueries({ queryKey: clavesInventario.productos })
    },
  })
}

export function useEliminarCategoria() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => solicitar<void>(`/categorias-producto/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesInventario.categorias })
    },
  })
}
