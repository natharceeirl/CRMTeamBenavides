import { describe, expect, it } from 'vitest'
import {
  ESTADO_VENTA,
  nombresEstadoVenta,
  puedeAnular,
  puedeAnularComprobante,
  puedeConfirmar,
  puedeRegistrarComprobante,
  rutaVentas,
} from './ventas'

describe('estados de venta', () => {
  it('coinciden con el enum del backend', () => {
    expect(ESTADO_VENTA.cotizacion).toBe(0)
    expect(ESTADO_VENTA.confirmada).toBe(1)
    expect(ESTADO_VENTA.anulada).toBe(2)
    expect(nombresEstadoVenta[ESTADO_VENTA.cotizacion]).toBe('Cotización')
  })

  it('solo se confirma una cotización', () => {
    expect(puedeConfirmar(ESTADO_VENTA.cotizacion)).toBe(true)
    expect(puedeConfirmar(ESTADO_VENTA.confirmada)).toBe(false)
    expect(puedeConfirmar(ESTADO_VENTA.anulada)).toBe(false)
  })

  it('se anula todo menos lo ya anulado', () => {
    expect(puedeAnular(ESTADO_VENTA.cotizacion)).toBe(true)
    expect(puedeAnular(ESTADO_VENTA.confirmada)).toBe(true)
    expect(puedeAnular(ESTADO_VENTA.anulada)).toBe(false)
  })
})

describe('rutaVentas', () => {
  it('sin filtros pide todo', () => {
    expect(rutaVentas()).toBe('/ventas')
  })

  it('manda el estado como número, incluido el cero', () => {
    // Cotización es 0: si se comprobara como booleano, se perdería el filtro.
    expect(rutaVentas({ estado: ESTADO_VENTA.cotizacion })).toBe('/ventas?estado=0')
    expect(rutaVentas({ estado: ESTADO_VENTA.anulada })).toBe('/ventas?estado=2')
  })

  it('combina cliente y orden de servicio', () => {
    expect(rutaVentas({ clienteId: 'c1', ordenServicioId: 'o1' })).toBe(
      '/ventas?clienteId=c1&ordenServicioId=o1',
    )
  })
})

describe('comprobantes', () => {
  it('solo se registra en una venta confirmada', () => {
    expect(puedeRegistrarComprobante(ESTADO_VENTA.confirmada, false)).toBe(true)
    expect(puedeRegistrarComprobante(ESTADO_VENTA.cotizacion, false)).toBe(false)
    expect(puedeRegistrarComprobante(ESTADO_VENTA.anulada, false)).toBe(false)
  })

  it('no se registra dos veces sobre la misma venta', () => {
    // El backend rechaza el duplicado; la web ni siquiera ofrece el formulario.
    expect(puedeRegistrarComprobante(ESTADO_VENTA.confirmada, true)).toBe(false)
  })

  it('solo se anula el comprobante emitido', () => {
    expect(puedeAnularComprobante('Emitido')).toBe(true)
    expect(puedeAnularComprobante('Anulado')).toBe(false)
    expect(puedeAnularComprobante(null)).toBe(false)
    expect(puedeAnularComprobante(undefined)).toBe(false)
  })
})
