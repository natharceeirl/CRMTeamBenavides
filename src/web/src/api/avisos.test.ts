import { describe, expect, it } from 'vitest'
import { avisoSegun, textoDeAviso } from './avisos'

describe('avisos de éxito', () => {
  it('muestra el texto fijo de la mutación', () => {
    expect(textoDeAviso({ exito: 'Cliente guardado' }, {}, {})).toBe('Cliente guardado')
  })

  it('arma el texto con lo que se mandó y lo que respondió la API', () => {
    const exito = avisoSegun<{ tipo: string }, { numeroOrden: string }>(
      ({ tipo }, respuesta) => `${tipo} ${respuesta.numeroOrden}`,
    )
    expect(textoDeAviso({ exito }, { tipo: 'Orden' }, { numeroOrden: 'OS-000021' })).toBe('Orden OS-000021')
  })

  it('una mutación sin aviso no muestra nada', () => {
    expect(textoDeAviso(undefined, {}, {})).toBeNull()
    expect(textoDeAviso({}, {}, {})).toBeNull()
  })
})
