// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { montar } from '../test/pantalla'
import { BarreraErrores } from './BarreraErrores'

let falla = true

function PantallaQueFalla() {
  if (falla) throw new Error('Falla de prueba')
  return <p>Pantalla bien</p>
}

function PantallaNoDescargada(): never {
  throw new TypeError('Failed to fetch dynamically imported module: https://x/assets/CajaPage-1a2b.js')
}

describe('BarreraErrores', () => {
  beforeEach(() => {
    // React y la barrera registran el error por consola: es lo esperado.
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    falla = true
  })

  it('si una pantalla falla, avisa en vez de dejar la página en blanco y deja reintentar', async () => {
    montar(
      <BarreraErrores alcance="pantalla">
        <PantallaQueFalla />
      </BarreraErrores>,
    )

    expect(screen.getByRole('alert').textContent).toContain('No se pudo mostrar esta pantalla')

    falla = false
    await userEvent.click(screen.getByRole('button', { name: 'Volver a intentar' }))

    expect(screen.getByText('Pantalla bien')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('si no se pudo descargar la pantalla (versión nueva publicada), ofrece recargar y no reintentar', () => {
    montar(
      <BarreraErrores alcance="pantalla">
        <PantallaNoDescargada />
      </BarreraErrores>,
    )

    expect(screen.getByRole('alert').textContent).toContain('Hay una versión nueva de la plataforma')
    expect(screen.getByRole('button', { name: 'Recargar' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Volver a intentar' })).toBeNull()
  })
})
