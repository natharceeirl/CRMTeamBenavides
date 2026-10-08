import { describe, expect, it } from 'vitest'
import { ENTIDAD_APROBACION, TIPO_APROBACION, rutaAprobaciones } from './aprobaciones'
import { GERENCIA } from './ordenes'
import { rutaExportarVentas } from './ventas'
import { precioDistintoAlDeLista } from '../components/AvisoPrecioGerencia'

describe('aprobaciones de Gerencia', () => {
  it('usa exactamente los valores del backend', () => {
    expect(TIPO_APROBACION.cambioPrecio).toBe('CambioPrecio')
    expect(Object.values(ENTIDAD_APROBACION)).toEqual(['OrdenServicio', 'Venta', 'PedidoLima'])
  })

  it('arma la ruta del historial solo con los filtros puestos', () => {
    expect(rutaAprobaciones({})).toBe('/aprobaciones')
    expect(rutaAprobaciones({ entidad: ENTIDAD_APROBACION.venta, estado: GERENCIA.rechazado })).toBe(
      '/aprobaciones?entidad=Venta&estado=3',
    )
  })
})

describe('exportar ventas', () => {
  it('usa los mismos filtros que la lista', () => {
    expect(rutaExportarVentas()).toBe('/ventas/exportar-excel')
    expect(rutaExportarVentas({ estado: 1 })).toBe('/ventas/exportar-excel?estado=1')
  })
})

describe('precio fuera de lista', () => {
  it('cualquier diferencia, suba o baje, pide aprobación', () => {
    expect(precioDistintoAlDeLista(110, 100)).toBe(true)
    expect(precioDistintoAlDeLista(90, 100)).toBe(true)
  })

  it('el mismo precio, o sin datos, no la pide', () => {
    expect(precioDistintoAlDeLista(100, 100)).toBe(false)
    expect(precioDistintoAlDeLista(100.004, 100)).toBe(false)
    expect(precioDistintoAlDeLista(null, 100)).toBe(false)
    expect(precioDistintoAlDeLista(100, undefined)).toBe(false)
  })
})
