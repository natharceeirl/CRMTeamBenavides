import { describe, expect, it } from 'vitest'
import { esPedidoFinal, rutaPedidosLima, siguienteEstadoPedido } from './pedidosLima'
import { ESTADO_PEDIDO_LIMA } from './tipos'

describe('siguienteEstadoPedido', () => {
  it('avanza un paso y no retrocede', () => {
    expect(siguienteEstadoPedido(ESTADO_PEDIDO_LIMA.pendiente)).toBe(ESTADO_PEDIDO_LIMA.confirmado)
    expect(siguienteEstadoPedido(ESTADO_PEDIDO_LIMA.enTransito)).toBe(ESTADO_PEDIDO_LIMA.recibido)
    expect(siguienteEstadoPedido(ESTADO_PEDIDO_LIMA.recibido)).toBe(ESTADO_PEDIDO_LIMA.entregado)
  })

  it('no sigue después de entregado ni de cancelado', () => {
    expect(siguienteEstadoPedido(ESTADO_PEDIDO_LIMA.entregado)).toBeNull()
    expect(siguienteEstadoPedido(ESTADO_PEDIDO_LIMA.cancelado)).toBeNull()
    expect(esPedidoFinal(ESTADO_PEDIDO_LIMA.enTransito)).toBe(false)
  })
})

describe('rutaPedidosLima', () => {
  it('agrega el estado solo si se filtra', () => {
    expect(rutaPedidosLima({})).toBe('/pedidos-lima')
    expect(rutaPedidosLima({ estado: ESTADO_PEDIDO_LIMA.pendiente }, '/pedidos-lima/exportar-excel')).toBe(
      '/pedidos-lima/exportar-excel?estado=Pendiente',
    )
  })
})
