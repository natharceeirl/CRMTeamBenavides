import { describe, expect, it } from 'vitest'
import { rutaDeNotificacion } from './notificaciones'

const id = '1f0e8a2c-0000-0000-0000-000000000001'

describe('rutaDeNotificacion', () => {
  it('las solicitudes llevan a la bandeja de aprobaciones', () => {
    expect(rutaDeNotificacion({ entidad: 'Aprobaciones', entidadId: id }, false)).toBe('/aprobaciones')
  })

  it('el personal va a la orden, la agenda o los pedidos', () => {
    expect(rutaDeNotificacion({ entidad: 'OrdenServicio', entidadId: id }, false)).toBe(`/ordenes/${id}`)
    expect(rutaDeNotificacion({ entidad: 'Cita', entidadId: id }, false)).toBe('/citas')
    expect(rutaDeNotificacion({ entidad: 'PedidoLima', entidadId: id }, false)).toBe('/pedidos-lima')
  })

  it('el cliente ve todo lo suyo en su portal', () => {
    expect(rutaDeNotificacion({ entidad: 'OrdenServicio', entidadId: id }, true)).toBe('/portal')
    expect(rutaDeNotificacion({ entidad: 'Cita', entidadId: id }, true)).toBe('/portal')
    expect(rutaDeNotificacion({ entidad: null, entidadId: null }, true)).toBe('/portal')
  })

  it('una orden sin id lleva a la lista', () => {
    expect(rutaDeNotificacion({ entidad: 'OrdenServicio', entidadId: null }, false)).toBe('/ordenes')
  })
})
