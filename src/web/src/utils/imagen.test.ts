import { describe, expect, it } from 'vitest'
import { medidasAchicadas, pesoLegible } from './imagen'

describe('medidas de una foto achicada', () => {
  it('una foto horizontal de celular queda con 1920 px de ancho', () => {
    expect(medidasAchicadas(4000, 3000)).toEqual({ ancho: 1920, alto: 1440 })
  })

  it('una vertical queda con 1920 px de alto', () => {
    expect(medidasAchicadas(3000, 4000)).toEqual({ ancho: 1440, alto: 1920 })
  })

  it('una foto que ya es chica no se agranda', () => {
    expect(medidasAchicadas(800, 600)).toEqual({ ancho: 800, alto: 600 })
    expect(medidasAchicadas(1920, 1080)).toEqual({ ancho: 1920, alto: 1080 })
  })
})

describe('peso legible', () => {
  it('en KB hasta 1 MB y en MB con un decimal después, con punto decimal como en Perú', () => {
    expect(pesoLegible(421_888)).toBe('412 KB')
    expect(pesoLegible(3 * 1024 * 1024)).toBe('3 MB')
    expect(pesoLegible(2.4 * 1024 * 1024)).toBe('2.4 MB')
  })
})
