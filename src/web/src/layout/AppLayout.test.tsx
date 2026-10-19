// @vitest-environment jsdom
import { screen, waitFor, within } from '@testing-library/react'
import { Route, Routes } from 'react-router'
import { describe, expect, it } from 'vitest'
import type { TipoCambioResponse } from '../api/tipos'
import { apiFalsa, montar, usuarioConSesion } from '../test/pantalla'
import { AppLayout } from './AppLayout'

const tipoCambio: TipoCambioResponse = {
  configurado: true,
  tipoCambio: 3.75,
  valorVenta: 3.762,
  valorCompra: 3.741,
  monedaBase: 'PEN',
  monedaExtranjera: 'USD',
  fechaActualizacion: new Date().toISOString(),
  ultimoUsuarioNombre: 'Gerencia',
}

// Lo que piden la campanita, el chatbot y el tipo de cambio de la barra lateral.
const barraLateral = {
  'GET /notificaciones/conteo': { noLeidas: 0 },
  'GET /chatbot/faqs': [],
  'GET /configuracion/tipo-cambio': tipoCambio,
}

function montarLayout() {
  montar(
    <Routes>
      <Route element={<AppLayout />}>
        <Route path="/" element={<p>Contenido</p>} />
      </Route>
    </Routes>,
  )
}

const enlacesDelMenu = () =>
  within(screen.getByRole('navigation', { name: 'Menú principal' }))
    .queryAllByRole('link')
    .map((enlace) => enlace.textContent)

describe('menú según el rol', () => {
  it('el vendedor ve solo lo que sus permisos le dejan abrir', async () => {
    apiFalsa({
      ...barraLateral,
      ...usuarioConSesion({ roles: ['Vendedor'], permisos: ['clientes.ver', 'inventario.ver', 'ventas.ver'] }),
    })

    montarLayout()

    await waitFor(() => expect(enlacesDelMenu()).toEqual(['Clientes', 'Repuestos', 'Ventas']))
    expect(await screen.findByText('US$ 3.762')).toBeTruthy()
  })

  it('Gerencia ve todo el menú del taller, pero no el portal del cliente', async () => {
    apiFalsa({ ...barraLateral, ...usuarioConSesion({ roles: ['Gerencia/Admin'] }) })

    montarLayout()

    await waitFor(() => expect(enlacesDelMenu()).toContain('Usuarios'))
    const enlaces = enlacesDelMenu()
    expect(enlaces).toContain('Configuración')
    expect(enlaces).toContain('Importar datos')
    expect(enlaces).toContain('Chatbot')
    expect(enlaces).not.toContain('Mi Portal')
  })

  it('el cliente solo ve su portal, aunque tenga otro permiso, y no se le pide el tipo de cambio', async () => {
    const { llamadas } = apiFalsa({
      ...barraLateral,
      ...usuarioConSesion({ roles: ['Cliente'], permisos: ['portal.acceso', 'clientes.ver'] }),
    })

    montarLayout()

    await waitFor(() => expect(enlacesDelMenu()).toEqual(['Mi Portal']))
    expect(screen.queryByText(/US\$/)).toBeNull()
    expect(llamadas.some((llamada) => llamada.ruta === '/configuracion/tipo-cambio')).toBe(false)
  })
})
