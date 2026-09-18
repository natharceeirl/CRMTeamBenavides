import { describe, expect, it } from 'vitest'
import { enTaller, rangoDelPeriodo, rutaReporte } from './reportes'
import type { DashboardResumenResponse } from './tipos'

const diasDeDiferencia = (desde: string, hasta: string) =>
  Math.round((new Date(hasta).getTime() - new Date(desde).getTime()) / (24 * 60 * 60 * 1000))

describe('rangoDelPeriodo', () => {
  const ahora = new Date('2026-09-18T15:30:00')

  it('«Hoy» va del arranque del día a su final', () => {
    const { fechaDesde, fechaHasta } = rangoDelPeriodo('Hoy', ahora)
    expect(new Date(fechaDesde).getHours()).toBe(0)
    expect(new Date(fechaDesde).getDate()).toBe(18)
    expect(new Date(fechaHasta).getDate()).toBe(18)
    expect(new Date(fechaHasta).getHours()).toBe(23)
  })

  it('«Semana» son siete días contando hoy', () => {
    const { fechaDesde, fechaHasta } = rangoDelPeriodo('Semana', ahora)
    expect(new Date(fechaDesde).getDate()).toBe(12)
    expect(diasDeDiferencia(fechaDesde, fechaHasta)).toBe(7)
  })

  it('«Mes» son treinta días contando hoy', () => {
    const { fechaDesde, fechaHasta } = rangoDelPeriodo('Mes', ahora)
    expect(new Date(fechaDesde).getMonth()).toBe(7) // agosto
    expect(new Date(fechaDesde).getDate()).toBe(20)
    expect(diasDeDiferencia(fechaDesde, fechaHasta)).toBe(30)
  })

  it('no arrastra la hora actual al inicio del rango', () => {
    const { fechaDesde } = rangoDelPeriodo('Semana', ahora)
    const inicio = new Date(fechaDesde)
    expect([inicio.getHours(), inicio.getMinutes(), inicio.getSeconds()]).toEqual([0, 0, 0])
  })
})

describe('rutaReporte', () => {
  it('sin filtros deja la ruta limpia', () => {
    expect(rutaReporte('/reportes/ventas')).toBe('/reportes/ventas')
  })

  it('agrega el rango de fechas', () => {
    const ruta = rutaReporte('/reportes/ventas', {
      fechaDesde: '2026-09-01T00:00:00.000Z',
      fechaHasta: '2026-09-18T23:59:59.999Z',
    })
    expect(ruta).toContain('fechaDesde=2026-09-01')
    expect(ruta).toContain('fechaHasta=2026-09-18')
  })

  it('manda el estado aunque sea cero', () => {
    expect(rutaReporte('/reportes/ordenes-servicio', { estado: 0 })).toBe(
      '/reportes/ordenes-servicio?estado=0',
    )
  })

  it('filtra por técnico y por cliente', () => {
    expect(rutaReporte('/reportes/ordenes-servicio', { tecnicoId: 't1' })).toBe(
      '/reportes/ordenes-servicio?tecnicoId=t1',
    )
    expect(rutaReporte('/reportes/ventas', { clienteId: 'c1' })).toBe('/reportes/ventas?clienteId=c1')
  })
})

describe('enTaller', () => {
  const resumen = (ordenes: Partial<DashboardResumenResponse['ordenesServicio']>) =>
    ({
      ordenesServicio: {
        abierta: 0,
        diagnostico: 0,
        aprobada: 0,
        enProceso: 0,
        lista: 0,
        entregada: 0,
        cancelada: 0,
        total: 0,
        ...ordenes,
      },
    }) as DashboardResumenResponse

  it('suma las órdenes que siguen en el taller', () => {
    expect(enTaller(resumen({ abierta: 2, diagnostico: 1, aprobada: 3, enProceso: 4, lista: 5 }))).toBe(15)
  })

  it('no cuenta las entregadas ni las anuladas', () => {
    expect(enTaller(resumen({ abierta: 1, entregada: 40, cancelada: 10 }))).toBe(1)
  })
})
