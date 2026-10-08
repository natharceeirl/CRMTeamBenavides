import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { avisoSegun } from './avisos'
import type {
  ActualizarProductoRequest,
  AjusteRequest,
  AltaRapidaProductoRequest,
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
  marca?: string
}

/** Arma la ruta con los filtros que acepta GET /api/productos. */
export function rutaProductos(filtros: FiltrosProductos = {}): string {
  const parametros = new URLSearchParams()
  if (filtros.categoriaId) parametros.set('categoriaId', filtros.categoriaId)
  if (filtros.busqueda?.trim()) parametros.set('busqueda', filtros.busqueda.trim())
  if (filtros.bajoStock) parametros.set('bajoStock', 'true')
  if (filtros.marca) parametros.set('marca', filtros.marca)

  const consulta = parametros.toString()
  return `/productos${consulta ? `?${consulta}` : ''}`
}

export const clavesInventario = {
  productos: ['productos'] as const,
  listaProductos: (filtros: FiltrosProductos) =>
    [
      'productos',
      filtros.categoriaId ?? 'todas',
      filtros.busqueda ?? '',
      filtros.bajoStock ?? false,
      filtros.marca ?? 'todas',
    ] as const,
  marcas: ['productos', 'marcas'] as const,
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

/** Las marcas que ya tienen repuestos, para filtrar y para sugerir al registrar. */
export function useMarcasProductos() {
  return useQuery({
    queryKey: clavesInventario.marcas,
    queryFn: () => solicitar<string[]>('/productos/marcas'),
    staleTime: 5 * 60_000,
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
    meta: { exito: 'Repuesto guardado' },
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

/** Registro mínimo de un repuesto desde una venta o una orden; si el código ya existe, devuelve ese. */
export function useAltaRapidaProducto() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Repuesto registrado' },
    mutationFn: (datos: AltaRapidaProductoRequest) =>
      solicitar<ProductoResponse>('/productos/alta-rapida', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesInventario.productos }),
        consultas.invalidateQueries({ queryKey: clavesInventario.categorias }),
      ])
    },
  })
}

/** Una categoría con solo su nombre, desde el registro de un repuesto. */
export function useAltaRapidaCategoria() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Categoría registrada' },
    mutationFn: (nombre: string) =>
      solicitar<CategoriaProductoResponse>('/categorias-producto/alta-rapida', {
        metodo: 'POST',
        cuerpo: { nombre },
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesInventario.categorias })
    },
  })
}

export function useEliminarProducto() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Repuesto dado de baja' },
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
    meta: { exito: avisoSegun<{ tipo: 'entradas' | 'salidas' | 'ajustes' }>(({ tipo }) =>
        tipo === 'entradas' ? 'Entrada registrada' : tipo === 'salidas' ? 'Salida registrada' : 'Ajuste registrado',
      ) },
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
    meta: { exito: 'Categoría guardada' },
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
    meta: { exito: 'Categoría eliminada' },
    mutationFn: (id: string) => solicitar<void>(`/categorias-producto/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesInventario.categorias })
    },
  })
}
