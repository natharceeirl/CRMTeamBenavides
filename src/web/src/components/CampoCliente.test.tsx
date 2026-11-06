// @vitest-environment jsdom
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { apiFalsa, montar, usuarioConSesion } from '../test/pantalla'
import { ModalPedidoLima } from './ModalPedidoLima'
import { ModalVehiculo } from './ModalVehiculo'
import { ModalVenta } from './ModalVenta'

/** Clientes en memoria: el alta agrega a Rosa Mamani y la lista la devuelve después. */
const clientesEnMemoria = () => {
  const clientes = [{ id: 'k1', nombreCompleto: 'Luis Quispe' }]
  return {
    'GET /clientes': () => clientes,
    'POST /clientes': () => {
      const nuevo = { id: 'k9', nombreCompleto: 'Rosa Mamani' }
      clientes.push(nuevo)
      return nuevo
    },
  }
}

const registrarRosa = async (usuario: ReturnType<typeof userEvent.setup>) => {
  await usuario.click(await screen.findByRole('button', { name: 'Registrar cliente nuevo' }))
  // El alta se abre encima del formulario: es el último diálogo.
  expect(await screen.findByText('Registrar cliente')).toBeTruthy()
  const alta = screen.getAllByRole('dialog').at(-1)!
  await usuario.type(within(alta).getByLabelText('Nombres'), 'Rosa')
  await usuario.type(within(alta).getByLabelText('Primer apellido'), 'Mamani')
  await usuario.click(within(alta).getByRole('button', { name: 'Guardar' }))
}

describe('registrar al cliente desde el formulario', () => {
  it('en la unidad, el cliente nuevo queda como propietario', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({
      ...usuarioConSesion({ roles: ['Recepcion'], permisos: ['clientes.ver', 'clientes.crear', 'unidades.crear'] }),
      ...clientesEnMemoria(),
      'POST /vehiculos': () => ({ id: 'v1' }),
    })
    montar(<ModalVehiculo abierto onCerrar={vi.fn()} />)

    await registrarRosa(usuario)
    expect(await screen.findByTitle('Rosa Mamani')).toBeTruthy()
    // El alta del cliente queda montada aunque cerrada: se guarda desde el diálogo de la unidad.
    const unidadDialogo = screen.getAllByRole('dialog')[0]
    await usuario.type(within(unidadDialogo).getByLabelText('Modelo'), 'FZ 2.0')
    await usuario.type(within(unidadDialogo).getByLabelText('VIN / Nro. de serie'), 'ME1RG0710K0001234')
    await usuario.click(within(unidadDialogo).getByRole('button', { name: 'Guardar' }))

    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.ruta === '/vehiculos')).toBe(true))
    const unidad = JSON.parse(String(llamadas.find((llamada) => llamada.ruta === '/vehiculos')?.cuerpo))
    expect(unidad).toMatchObject({ clienteId: 'k9', modelo: 'FZ 2.0', numeroSerieVIN: 'ME1RG0710K0001234' })
  })

  it('en la venta, el cliente nuevo queda elegido', async () => {
    const usuario = userEvent.setup()
    apiFalsa({
      ...usuarioConSesion({ roles: ['Vendedor'], permisos: ['clientes.ver', 'clientes.crear', 'ventas.crear'] }),
      ...clientesEnMemoria(),
      'GET /productos': () => [],
    })
    montar(<ModalVenta abierto onCerrar={vi.fn()} />)

    await registrarRosa(usuario)
    expect(await screen.findByTitle('Rosa Mamani')).toBeTruthy()
  })

  it('en el pedido a Lima, sin clientes.crear no se ofrece', async () => {
    apiFalsa({
      ...usuarioConSesion({ roles: ['Vendedor'], permisos: ['clientes.ver', 'pedidoslima.crear'] }),
      ...clientesEnMemoria(),
      'GET /productos': () => [],
    })
    montar(<ModalPedidoLima abierto onCerrar={vi.fn()} />)

    expect(await screen.findByText('Cliente')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Registrar cliente nuevo' })).toBeNull()
  })

  it('desde la ficha del cliente, la unidad no pide propietario', async () => {
    apiFalsa({
      ...usuarioConSesion({ roles: ['Recepcion'], permisos: ['clientes.ver', 'clientes.crear', 'unidades.crear'] }),
      ...clientesEnMemoria(),
    })
    montar(<ModalVehiculo abierto clienteFijo="k1" onCerrar={vi.fn()} />)

    expect(await screen.findByText('Tipo de unidad')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Registrar cliente nuevo' })).toBeNull()
  })
})
