import { describe, expect, it } from 'vitest'
import { esFallaDeCarga } from './errores'

describe('esFallaDeCarga', () => {
  it('reconoce la descarga fallida de una pantalla en cada navegador', () => {
    expect(esFallaDeCarga(new TypeError('Failed to fetch dynamically imported module: https://x/assets/Caja-1a2b.js'))).toBe(true)
    expect(esFallaDeCarga(new TypeError('error loading dynamically imported module: https://x/assets/Caja-1a2b.js'))).toBe(true)
    expect(esFallaDeCarga(new TypeError('Importing a module script failed.'))).toBe(true)

    const chunk = new Error('Loading chunk 3 failed')
    chunk.name = 'ChunkLoadError'
    expect(esFallaDeCarga(chunk)).toBe(true)
  })

  it('no confunde un error de la pantalla con una descarga fallida', () => {
    expect(esFallaDeCarga(new TypeError("Cannot read properties of undefined (reading 'nombre')"))).toBe(false)
    expect(esFallaDeCarga('Failed to fetch dynamically imported module')).toBe(false)
    expect(esFallaDeCarga(null)).toBe(false)
  })
})
