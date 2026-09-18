import { describe, expect, it } from 'vitest'
import { entero, fechaHora, importe, referenciaOrden, soles } from './formato'

describe('formato', () => {
  it('escribe los montos en soles con dos decimales', () => {
    expect(soles(1234.5)).toBe('S/ 1,234.50')
    expect(importe(0)).toBe('0.00')
  })

  it('separa los miles en los enteros', () => {
    expect(entero(12480)).toBe('12,480')
  })

  it('muestra una raya cuando no hay fecha', () => {
    expect(fechaHora(null)).toBe('—')
    expect(fechaHora(undefined)).toBe('—')
    expect(fechaHora('')).toBe('—')
  })

  it('convierte la fecha ISO de la API a dia/mes y hora de 24 horas', () => {
    // Sale en hora local, así que se comprueba la forma, no la hora exacta.
    expect(fechaHora('2026-09-18T13:45:00Z')).toMatch(/^\d{2}\/\d{2} \d{2}:\d{2}$/)
  })

  it('devuelve una raya si la fecha no se entiende', () => {
    expect(fechaHora('no es una fecha')).toBe('—')
  })

  it('arma la referencia de la orden con el inicio del id', () => {
    expect(referenciaOrden('64404ca3-6464-486a-b0aa-14d157fa1375')).toBe('#64404CA3')
  })
})
