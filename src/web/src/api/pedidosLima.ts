import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { avisoSegun, type AvisoExito } from './avisos'
import { clavesInventario } from './inventario'
import { clavesCaja } from './caja'
import {
  ESTADO_PEDIDO_LIMA,
  type EstadoPedidoLima,
  type ActualizarPedidoLimaRequest,
  type ActualizarPrecioDetallePedidoLimaRequest,
  type CrearPedidoLimaRequest,
  type PagoResponse,
  type PedidoLimaResponse,
  type RegistrarPagoRequest,
} from './tipos'

export const nombresEstadoPedidoLima: Record<EstadoPedidoLima, string> = {
  [ESTADO_PEDIDO_LIMA.pendiente]: 'Pendiente',
  [ESTADO_PEDIDO_LIMA.confirmado]: 'Confirmado',
  [ESTADO_PEDIDO_LIMA.enPreparacion]: 'En preparación',
  [ESTADO_PEDIDO_LIMA.enTransito]: 'En tránsito',
  [ESTADO_PEDIDO_LIMA.recibido]: 'Recibido',
  [ESTADO_PEDIDO_LIMA.entregado]: 'Entregado',
  [ESTADO_PEDIDO_LIMA.cancelado]: 'Cancelado',
}

/** Para el cliente, como en la app: «En camino» y «Listo para recoger». */
export const nombresEstadoPedidoCliente: Record<EstadoPedidoLima, string> = {
  ...nombresEstadoPedidoLima,
  [ESTADO_PEDIDO_LIMA.enTransito]: 'En camino',
  [ESTADO_PEDIDO_LIMA.recibido]: 'Listo para recoger',
}

export const esPedidoFinal = (estado: EstadoPedidoLima) =>
  estado === ESTADO_PEDIDO_LIMA.entregado || estado === ESTADO_PEDIDO_LIMA.cancelado

/** El camino del pedido, en el orden del backend. Cancelar va aparte, con motivo. */
const CAMINO_PEDIDO: EstadoPedidoLima[] = [
  ESTADO_PEDIDO_LIMA.pendiente,
  ESTADO_PEDIDO_LIMA.confirmado,
  ESTADO_PEDIDO_LIMA.enPreparacion,
  ESTADO_PEDIDO_LIMA.enTransito,
  ESTADO_PEDIDO_LIMA.recibido,
  ESTADO_PEDIDO_LIMA.entregado,
]

/** El paso siguiente del pedido: los estados van en orden y no se retrocede. */
export const siguienteEstadoPedido = (estado: EstadoPedidoLima): EstadoPedidoLima | null =>
  esPedidoFinal(estado) ? null : (CAMINO_PEDIDO[CAMINO_PEDIDO.indexOf(estado) + 1] ?? null)

export type FiltrosPedidosLima = { estado?: EstadoPedidoLima; clienteId?: string }

export function rutaPedidosLima(filtros: FiltrosPedidosLima, base = '/pedidos-lima'): string {
  const parametros = new URLSearchParams()
  if (filtros.estado) parametros.set('estado', filtros.estado)
  if (filtros.clienteId) parametros.set('clienteId', filtros.clienteId)
  const consulta = parametros.toString()
  return consulta ? `${base}?${consulta}` : base
}

export const clavesPedidosLima = {
  todas: ['pedidos-lima'] as const,
  lista: (filtros: FiltrosPedidosLima) => ['pedidos-lima', 'lista', filtros] as const,
  uno: (id: string) => ['pedidos-lima', id] as const,
}

export function usePedidosLima(filtros: FiltrosPedidosLima) {
  return useQuery({
    queryKey: clavesPedidosLima.lista(filtros),
    queryFn: () => solicitar<PedidoLimaResponse[]>(rutaPedidosLima(filtros)),
  })
}

export function usePedidoLima(id: string | null) {
  return useQuery({
    queryKey: clavesPedidosLima.uno(id ?? ''),
    queryFn: () => solicitar<PedidoLimaResponse>(`/pedidos-lima/${id}`),
    enabled: Boolean(id),
  })
}

/** Entregar descuenta stock: también se refresca el inventario. */
function useMutacionPedido<V>(mutationFn: (variables: V) => Promise<PedidoLimaResponse>, exito: AvisoExito) {
  const consultas = useQueryClient()
  return useMutation({
    meta: { exito },
    mutationFn,
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesPedidosLima.todas }),
        consultas.invalidateQueries({ queryKey: clavesInventario.productos }),
      ])
    },
  })
}

export const useCrearPedidoLima = () =>
  useMutacionPedido(
    (datos: CrearPedidoLimaRequest) => solicitar<PedidoLimaResponse>('/pedidos-lima', { metodo: 'POST', cuerpo: datos }),
    'Pedido registrado',
  )

export const useActualizarPedidoLima = () =>
  useMutacionPedido(
    ({ id, datos }: { id: string; datos: ActualizarPedidoLimaRequest }) =>
      solicitar<PedidoLimaResponse>(`/pedidos-lima/${id}`, { metodo: 'PUT', cuerpo: datos }),
    'Datos del envío guardados',
  )

export const useCambiarEstadoPedidoLima = () =>
  useMutacionPedido(
    ({ id, nuevoEstado, observacion }: { id: string; nuevoEstado: EstadoPedidoLima; observacion: string | null }) =>
      solicitar<PedidoLimaResponse>(`/pedidos-lima/${id}/estado`, { metodo: 'PUT', cuerpo: { nuevoEstado, observacion } }),
    avisoSegun<{ nuevoEstado: EstadoPedidoLima }>(({ nuevoEstado }) => `Pedido: ${nombresEstadoPedidoLima[nuevoEstado].toLowerCase()}`),
  )

export const useCancelarPedidoLima = () =>
  useMutacionPedido(
    ({ id, motivo }: { id: string; motivo: string }) =>
      solicitar<PedidoLimaResponse>(`/pedidos-lima/${id}/cancelar`, { metodo: 'PUT', cuerpo: { motivoCancelacion: motivo } }),
    'Pedido cancelado',
  )

/**
 * Corrige cantidad o precio de un repuesto del pedido. Otro precio que el de lista
 * deja el pedido pendiente de Gerencia; volver al de lista lo libera. La clave de
 * aprobaciones se escribe aquí porque api/aprobaciones importa este archivo.
 */
export function useActualizarDetallePedidoLima() {
  const consultas = useQueryClient()
  return useMutation({
    meta: { exito: 'Repuesto del pedido actualizado' },
    mutationFn: ({ id, detalleId, datos }: { id: string; detalleId: string; datos: ActualizarPrecioDetallePedidoLimaRequest }) =>
      solicitar<PedidoLimaResponse>(`/pedidos-lima/${id}/detalles/${detalleId}`, { metodo: 'PUT', cuerpo: datos }),
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesPedidosLima.todas }),
        consultas.invalidateQueries({ queryKey: ['aprobaciones'] }),
      ])
    },
  })
}

/** Adelanto mientras el pedido viene en camino, o pago del saldo. Ambos entran solos a la caja abierta. */
export function useRegistrarPagoPedidoLima() {
  const consultas = useQueryClient()
  return useMutation({
    meta: {
      exito: avisoSegun<{ datos: RegistrarPagoRequest }>(({ datos }) =>
        datos.esAnticipo ? 'Adelanto registrado' : 'Pago registrado',
      ),
    },
    mutationFn: ({ id, datos }: { id: string; datos: RegistrarPagoRequest }) =>
      solicitar<PagoResponse>(`/pedidos-lima/${id}/${datos.esAnticipo ? 'adelanto' : 'pagos'}`, {
        metodo: 'POST',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesPedidosLima.todas }),
        consultas.invalidateQueries({ queryKey: clavesCaja.todas }),
      ])
    },
  })
}
