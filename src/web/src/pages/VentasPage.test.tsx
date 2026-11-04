// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { apiFalsa, montar, usuarioConSesion } from '../test/pantalla'
import { rutaVentas } from '../api/ventas'
import { VentasPage } from './VentasPage'

const venta = {
  id: 'a1b2c3d4-0000-0000-0000-000000000000',
  clienteId: 'k1',
  clienteNombre: 'Rosa Mamani',
  ordenServicioId: 'o1',
  numeroOrdenServicio: 'OS-000902',
  estado: 'Confirmada',
  estadoId: 1,
  fecha: '2026-11-04T15:00:00Z',
  total: 35,
  cantidadItems: 1,
  activo: true,
  saldo: 0,
  estadoPago: 'Pagado',
  estadoComprobante: 'Pendiente',
  estadoAprobacionGerenciaId: 0,
}

describe('filtros del registro de ventas', () => {
  it('la ruta lleva cada filtro, con las fechas como días', () => {
    expect(
      rutaVentas({ busqueda: 'OS-000902', comprobante: 'Pendiente', fechaDesde: '2026-11-01', fechaHasta: '2026-11-04' }),
    ).toBe('/ventas?fechaDesde=2026-11-01&fechaHasta=2026-11-04&comprobante=Pendiente&busqueda=OS-000902')
    expect(rutaVentas()).toBe('/ventas')
  })

  it('busca y filtra por comprobante, y el Excel exporta lo mismo', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({
      ...usuarioConSesion({ roles: ['Vendedor'], permisos: ['ventas.ver', 'clientes.ver'] }),
      'GET /ventas': () => [venta],
      'GET /ventas/pendientes-comprobante': () => [],
      'GET /clientes': () => [{ id: 'k1', nombreCompleto: 'Rosa Mamani' }],
      'GET /ventas/exportar-excel': () => new Response(new Blob(['xlsx']), { status: 200 }),
    })
    URL.createObjectURL ??= () => 'blob:prueba'
    URL.revokeObjectURL ??= () => undefined
    montar(<VentasPage />)

    // La orden que liquidó la venta se ve en la lista: así se encuentra lo que se buscó.
    expect(await screen.findByText('OS-000902')).toBeTruthy()

    await usuario.type(screen.getByRole('searchbox', { name: 'Buscar venta' }), '902{Enter}')
    await usuario.click(screen.getByRole('combobox', { name: 'Filtrar por comprobante' }))
    await usuario.click(await screen.findByTitle('Sin comprobante'))

    const consultas = () =>
      llamadas.filter((llamada) => llamada.ruta === '/ventas').map((llamada) => llamada.url.searchParams)
    await vi.waitFor(() =>
      expect(consultas().some((p) => p.get('busqueda') === '902' && p.get('comprobante') === 'Pendiente')).toBe(true),
    )

    await usuario.click(screen.getByRole('button', { name: 'Exportar a Excel' }))
    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.ruta === '/ventas/exportar-excel')).toBe(true))
    const excel = llamadas.find((llamada) => llamada.ruta === '/ventas/exportar-excel')!.url.searchParams
    expect(excel.get('busqueda')).toBe('902')
    expect(excel.get('comprobante')).toBe('Pendiente')
  })
})
