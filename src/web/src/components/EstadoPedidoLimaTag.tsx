import { nombresEstadoPedidoLima } from '../api/pedidosLima'
import { ESTADO_PEDIDO_LIMA, type EstadoPedidoLima } from '../api/tipos'
import { EtiquetaEstado, type TonoEstado } from './EtiquetaEstado'

// Pendiente y Recibido piden una acción (confirmar, entregar); lo demás es camino.
const tonos: Record<EstadoPedidoLima, TonoEstado> = {
  [ESTADO_PEDIDO_LIMA.pendiente]: 'alerta',
  [ESTADO_PEDIDO_LIMA.confirmado]: 'neutro',
  [ESTADO_PEDIDO_LIMA.enPreparacion]: 'neutro',
  [ESTADO_PEDIDO_LIMA.enTransito]: 'neutro',
  [ESTADO_PEDIDO_LIMA.recibido]: 'alerta',
  [ESTADO_PEDIDO_LIMA.entregado]: 'hecho',
  [ESTADO_PEDIDO_LIMA.cancelado]: 'apagado',
}

type Props = {
  estado: EstadoPedidoLima
  nombres?: Record<EstadoPedidoLima, string>
}

export function EstadoPedidoLimaTag({ estado, nombres = nombresEstadoPedidoLima }: Readonly<Props>) {
  return <EtiquetaEstado tono={tonos[estado] ?? 'neutro'}>{nombres[estado] ?? 'Desconocido'}</EtiquetaEstado>
}
