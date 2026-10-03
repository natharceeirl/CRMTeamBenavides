import { describe, expect, it } from 'vitest'
import { diaLocal, entero, fechaCorta, fechaHora, importe, porcentaje, referenciaOrden, soles } from './formato'

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

  it('escribe el margen con un decimal', () => {
    expect(porcentaje(32.456)).toMatch(/^32.5 %$/)
    expect(porcentaje(-4)).toMatch(/^-4.0 %$/)
  })

  it('da el día local, no el de UTC', () => {
    // A las 21:00 en Perú ya es el día siguiente en UTC.
    expect(diaLocal(new Date(2026, 9, 2, 21, 0))).toBe('2026-10-02')
    expect(fechaCorta(new Date(2026, 9, 2, 21, 0).toISOString())).toBe('02/10/2026')
    expect(fechaCorta(null)).toBe('—')
  })
})
