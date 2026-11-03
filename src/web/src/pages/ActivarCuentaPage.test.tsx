// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { apiFalsa, montar } from '../test/pantalla'
import { ActivarCuentaPage } from './ActivarCuentaPage'

const llenar = async (usuario: ReturnType<typeof userEvent.setup>, codigo: string, repetida: string) => {
  await usuario.type(screen.getByLabelText('DNI'), '40123456')
  await usuario.type(screen.getByLabelText('Código de activación'), codigo)
  await usuario.type(screen.getByLabelText('Contraseña nueva'), 'Mi.Clave1')
  await usuario.type(screen.getByLabelText('Repite la contraseña'), repetida)
  await usuario.click(screen.getByRole('button', { name: 'Activar y entrar' }))
}

describe('activar la cuenta del portal en la web', () => {
  it('pide el código de 6 dígitos y la misma contraseña dos veces antes de llamar a la API', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({})
    montar(<ActivarCuentaPage />)

    await llenar(usuario, '0429', 'otra')

    expect(await screen.findByText('El código tiene 6 dígitos')).toBeTruthy()
    expect(await screen.findByText('Las contraseñas no coinciden')).toBeTruthy()
    expect(llamadas.filter((llamada) => llamada.ruta === '/auth/activar-cuenta')).toHaveLength(0)
  })

  it('manda el DNI y el código sin correo, y muestra el motivo si la API no los acepta', async () => {
    const usuario = userEvent.setup()
    const motivo = 'El DNI o el código no son válidos, o el código ya venció. Pide uno nuevo en el taller.'
    const { llamadas } = apiFalsa({
      'POST /auth/activar-cuenta': () =>
        new Response(JSON.stringify({ error: motivo }), { status: 400, headers: { 'content-type': 'application/json' } }),
    })
    montar(<ActivarCuentaPage />)

    await llenar(usuario, '042917', 'Mi.Clave1')

    expect(await screen.findByText(motivo)).toBeTruthy()
    const pedido = llamadas.find((llamada) => llamada.ruta === '/auth/activar-cuenta')
    expect(JSON.parse(String(pedido?.cuerpo))).toEqual({
      numeroDocumento: '40123456',
      codigo: '042917',
      password: 'Mi.Clave1',
      email: null,
    })
  })
})
