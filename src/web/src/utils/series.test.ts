import { describe, expect, it } from 'vitest'
import {
  claveDelDia,
  etiquetaDelDia,
  indicesVisibles,
  serieDiaria,
  ticksDeEje,
  topeDeEje,
} from './series'

/** Un rango local, como el que arma `rangoDelPeriodo`. */
const rango = (desde: string, hasta: string) => ({
  fechaDesde: new Date(desde).toISOString(),
  fechaHasta: new Date(hasta).toISOString(),
})

type Venta = { fecha: string; total: number }

const venta = (fecha: string, total: number): Venta => ({
  fecha: new Date(fecha).toISOString(),
  total,
})

const serieDeVentas = (ventas: Venta[], desde: string, hasta: string) =>
  serieDiaria(
    ventas,
    (item) => item.fecha,
    (item) => item.total,
    rango(desde, hasta),
  )

describe('claveDelDia y etiquetaDelDia', () => {
  it('arma la clave con ceros a la izquierda', () => {
    expect(claveDelDia(new Date(2026, 8, 5, 23, 59))).toBe('2026-09-05')
  })

  it('escribe el día como dia/mes', () => {
    expect(etiquetaDelDia('2026-09-05')).toBe('05/09')
  })
})

describe('serieDiaria', () => {
  it('deja un punto por día del rango, aunque no haya datos', () => {
    const serie = serieDeVentas([], '2026-09-19T00:00:00', '2026-09-21T23:59:59')

    expect(serie.map((punto) => punto.dia)).toEqual(['2026-09-19', '2026-09-20', '2026-09-21'])
    expect(serie.every((punto) => punto.valor === 0)).toBe(true)
  })

  it('suma varios registros del mismo día', () => {
    const serie = serieDeVentas(
      [venta('2026-09-20T09:00:00', 100), venta('2026-09-20T17:30:00', 250)],
      '2026-09-19T00:00:00',
      '2026-09-21T23:59:59',
    )

    expect(serie.find((punto) => punto.dia === '2026-09-20')?.valor).toBe(350)
    expect(serie.find((punto) => punto.dia === '2026-09-19')?.valor).toBe(0)
  })

  it('descarta lo que cae fuera del rango', () => {
    const serie = serieDeVentas(
      [venta('2026-09-01T09:00:00', 999), venta('2026-09-20T09:00:00', 100)],
      '2026-09-19T00:00:00',
      '2026-09-21T23:59:59',
    )

    expect(serie).toHaveLength(3)
    expect(serie.reduce((suma, punto) => suma + punto.valor, 0)).toBe(100)
  })

  it('aguanta fechas inválidas sin romperse', () => {
    const serie = serieDeVentas(
      [{ fecha: 'no es una fecha', total: 50 }, venta('2026-09-20T09:00:00', 100)],
      '2026-09-19T00:00:00',
      '2026-09-21T23:59:59',
    )

    expect(serie.reduce((suma, punto) => suma + punto.valor, 0)).toBe(100)
  })

  it('devuelve vacío si el rango está al revés', () => {
    expect(serieDeVentas([], '2026-09-21T00:00:00', '2026-09-19T00:00:00')).toEqual([])
  })

  it('un rango de un solo día da un solo punto', () => {
    const serie = serieDeVentas(
      [venta('2026-09-21T10:00:00', 80)],
      '2026-09-21T00:00:00',
      '2026-09-21T23:59:59',
    )

    expect(serie).toHaveLength(1)
    expect(serie[0]).toMatchObject({ dia: '2026-09-21', etiqueta: '21/09', valor: 80 })
  })

  it('no se salta días al cruzar el cambio de mes', () => {
    const serie = serieDeVentas([], '2026-09-29T00:00:00', '2026-10-02T23:59:59')

    expect(serie.map((punto) => punto.etiqueta)).toEqual(['29/09', '30/09', '01/10', '02/10'])
  })
})

describe('ticksDeEje', () => {
  it('arranca en cero y termina en un número redondo', () => {
    expect(ticksDeEje(7, { entero: true })).toEqual([0, 2, 4, 6, 8])
  })

  it('usa medios escalones cuando el valor no es entero', () => {
    expect(ticksDeEje(10)).toEqual([0, 2.5, 5, 7.5, 10])
  })

  it('nunca da pasos menores a uno si se piden enteros', () => {
    expect(ticksDeEje(2, { entero: true })).toEqual([0, 1, 2])
  })

  it('da un eje usable sin datos', () => {
    expect(ticksDeEje(0)).toEqual([0, 1])
    expect(ticksDeEje(Number.NaN)).toEqual([0, 1])
    expect(ticksDeEje(-5)).toEqual([0, 1])
  })

  it('cubre siempre el máximo', () => {
    for (const maximo of [1, 3, 17, 42, 99, 1234, 87500]) {
      const cortes = ticksDeEje(maximo)
      expect(topeDeEje(cortes)).toBeGreaterThanOrEqual(maximo)
    }
  })

  it('no arrastra decimales sucios', () => {
    expect(ticksDeEje(0.9)).toEqual([0, 0.25, 0.5, 0.75, 1])
  })
})

describe('topeDeEje', () => {
  it('es el último corte', () => {
    expect(topeDeEje([0, 5, 10])).toBe(10)
  })

  it('sin cortes no devuelve cero, que dejaría el eje sin altura', () => {
    expect(topeDeEje([])).toBe(1)
  })
})

describe('indicesVisibles', () => {
  it('con pocos días los etiqueta todos', () => {
    expect(indicesVisibles(4)).toEqual([0, 1, 2, 3])
  })

  it('con un mes reparte unas pocas y no las amontona', () => {
    const indices = indicesVisibles(30)

    expect(indices).toHaveLength(6)
    expect(indices[0]).toBe(0)
    expect(indices[indices.length - 1]).toBe(29)
  })

  it('siempre incluye el primero y el último', () => {
    for (const cantidad of [1, 2, 7, 15, 31, 90]) {
      const indices = indicesVisibles(cantidad)
      expect(indices[0]).toBe(0)
      expect(indices[indices.length - 1]).toBe(cantidad - 1)
    }
  })

  it('no repite posiciones', () => {
    const indices = indicesVisibles(8)
    expect(new Set(indices).size).toBe(indices.length)
  })

  it('sin datos no etiqueta nada', () => {
    expect(indicesVisibles(0)).toEqual([])
  })
})
