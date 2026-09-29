import { describe, expect, it } from 'vitest'
import { identificadorUnidad, lecturaIngresoOrden, lecturaMedidor, nombreTipoUnidad, ordenMidePorHoras, ultimaLecturaRegistrada } from './unidades'
import { referenciaOrden } from './formato'

describe('unidades', () => {
  it('nombra el tipo de unidad del enum del backend', () => {
    expect(nombreTipoUnidad({ tipoUnidadId: 2 })).toBe('Moto acuática')
    expect(nombreTipoUnidad({ tipoUnidadId: undefined })).toBe('Unidad')
  })

  it('muestra la lectura en km o en horas según el medidor', () => {
    expect(lecturaMedidor({ tipoMedidorId: 0, lecturaMedidorActual: null, kilometraje: 12450, horasUso: null })).toMatch(
      /^12.450 km$/,
    )
    expect(lecturaMedidor({ tipoMedidorId: 1, lecturaMedidorActual: 86, kilometraje: null, horasUso: null })).toBe('86 h')
    expect(lecturaMedidor({ tipoMedidorId: 1, lecturaMedidorActual: null, kilometraje: null, horasUso: null })).toBe('—')
  })

  it('usa la serie cuando la unidad no tiene placa', () => {
    expect(identificadorUnidad({ placa: '4521-7B', numeroSerieVIN: 'YAMA2231H122' })).toBe('4521-7B')
    expect(identificadorUnidad({ placa: null, numeroSerieVIN: 'YAMA2231H122' })).toBe('Serie YAMA2231H122')
  })

  it('deduce el medidor de la orden por su lectura o por el tipo de unidad', () => {
    expect(ordenMidePorHoras({ kilometrajeIngreso: 100, horasUsoIngreso: null, tipoUnidad: 'Generador' })).toBe(false)
    expect(ordenMidePorHoras({ kilometrajeIngreso: null, horasUsoIngreso: null, tipoUnidad: 'MotoAcuatica' })).toBe(true)
    expect(lecturaIngresoOrden({ kilometrajeIngreso: null, horasUsoIngreso: 310, tipoUnidad: 'Generador' })).toBe('310 h')
  })
})

describe('referenciaOrden', () => {
  it('prefiere el correlativo de la API', () => {
    expect(referenciaOrden({ id: 'abcdef12-0000', numeroOrden: 'OT-000123' })).toBe('OT-000123')
  })

  it('cae al inicio del id si no hay correlativo', () => {
    expect(referenciaOrden({ id: 'abcdef12-0000', numeroOrden: null })).toBe('#ABCDEF12')
    expect(referenciaOrden('abcdef12-0000')).toBe('#ABCDEF12')
  })
})

describe('ultimaLecturaRegistrada', () => {
  it('toma la mayor entre la unidad y sus órdenes anteriores', () => {
    const ordenes = [{ kilometrajeIngreso: 12000 }, { kilometrajeIngreso: 18900 }, { lecturaMedidorIngreso: 15000 }]
    expect(ultimaLecturaRegistrada(false, 17500, ordenes)).toBe(18900)
  })

  it('en horas lee las horas de uso, no los kilómetros', () => {
    const ordenes = [{ horasUsoIngreso: 86.5 }, { kilometrajeIngreso: 99999 }]
    expect(ultimaLecturaRegistrada(true, 80, ordenes)).toBe(86.5)
  })

  it('sin lecturas no hay mínimo', () => {
    expect(ultimaLecturaRegistrada(false, null, [])).toBeNull()
  })
})
