import { describe, expect, it } from 'vitest'
import type { TipoCambioResponse } from '../api/tipos'
import { esDeHoy, resumenTipoCambio, tipoDeCambio } from './tipoCambio'

const vigente = (fechaActualizacion: string): TipoCambioResponse => ({
  configurado: true,
  tipoCambio: 3.75,
  valorVenta: 3.75,
  valorCompra: 3.74,
  monedaBase: 'PEN',
  monedaExtranjera: 'USD',
  fechaActualizacion,
  ultimoUsuarioNombre: 'Gerencia',
})

describe('tipo de cambio', () => {
  it('se lee con tres o cuatro decimales', () => {
    expect(tipoDeCambio(3.75)).toBe('3.750')
    expect(tipoDeCambio(3.7525)).toBe('3.7525')
    expect(tipoDeCambio(null)).toBe('—')
  })

  it('en la barra muestra la fecha si el valor no es de hoy', () => {
    const hoy = new Date(2026, 8, 30, 10, 0)
    expect(esDeHoy(new Date(2026, 8, 30, 8, 0).toISOString(), hoy)).toBe(true)
    expect(resumenTipoCambio(vigente(new Date(2026, 8, 30, 8, 0).toISOString()), hoy)).toBe('US$ 3.750')
    expect(resumenTipoCambio(vigente(new Date(2026, 8, 28, 8, 0).toISOString()), hoy)).toBe('US$ 3.750 · del 28/09')
  })

  it('sin tipo de cambio registrado no muestra nada', () => {
    expect(resumenTipoCambio(undefined)).toBeNull()
    expect(resumenTipoCambio({ ...vigente('2026-09-30T13:00:00Z'), configurado: false })).toBeNull()
  })
})
