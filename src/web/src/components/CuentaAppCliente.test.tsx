// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { apiFalsa, montar } from '../test/pantalla'
import { CuentaAppCliente } from './CuentaAppCliente'

const json = (datos: unknown, status = 200) =>
  new Response(JSON.stringify(datos), { status, headers: { 'content-type': 'application/json' } })

describe('cuenta del cliente en la app', () => {
  it('genera el código y lo muestra en dos grupos de tres', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({
      'GET /clientes/c1/cuenta-app': () => json({ tieneCuenta: false, email: null, codigoVigenteHasta: null }),
      'POST /clientes/c1/codigo-activacion': () => json({ codigo: '042917', vence: '2026-11-05T15:00:00Z' }),
    })
    montar(<CuentaAppCliente clienteId="c1" puedeGenerar />)

    expect(await screen.findByText('Sin cuenta')).toBeTruthy()
    await usuario.click(screen.getByRole('button', { name: 'Generar código de activación' }))

    expect(await screen.findByText('042 917')).toBeTruthy()
    expect(screen.getByText(/toca «Activar mi cuenta» y escribe su DNI/)).toBeTruthy()
    expect(llamadas.some((llamada) => llamada.ruta === '/clientes/c1/codigo-activacion')).toBe(true)
  })

  it('con la cuenta activa no ofrece generar otro código', async () => {
    apiFalsa({
      'GET /clientes/c1/cuenta-app': () => json({ tieneCuenta: true, email: null, codigoVigenteHasta: null }),
    })
    montar(<CuentaAppCliente clienteId="c1" puedeGenerar />)

    expect(await screen.findByText('Activa · entra con su DNI')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Generar/ })).toBeNull()
  })

  it('sin clientes.editar muestra el estado pero no el botón', async () => {
    apiFalsa({
      'GET /clientes/c1/cuenta-app': () =>
        json({ tieneCuenta: false, email: null, codigoVigenteHasta: '2026-11-05T15:00:00Z' }),
    })
    montar(<CuentaAppCliente clienteId="c1" puedeGenerar={false} />)

    expect(await screen.findByText(/Sin activar · código vigente hasta el/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Generar/ })).toBeNull()
  })
})
