import { nombresEstadoCita } from '../api/citas'
import { ESTADO_CITA, type EstadoCita } from '../api/tipos'
import { EtiquetaEstado, type TonoEstado } from './EtiquetaEstado'

// Pendiente pide confirmarla; «No asistió» es el desenlace malo, a medias entre cerrado y alerta.
const tonos: Record<EstadoCita, TonoEstado> = {
  [ESTADO_CITA.pendiente]: 'alerta',
  [ESTADO_CITA.confirmada]: 'neutro',
  [ESTADO_CITA.enTaller]: 'neutro',
  [ESTADO_CITA.completada]: 'hecho',
  [ESTADO_CITA.cancelada]: 'apagado',
  [ESTADO_CITA.noAsistio]: 'suave',
}

export function EstadoCitaTag({ estado }: Readonly<{ estado: EstadoCita }>) {
  return <EtiquetaEstado tono={tonos[estado] ?? 'neutro'}>{nombresEstadoCita[estado] ?? 'Desconocido'}</EtiquetaEstado>
}
