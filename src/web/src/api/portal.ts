import { useQuery } from '@tanstack/react-query'
import { solicitar } from './http'

export type PortalResumen = {
  clienteId: string
  clienteNombre: string
  cantidadUnidades: number
  cantidadOrdenesActivas: number
  cantidadPresupuestosPendientes: number
  saldoPendienteTotal: number
}

export type PortalComprobante = {
  id: string
  ventaId: string
  ordenServicioId: string | null
  numeroOrden: string | null
  tipo: string
  serie: string | null
  numero: string | null
  fecha: string
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  porcentajeIgv: number
  montoIgv: number
  total: number
  estado: string
  metodoPagoPrincipal: string | null
  observaciones: string | null
}

export type HistorialServicioItem = {
  id: string
  descripcion: string
  servicioNombre: string | null
  productoCodigo: string | null
  tipoItem: number
  tipoItemNombre: string
  cantidad: number
  precioUnitario: number
  total: number
}

export type AtencionServicioUnidad = {
  ordenServicioId: string
  numeroOrden: string | null
  fechaIngreso: string
  fechaSalida: string | null
  estado: string
  estadoId: number
  kilometrajeIngreso: number | null
  horasUsoIngreso: number | null
  lecturaMedidorIngreso: number | null
  tipoMedidor: string | null
  motivoFalla: string | null
  solucion: string | null
  observacionesCliente: string | null
  total: number
  items: HistorialServicioItem[]
}

export const clavesPortal = {
  todas: ['portal'] as const,
  resumen: () => [...clavesPortal.todas, 'resumen'] as const,
  comprobantes: () => [...clavesPortal.todas, 'comprobantes'] as const,
  historialUnidad: (vehiculoId: string) => [...clavesPortal.todas, 'unidades', vehiculoId, 'historial'] as const,
}

export function usePortalResumen() {
  return useQuery({
    queryKey: clavesPortal.resumen(),
    queryFn: () => solicitar<PortalResumen>('/portal/resumen'),
  })
}

export function usePortalComprobantes() {
  return useQuery({
    queryKey: clavesPortal.comprobantes(),
    queryFn: () => solicitar<PortalComprobante[]>('/portal/comprobantes'),
  })
}

export function useHistorialServicioUnidad(vehiculoId: string | null) {
  return useQuery({
    queryKey: vehiculoId ? clavesPortal.historialUnidad(vehiculoId) : ['historial-nulo'],
    queryFn: () => (vehiculoId ? solicitar<AtencionServicioUnidad[]>(`/vehiculos/${vehiculoId}/historial-servicio`) : Promise.resolve([])),
    enabled: Boolean(vehiculoId),
  })
}
