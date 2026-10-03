import { ESTADO, fechaIngresoOrden } from '../api/ordenes'

export type SituacionPaso = 'hecho' | 'actual' | 'pendiente'

export type PasoAvance = {
  titulo: string
  situacion: SituacionPaso
  fecha: string | null
  /** La fecha es la entrega estimada: todavía no pasó. */
  estimada: boolean
}

type OrdenConAvance = {
  estadoId: number
  fechaApertura: string
  fechaIngreso?: string | null
  fechaEstimadaEntrega?: string | null
  historial?: { estadoNuevoId: number; fechaCambio: string }[] | null
}

/** Los estados con los nombres que entiende el cliente (docs/referencias/app-cliente.html). */
const PASOS: readonly (readonly [number, string])[] = [
  [ESTADO.abierta, 'Recibida'],
  [ESTADO.diagnostico, 'Diagnóstico'],
  [ESTADO.aprobada, 'Presupuesto aprobado'],
  [ESTADO.enProceso, 'En reparación'],
  [ESTADO.lista, 'Lista para recoger'],
  [ESTADO.entregada, 'Entregada'],
]

/**
 * Avance de la orden para el cliente, igual que en la app: cada paso con la última
 * vez que la orden llegó a él. Si vuelve de «Lista» a «En proceso» (reingreso),
 * «Lista» queda pendiente otra vez. La entrega estimada va en «Lista para recoger».
 * Una orden cancelada no tiene avance: quien la muestra lo dice aparte.
 */
export function pasosDeAvance(orden: OrdenConAvance): PasoAvance[] {
  const ultimaVez = (estado: number): string | null => {
    let fecha: string | null = null
    for (const cambio of orden.historial ?? []) {
      if (cambio.estadoNuevoId === estado && (!fecha || new Date(cambio.fechaCambio) > new Date(fecha))) {
        fecha = cambio.fechaCambio
      }
    }
    return fecha
  }

  return PASOS.map(([estado, titulo]) => {
    let situacion: SituacionPaso = 'pendiente'
    if (estado < orden.estadoId) situacion = 'hecho'
    if (estado === orden.estadoId) situacion = 'actual'

    if (situacion === 'pendiente') {
      const estimada = estado === ESTADO.lista ? (orden.fechaEstimadaEntrega ?? null) : null
      return { titulo, situacion, fecha: estimada, estimada: estimada !== null }
    }
    const fecha = estado === ESTADO.abierta ? fechaIngresoOrden(orden) : ultimaVez(estado)
    return { titulo, situacion, fecha, estimada: false }
  })
}
