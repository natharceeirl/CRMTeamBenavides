import { ESTADO_COMPRA, type CompraResumenResponse } from '../api/tipos'
import { ESTADO_PAGO_COMPRA } from '../api/compras'
import { EtiquetaEstado, type TonoEstado } from './EtiquetaEstado'

const tonos: Record<string, TonoEstado> = {
  [ESTADO_PAGO_COMPRA.pendiente]: 'alerta',
  [ESTADO_PAGO_COMPRA.parcial]: 'suave',
  [ESTADO_PAGO_COMPRA.pagada]: 'hecho',
}

/** Anulada, vencida o el estado de pago que calcula el backend. */
export function EstadoCompraTag({
  compra,
}: Readonly<{ compra: Pick<CompraResumenResponse, 'estado' | 'estadoPago' | 'vencida'> }>) {
  if (compra.estado === ESTADO_COMPRA.anulada) return <EtiquetaEstado tono="apagado">Anulada</EtiquetaEstado>
  if (compra.vencida) return <EtiquetaEstado tono="alerta">Vencida</EtiquetaEstado>
  const estado = compra.estadoPago ?? ESTADO_PAGO_COMPRA.pendiente
  return <EtiquetaEstado tono={tonos[estado] ?? 'neutro'}>{estado}</EtiquetaEstado>
}
