// @vitest-environment jsdom
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { apiFalsa, montar, usuarioConSesion } from '../test/pantalla'
import { rutaMotos } from '../api/motos'
import { PanelVehiculos } from './PanelVehiculos'
import { ModalVenta } from './ModalVenta'

const modelo = {
  id: 'm1',
  codigo: 'YB125',
  nombre: 'YB125',
  marca: 'Yamaha',
  precioVenta: 6500,
  monedaPrecio: 0,
  precioIncluyeIgv: true,
  precioListaSoles: 6500,
  stockActual: 1,
  tipoId: 1,
  tipo: 'Vehiculo',
  categoriaId: 'c1',
  categoriaNombre: 'Motos',
  activo: true,
}

const moto = {
  id: 'u1',
  productoId: 'm1',
  productoCodigo: 'YB125',
  productoNombre: 'YB125',
  numeroSerieVIN: 'ME1YB125A001',
  numeroMotor: 'E3X1',
  anio: 2026,
  color: 'Negro',
  dua: '118-2026',
  itemDua: '3',
  costoUnitario: 4900,
  estado: 'En stock',
  estadoId: 0,
  fechaIngreso: '2026-11-05T15:00:00Z',
  numeroCompra: 'CP-000004',
}

describe('motos del inventario', () => {
  it('la ruta de las motos lleva sus filtros', () => {
    expect(rutaMotos({ productoId: 'm1', estado: 0, busqueda: ' ME1 ' })).toBe('/motos?productoId=m1&estado=0&busqueda=ME1')
  })

  it('la pestaña muestra los modelos y cada moto con su serie, e ingresa las de la tienda', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({
      ...usuarioConSesion({ roles: ['Gerencia/Admin'] }),
      'GET /productos': () => [modelo],
      'GET /motos': () => [moto],
      'POST /productos/:id/motos': () => ({ ...moto, id: 'u2', numeroSerieVIN: 'ME1YB125A009' }),
    })
    montar(<PanelVehiculos />)

    expect(await screen.findByText('ME1YB125A001')).toBeTruthy()
    expect(screen.getByText('Compra CP-000004')).toBeTruthy()
    expect(screen.getByText('118-2026 · ítem 3')).toBeTruthy()

    await usuario.click(screen.getAllByRole('button', { name: 'Ingresar moto' })[0])
    const dialogo = await screen.findByRole('dialog')
    await usuario.type(within(dialogo).getByLabelText('Serie (VIN o chasis)'), 'me1yb125a009')
    await usuario.type(within(dialogo).getByLabelText('Costo (soles, sin IGV)'), '4800')
    await usuario.click(within(dialogo).getByRole('button', { name: 'Ingresar' }))

    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.ruta === '/productos/m1/motos')).toBe(true))
    const cuerpo = JSON.parse(String(llamadas.find((llamada) => llamada.ruta === '/productos/m1/motos')?.cuerpo))
    expect(cuerpo).toMatchObject({ moto: { numeroSerieVIN: 'me1yb125a009' }, costo: 4800 })
  })

  it('al vender una moto se elige por su serie y la cantidad es la de motos elegidas', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({
      ...usuarioConSesion({ roles: ['Vendedor'], permisos: ['clientes.ver', 'ventas.crear', 'inventario.ver'] }),
      'GET /clientes': () => [{ id: 'k1', nombreCompleto: 'Rosa Mamani' }],
      'GET /productos': () => [modelo],
      'GET /motos': () => [moto],
      'POST /ventas': () => ({ id: 'v1', estadoId: 0 }),
    })
    montar(<ModalVenta abierto onCerrar={vi.fn()} />)

    await usuario.click(await screen.findByRole('combobox', { name: 'Cliente' }))
    await usuario.click(await screen.findByTitle('Rosa Mamani'))
    const productos = screen.getAllByRole('combobox').find((caja) => caja.closest('.linea-detalle'))!
    await usuario.click(productos)
    await usuario.click(await screen.findByTitle(/YB125 · YB125/))
    await usuario.click(await screen.findByRole('combobox', { name: 'Motos por serie' }))
    await usuario.click(await screen.findByTitle('ME1YB125A001 · Negro · 2026'))
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))

    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.metodo === 'POST' && llamada.ruta === '/ventas')).toBe(true))
    const venta = JSON.parse(String(llamadas.find((llamada) => llamada.metodo === 'POST' && llamada.ruta === '/ventas')?.cuerpo))
    expect(venta.detalles[0]).toMatchObject({ productoId: 'm1', cantidad: 1, unidadesInventarioIds: ['u1'] })
  })
})
