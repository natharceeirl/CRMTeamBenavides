import { describe, expect, it } from 'vitest'
import { ENTIDAD_APROBACION, TIPO_APROBACION, rutaAprobaciones } from './aprobaciones'
import { GERENCIA } from './ordenes'
import { ESTADO_VENTA, rutaExportarVentas } from './ventas'
import type { VentaDetalleResponse } from './tipos'
import { precioDistintoAlDeLista } from '../components/AvisoPrecioGerencia'
import { esVentaEditable } from '../components/ModalEditarCotizacion'
import { PERMISOS, fijaPreciosDeOrdenSinAprobacion } from '../auth/acceso'

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

describe('qué venta se puede corregir', () => {
  const base = {
    id: 'v1',
    clienteId: 'c1',
    clienteNombre: 'Cliente',
    clienteDocumento: null,
    clienteTelefono: null,
    ordenServicioId: null,
    estado: '',
    fecha: '2026-10-10T10:00:00Z',
    total: 100,
    activo: true,
    detalles: [{ id: 'd1', productoId: 'p1', productoCodigo: 'P1', productoNombre: 'Filtro', cantidad: 1, precioUnitario: 100, subtotal: 100 }],
    pagos: [],
    comprobante: null,
  } as unknown as VentaDetalleResponse

  it('una cotización sin pagos, sí', () => {
    expect(esVentaEditable({ ...base, estadoId: ESTADO_VENTA.cotizacion })).toBe(true)
  })

  it('una venta confirmada, solo con el precio pendiente o rechazado', () => {
    const confirmada = { ...base, estadoId: ESTADO_VENTA.confirmada }
    expect(esVentaEditable({ ...confirmada, estadoAprobacionGerenciaId: GERENCIA.rechazado })).toBe(true)
    expect(esVentaEditable({ ...confirmada, estadoAprobacionGerenciaId: GERENCIA.pendiente })).toBe(true)
    expect(esVentaEditable({ ...confirmada, estadoAprobacionGerenciaId: GERENCIA.aprobado })).toBe(false)
  })

  it('con pagos o comprobante, no', () => {
    const rechazada = { ...base, estadoId: ESTADO_VENTA.confirmada, estadoAprobacionGerenciaId: GERENCIA.rechazado }
    expect(esVentaEditable({ ...rechazada, pagos: [{ id: 'pg1' }] } as unknown as VentaDetalleResponse)).toBe(false)
    expect(esVentaEditable({ ...rechazada, comprobante: { id: 'cp1' } } as unknown as VentaDetalleResponse)).toBe(false)
  })
})

describe('quién fija precios de la orden sin aprobación', () => {
  const sesion = (roles: string[], permisos: string[] = []) => ({
    roles,
    esGerencia: roles.includes('Gerencia/Admin'),
    esRecepcion: roles.includes('Recepcion'),
    tienePermiso: (permiso: string) => permisos.includes(permiso),
    tieneAlgunPermiso: (lista: string[]) => lista.some((permiso) => permisos.includes(permiso)),
  })

  it('Gerencia, Recepción y quien tiene precios.modificar, igual que el backend', () => {
    expect(fijaPreciosDeOrdenSinAprobacion(sesion(['Gerencia/Admin']))).toBe(true)
    expect(fijaPreciosDeOrdenSinAprobacion(sesion(['Recepcion']))).toBe(true)
    expect(fijaPreciosDeOrdenSinAprobacion(sesion(['Vendedor'], [PERMISOS.preciosModificar]))).toBe(true)
  })

  it('el técnico pide aprobación', () => {
    expect(fijaPreciosDeOrdenSinAprobacion(sesion(['Tecnico'], [PERMISOS.ordenesAgregarItems]))).toBe(false)
  })
})
