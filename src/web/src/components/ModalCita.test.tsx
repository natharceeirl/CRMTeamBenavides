// @vitest-environment jsdom
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import { describe, expect, it, vi } from 'vitest'
import { apiFalsa, montar, usuarioConSesion } from '../test/pantalla'
import { unidadDeCita } from '../api/citas'
import { ModalCita } from './ModalCita'

const recepcion = () =>
  usuarioConSesion({
    roles: ['Recepcion'],
    permisos: ['clientes.ver', 'clientes.crear', 'unidades.ver', 'citas.ver', 'citas.crear'],
  })

describe('nueva cita', () => {
  it('registra al cliente ahí mismo y agenda sin unidad', async () => {
    const usuario = userEvent.setup()
    const clientes = [{ id: 'k1', nombreCompleto: 'Luis Quispe' }]
    const { llamadas } = apiFalsa({
      ...recepcion(),
      'GET /clientes': () => clientes,
      'POST /clientes': () => {
        const nuevo = { id: 'k9', nombreCompleto: 'Rosa Mamani' }
        clientes.push(nuevo)
        return nuevo
      },
      'GET /vehiculos': () => [],
      'POST /citas': () => ({ id: 'c1' }),
    })
    const manana = dayjs().add(1, 'day').hour(9).minute(0)
    montar(<ModalCita abierto fechaInicial={manana} onCerrar={vi.fn()} />)

    await usuario.click(await screen.findByRole('button', { name: 'Registrar cliente nuevo' }))
    // El alta se abre encima de la cita: es el último diálogo.
    expect(await screen.findByText('Registrar cliente')).toBeTruthy()
    const alta = screen.getAllByRole('dialog').at(-1)!
    await usuario.type(within(alta).getByLabelText('Nombre completo'), 'Rosa Mamani')
    await usuario.click(within(alta).getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByTitle('Rosa Mamani')).toBeTruthy()
    expect(screen.getByText('Unidad (opcional)')).toBeTruthy()
    await usuario.type(screen.getByLabelText('Motivo'), 'Revisión general')
    await usuario.click(screen.getByRole('button', { name: 'Agendar' }))

    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.ruta === '/citas')).toBe(true))
    const cita = JSON.parse(String(llamadas.find((llamada) => llamada.ruta === '/citas')?.cuerpo))
    expect(cita).toMatchObject({ clienteId: 'k9', vehiculoId: null, motivo: 'Revisión general' })
  })

  it('sin clientes.crear no ofrece registrar al cliente', async () => {
    apiFalsa({
      ...usuarioConSesion({ roles: ['Vendedor'], permisos: ['clientes.ver', 'citas.crear'] }),
      'GET /clientes': () => [],
    })
    montar(<ModalCita abierto onCerrar={vi.fn()} />)

    expect(await screen.findByText('Unidad (opcional)')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Registrar cliente nuevo' })).toBeNull()
  })
})

describe('unidad de la cita', () => {
  it('dice «Sin unidad» cuando la cita no la tiene', () => {
    expect(unidadDeCita({ vehiculoModelo: null, vehiculoPlaca: null })).toBe('Sin unidad')
    expect(unidadDeCita({ vehiculoModelo: 'Yamaha FZ 2.0', vehiculoPlaca: null })).toBe('Yamaha FZ 2.0 · sin placa')
    expect(unidadDeCita({ vehiculoModelo: 'Yamaha FZ 2.0', vehiculoPlaca: 'ABC-123' })).toBe('Yamaha FZ 2.0 · ABC-123')
  })
})
