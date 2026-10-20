// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { apiFalsa, montar } from '../test/pantalla'
import { ModalEliminarCuenta } from './ModalEliminarCuenta'

describe('eliminar mi cuenta', () => {
  it('confirma con la contraseña y avisa cuando la cuenta ya no existe', async () => {
    const usuario = userEvent.setup()
    const onEliminada = vi.fn()
    const { llamadas } = apiFalsa({
      'POST /auth/eliminar-cuenta': () => new Response(null, { status: 204 }),
    })
    montar(<ModalEliminarCuenta abierto onCerrar={vi.fn()} onEliminada={onEliminada} />)

    expect(screen.getByText(/El taller conserva tu ficha de cliente/)).toBeTruthy()
    await usuario.type(screen.getByLabelText('Tu contraseña, para confirmar'), 'Mi.Clave1')
    await usuario.click(screen.getByRole('button', { name: 'Eliminar mi cuenta' }))

    await vi.waitFor(() => expect(onEliminada).toHaveBeenCalledOnce())
    const pedido = llamadas.find((llamada) => llamada.ruta === '/auth/eliminar-cuenta')
    expect(JSON.parse(String(pedido?.cuerpo))).toEqual({ password: 'Mi.Clave1' })
  })

  it('con la contraseña equivocada muestra el motivo y no da la cuenta por eliminada', async () => {
    const usuario = userEvent.setup()
    const onEliminada = vi.fn()
    apiFalsa({
      'POST /auth/eliminar-cuenta': () =>
        new Response(JSON.stringify({ error: 'La contraseña no es correcta.' }), {
          status: 400,
          headers: { 'content-type': 'application/json' },
        }),
    })
    montar(<ModalEliminarCuenta abierto onCerrar={vi.fn()} onEliminada={onEliminada} />)

    await usuario.type(screen.getByLabelText('Tu contraseña, para confirmar'), 'otra')
    await usuario.click(screen.getByRole('button', { name: 'Eliminar mi cuenta' }))

    expect(await screen.findByText('La contraseña no es correcta.')).toBeTruthy()
    expect(onEliminada).not.toHaveBeenCalled()
  })

  it('sin contraseña no llama a la API', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({})
    montar(<ModalEliminarCuenta abierto onCerrar={vi.fn()} onEliminada={vi.fn()} />)

    await usuario.click(screen.getByRole('button', { name: 'Eliminar mi cuenta' }))

    expect(await screen.findByText('Ingresa tu contraseña')).toBeTruthy()
    expect(llamadas).toHaveLength(0)
  })
})
