import { describe, expect, it } from 'vitest'
import { TIPO_DOCUMENTO_CLIENTE } from '../api/tipos'
import { ESTADO } from '../api/ordenes'
import { medidorPorTipoUnidad, motivoDocumentoInvalido, ordenAbiertaDeUnidad, tipoDocumentoSugerido } from './recepcion'

describe('medidorPorTipoUnidad', () => {
  it('moto acuática y generador van por horas; moto, cuatrimoto y otro, por kilómetros', () => {
    expect(medidorPorTipoUnidad(0)).toBe(0)
    expect(medidorPorTipoUnidad(1)).toBe(0)
    expect(medidorPorTipoUnidad(2)).toBe(1)
    expect(medidorPorTipoUnidad(3)).toBe(1)
    expect(medidorPorTipoUnidad(4)).toBe(0)
  })
})

describe('tipoDocumentoSugerido', () => {
  it('reconoce DNI y RUC por la cantidad de dígitos', () => {
    expect(tipoDocumentoSugerido('12345678')).toBe(TIPO_DOCUMENTO_CLIENTE.dni)
    expect(tipoDocumentoSugerido(' 20123456789 ')).toBe(TIPO_DOCUMENTO_CLIENTE.ruc)
    expect(tipoDocumentoSugerido('001234567')).toBe(TIPO_DOCUMENTO_CLIENTE.otro)
  })
})

describe('motivoDocumentoInvalido', () => {
  it('pide 8 dígitos al DNI y 11 al RUC', () => {
    expect(motivoDocumentoInvalido(TIPO_DOCUMENTO_CLIENTE.dni, '12345678')).toBeNull()
    expect(motivoDocumentoInvalido(TIPO_DOCUMENTO_CLIENTE.dni, '1234567')).toMatch(/8 dígitos/)
    expect(motivoDocumentoInvalido(TIPO_DOCUMENTO_CLIENTE.ruc, '12345678')).toMatch(/11 dígitos/)
    expect(motivoDocumentoInvalido(TIPO_DOCUMENTO_CLIENTE.otro, 'CE001234567')).toBeNull()
  })
})

describe('ordenAbiertaDeUnidad', () => {
  it('encuentra la que no está entregada ni cancelada', () => {
    const ordenes = [
      { numeroOrden: 'OT-1', estadoId: ESTADO.entregada },
      { numeroOrden: 'OT-2', estadoId: ESTADO.lista },
    ]
    expect(ordenAbiertaDeUnidad(ordenes)?.numeroOrden).toBe('OT-2')
  })

  it('sin órdenes abiertas, o sin datos todavía, no hay ninguna', () => {
    expect(ordenAbiertaDeUnidad([{ estadoId: ESTADO.cancelada }, { estadoId: ESTADO.entregada }])).toBeNull()
    expect(ordenAbiertaDeUnidad(undefined)).toBeNull()
  })
})
