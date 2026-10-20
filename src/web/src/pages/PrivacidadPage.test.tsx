// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { apiFalsa, montar } from '../test/pantalla'
import { PrivacidadPage } from './PrivacidadPage'

describe('política de privacidad', () => {
  it('se lee sin iniciar sesión y no llama a la API', () => {
    const { llamadas } = apiFalsa({})

    montar(<PrivacidadPage />, { ruta: '/privacidad' })

    expect(screen.getByRole('heading', { level: 1, name: 'Política de privacidad' })).toBeTruthy()
    expect(llamadas).toHaveLength(0)
  })

  it('tiene la sección para eliminar la cuenta, el enlace web que pide Google Play', () => {
    apiFalsa({})

    montar(<PrivacidadPage />, { ruta: '/privacidad#eliminar-cuenta' })

    const seccion = document.getElementById('eliminar-cuenta')
    expect(seccion?.querySelector('h2')?.textContent).toBe('Eliminar tu cuenta')
    expect(seccion?.textContent).toContain('Desde la app')
    expect(seccion?.textContent).toContain('Desde la web')
  })

  it('deja a la vista lo que falta completar', () => {
    apiFalsa({})

    montar(<PrivacidadPage />, { ruta: '/privacidad' })

    expect(screen.getAllByText(/\[Pendiente: /).length).toBeGreaterThan(0)
    expect(screen.getByText('Borrador')).toBeTruthy()
  })
})
