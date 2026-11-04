import { describe, expect, it } from 'vitest'
import { MONEDA_PRECIO, precioDeLinea, textoPrecioLista, textoPrecioRegistrado, totalDeLinea } from './precios'

describe('precios de los repuestos', () => {
  it('muestra el precio como se registró, con «+ IGV» si no lo incluye', () => {
    expect(textoPrecioRegistrado({ precioVenta: 25, monedaPrecio: MONEDA_PRECIO.usd, precioIncluyeIgv: true })).toBe('US$ 25.00')
    expect(textoPrecioRegistrado({ precioVenta: 29.66, monedaPrecio: MONEDA_PRECIO.pen, precioIncluyeIgv: false })).toBe(
      'S/ 29.66 + IGV',
    )
  })

  it('el precio de lista en soles avisa cuando falta el tipo de cambio', () => {
    expect(textoPrecioLista({ precioListaSoles: 94, precioIncluyeIgv: true })).toBe('S/ 94.00')
    expect(textoPrecioLista({ precioListaSoles: null, precioIncluyeIgv: true })).toBe('sin tipo de cambio')
  })

  it('el total de una línea con IGV es el precio por la cantidad; sin IGV le suma el 18 %', () => {
    expect(totalDeLinea(10, 3, true)).toBe(30)
    expect(totalDeLinea(10, 3, false)).toBeCloseTo(35.4)
  })

  it('una línea cobrada con IGV se edita con IGV', () => {
    expect(precioDeLinea({ precioUnitario: 8.47, precioUnitarioConIgv: 10 })).toBe(10)
    expect(precioDeLinea({ precioUnitario: 29.66, precioUnitarioConIgv: null })).toBe(29.66)
  })
})
