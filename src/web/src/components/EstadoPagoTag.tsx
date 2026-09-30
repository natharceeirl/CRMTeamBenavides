import { ESTADO_PAGO } from '../api/pagos'
import { EtiquetaEstado, type TonoEstado } from './EtiquetaEstado'

// Lo que se debe llama la atención; lo pagado queda sobrio.
const tonos: Record<string, TonoEstado> = {
  [ESTADO_PAGO.pendiente]: 'alerta',
  [ESTADO_PAGO.parcial]: 'suave',
  [ESTADO_PAGO.pagado]: 'hecho',
}

/** Pendiente, Parcial o Pagado, tal como lo calcula el backend. */
export function EstadoPagoTag({ estado }: Readonly<{ estado: string | null | undefined }>) {
  const nombre = estado ?? ESTADO_PAGO.pendiente
  return <EtiquetaEstado tono={tonos[nombre] ?? 'alerta'}>{nombre}</EtiquetaEstado>
}
