import { describe, expect, it } from 'vitest'
import { MONEDA_COMPRA, TIPO_AFECTACION_IGV } from '../api/tipos'
import { calcularLinea, calcularTotales, montoEnMoneda, redondear } from './compras'

const { gravado, exonerado, inafecto } = TIPO_AFECTACION_IGV

// Los mismos casos que se probaron contra la API: la vista previa tiene que dar lo que guarda el backend.
describe('calcularLinea', () => {
  it('sin IGV: el IGV va encima del subtotal', () => {
    expect(calcularLinea({ cantidad: 10, precioUnitario: 20, tipoAfectacionIgv: gravado }, 18, false)).toEqual({
      subtotal: 200,
      igv: 36,
      total: 236,
    })
  })

  it('con IGV: el importe es el total y el subtotal sale de dividirlo', () => {
    expect(calcularLinea({ cantidad: 2, precioUnitario: 11.8, tipoAfectacionIgv: gravado }, 18, true)).toEqual({
      subtotal: 20,
      igv: 3.6,
      total: 23.6,
    })
    expect(calcularLinea({ cantidad: 3, precioUnitario: 11.8, tipoAfectacionIgv: gravado }, 18, true)).toEqual({
      subtotal: 30,
      igv: 5.4,
      total: 35.4,
    })
  })

  it('exonerado e inafecto no llevan IGV, con o sin IGV incluido', () => {
    expect(calcularLinea({ cantidad: 2, precioUnitario: 15, tipoAfectacionIgv: exonerado }, 18, false)).toEqual({
      subtotal: 30,
      igv: 0,
      total: 30,
    })
    expect(calcularLinea({ cantidad: 1, precioUnitario: 100, tipoAfectacionIgv: inafecto }, 18, true)).toEqual({
      subtotal: 100,
      igv: 0,
      total: 100,
    })
  })

  it('con IGV en 0 % no hay impuesto', () => {
    expect(calcularLinea({ cantidad: 1, precioUnitario: 50, tipoAfectacionIgv: gravado }, 0, false).igv).toBe(0)
  })

  it('una línea a medio escribir cuenta como 0', () => {
    expect(calcularLinea({ cantidad: Number.NaN, precioUnitario: 10, tipoAfectacionIgv: gravado }, 18, false).total).toBe(0)
  })
})

describe('calcularTotales', () => {
  it('suma por afectación: flete gravado y repuesto exonerado', () => {
    expect(
      calcularTotales(
        [
          { cantidad: 1, precioUnitario: 50, tipoAfectacionIgv: gravado },
          { cantidad: 2, precioUnitario: 15, tipoAfectacionIgv: exonerado },
        ],
        18,
        false,
      ),
    ).toEqual({ gravado: 50, exonerado: 30, inafecto: 0, igv: 9, total: 89 })
  })
})

describe('redondear', () => {
  it('la mitad sube, como MidpointRounding.AwayFromZero', () => {
    expect(redondear(1.005)).toBe(1.01)
    expect(redondear(2.675)).toBe(2.68)
    expect(redondear(0.125)).toBe(0.13)
    expect(redondear(3 * 11.8)).toBe(35.4)
  })
})

describe('montoEnMoneda', () => {
  it('pone el símbolo de la moneda de la compra', () => {
    expect(montoEnMoneda(MONEDA_COMPRA.pen, 10)).toMatch(/^S\/ 10[.,]00$/)
    expect(montoEnMoneda(MONEDA_COMPRA.usd, 10)).toMatch(/^US\$ 10[.,]00$/)
  })
})
