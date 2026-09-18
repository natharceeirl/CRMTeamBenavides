import { describe, expect, it } from 'vitest'
import { MOVIMIENTO, nombresMovimiento, rutaProductos } from './inventario'

describe('rutaProductos', () => {
  it('sin filtros pide el catálogo completo', () => {
    expect(rutaProductos()).toBe('/productos')
    expect(rutaProductos({})).toBe('/productos')
  })

  it('manda cada filtro que la API entiende', () => {
    expect(rutaProductos({ categoriaId: 'abc' })).toBe('/productos?categoriaId=abc')
    expect(rutaProductos({ bajoStock: true })).toBe('/productos?bajoStock=true')
  })

  it('ignora la búsqueda vacía o en blanco', () => {
    expect(rutaProductos({ busqueda: '   ' })).toBe('/productos')
  })

  it('recorta la búsqueda y la codifica', () => {
    expect(rutaProductos({ busqueda: '  filtro aceite  ' })).toBe('/productos?busqueda=filtro+aceite')
  })

  it('no manda bajoStock cuando es falso, para no filtrar de más', () => {
    expect(rutaProductos({ bajoStock: false, categoriaId: 'abc' })).toBe('/productos?categoriaId=abc')
  })

  it('combina los tres filtros', () => {
    expect(rutaProductos({ categoriaId: 'abc', busqueda: 'aceite', bajoStock: true })).toBe(
      '/productos?categoriaId=abc&busqueda=aceite&bajoStock=true',
    )
  })
})

describe('tipos de movimiento', () => {
  it('coinciden con el enum del backend', () => {
    expect(MOVIMIENTO.entrada).toBe(0)
    expect(MOVIMIENTO.salida).toBe(1)
    expect(MOVIMIENTO.ajuste).toBe(2)
    expect(nombresMovimiento[MOVIMIENTO.salida]).toBe('Salida')
  })
})
