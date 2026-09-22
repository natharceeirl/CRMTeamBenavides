import { useQuery } from '@tanstack/react-query'
import { solicitar } from './http'
import { ESTADO, esEstadoTerminal, nombresEstado } from './ordenes'
import type { BarraDato } from '../components/GraficoBarras'
import type {
  DashboardResumenResponse,
  OrdenServicioReporteResponse,
  StockBajoResponse,
  VentaReporteResponse,
} from './tipos'

export type Periodo = 'Hoy' | 'Semana' | 'Mes'

export type RangoFechas = {
  fechaDesde: string
  fechaHasta: string
}

/**
 * Convierte el periodo elegido en el rango que espera la API.
 * «Semana» son los últimos 7 días contando hoy, y «Mes», los últimos 30.
 */
export function rangoDelPeriodo(periodo: Periodo, ahora: Date = new Date()): RangoFechas {
  const desde = new Date(ahora)
  desde.setHours(0, 0, 0, 0)

  if (periodo === 'Semana') {
    desde.setDate(desde.getDate() - 6)
  }
  if (periodo === 'Mes') {
    desde.setDate(desde.getDate() - 29)
  }

  const hasta = new Date(ahora)
  hasta.setHours(23, 59, 59, 999)

  return { fechaDesde: desde.toISOString(), fechaHasta: hasta.toISOString() }
}

export type FiltrosReporte = Partial<RangoFechas> & {
  estado?: number
  tecnicoId?: string
  clienteId?: string
}

/** Arma la ruta del reporte con los filtros que la API entiende. */
export function rutaReporte(ruta: string, filtros: FiltrosReporte = {}): string {
  const parametros = new URLSearchParams()
  if (filtros.fechaDesde) parametros.set('fechaDesde', filtros.fechaDesde)
  if (filtros.fechaHasta) parametros.set('fechaHasta', filtros.fechaHasta)
  if (filtros.estado !== undefined) parametros.set('estado', String(filtros.estado))
  if (filtros.tecnicoId) parametros.set('tecnicoId', filtros.tecnicoId)
  if (filtros.clienteId) parametros.set('clienteId', filtros.clienteId)

  const consulta = parametros.toString()
  return `${ruta}${consulta ? `?${consulta}` : ''}`
}

export const clavesReportes = {
  resumen: (rango?: RangoFechas) => ['dashboard', rango?.fechaDesde ?? 'todo'] as const,
  ordenes: (filtros: FiltrosReporte) => ['reportes', 'ordenes', filtros.fechaDesde ?? 'todo'] as const,
  ventas: (filtros: FiltrosReporte) => ['reportes', 'ventas', filtros.fechaDesde ?? 'todo'] as const,
  stockBajo: ['reportes', 'stock-bajo'] as const,
}

export function useResumenDashboard(rango?: RangoFechas) {
  return useQuery({
    queryKey: clavesReportes.resumen(rango),
    queryFn: () =>
      solicitar<DashboardResumenResponse>(rutaReporte('/dashboard/resumen', rango ?? {})),
  })
}

export function useReporteOrdenes(filtros: FiltrosReporte = {}) {
  return useQuery({
    queryKey: clavesReportes.ordenes(filtros),
    queryFn: () =>
      solicitar<OrdenServicioReporteResponse[]>(rutaReporte('/reportes/ordenes-servicio', filtros)),
  })
}

export function useReporteVentas(filtros: FiltrosReporte = {}) {
  return useQuery({
    queryKey: clavesReportes.ventas(filtros),
    queryFn: () => solicitar<VentaReporteResponse[]>(rutaReporte('/reportes/ventas', filtros)),
  })
}

export function useStockBajo() {
  return useQuery({
    queryKey: clavesReportes.stockBajo,
    queryFn: () => solicitar<StockBajoResponse[]>('/reportes/stock-bajo'),
  })
}

/** Órdenes que están físicamente en el taller, sin contar entregadas ni anuladas. */
export function enTaller(resumen: DashboardResumenResponse): number {
  const { abierta, diagnostico, aprobada, enProceso, lista } = resumen.ordenesServicio
  return abierta + diagnostico + aprobada + enProceso + lista
}

/**
 * El resumen del tablero, listo para el gráfico de barras.
 *
 * Va en el orden del flujo, no de mayor a menor: así se lee dónde se están
 * acumulando las órdenes. Entregada y Cancelada salen atenuadas porque son
 * contexto: lo que el taller mira es lo que todavía tiene adentro.
 */
export function barrasPorEstado(resumen: DashboardResumenResponse): BarraDato[] {
  const ordenes = resumen.ordenesServicio
  const conteos: readonly (readonly [number, number])[] = [
    [ESTADO.abierta, ordenes.abierta],
    [ESTADO.diagnostico, ordenes.diagnostico],
    [ESTADO.aprobada, ordenes.aprobada],
    [ESTADO.enProceso, ordenes.enProceso],
    [ESTADO.lista, ordenes.lista],
    [ESTADO.entregada, ordenes.entregada],
    [ESTADO.cancelada, ordenes.cancelada],
  ]

  return conteos.map(([estadoId, valor]) => ({
    etiqueta: nombresEstado[estadoId],
    valor,
    atenuada: esEstadoTerminal(estadoId),
  }))
}
