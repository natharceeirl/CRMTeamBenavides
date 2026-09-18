import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorApi, guardarSesion, sesionActual, solicitar } from './http'
import type { Sesion } from './http'

const enUnaHora = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()
const enUnaSemana = () => new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString()

const sesionCon = (acceso: string, refresco: string): Sesion => ({
  accessToken: acceso,
  accessTokenExpiration: enUnaHora(),
  refreshToken: refresco,
  refreshTokenExpiration: enUnaSemana(),
})

const respuesta = (cuerpo: unknown, estado = 200) =>
  new Response(cuerpo === null ? '' : JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'content-type': 'application/json' },
  })

const cabeceras = (llamada: unknown[]) =>
  (llamada[1] as { headers: Record<string, string> }).headers

beforeEach(() => {
  guardarSesion(null)
})

afterEach(() => {
  vi.unstubAllGlobals()
  guardarSesion(null)
})

describe('solicitar', () => {
  it('manda el token de la sesión en cada llamada', async () => {
    guardarSesion(sesionCon('token-1', 'refresco-1'))
    const llamar = vi.fn().mockResolvedValue(respuesta([{ id: '1' }]))
    vi.stubGlobal('fetch', llamar)

    await solicitar('/clientes')

    expect(llamar).toHaveBeenCalledTimes(1)
    expect(llamar.mock.calls[0][0]).toBe('/api/clientes')
    expect(cabeceras(llamar.mock.calls[0]).Authorization).toBe('Bearer token-1')
  })

  it('no manda token si no hay sesión', async () => {
    const llamar = vi.fn().mockResolvedValue(respuesta([]))
    vi.stubGlobal('fetch', llamar)

    await solicitar('/chatbot/faqs')

    expect(cabeceras(llamar.mock.calls[0]).Authorization).toBeUndefined()
  })

  it('ante un 401 renueva el token y reintenta la llamada original', async () => {
    guardarSesion(sesionCon('token-viejo', 'refresco-1'))
    const llamar = vi
      .fn()
      .mockResolvedValueOnce(respuesta(null, 401))
      .mockResolvedValueOnce(respuesta(sesionCon('token-nuevo', 'refresco-2')))
      .mockResolvedValueOnce(respuesta([{ id: '1' }]))
    vi.stubGlobal('fetch', llamar)

    const datos = await solicitar<{ id: string }[]>('/clientes')

    expect(datos).toEqual([{ id: '1' }])
    expect(llamar).toHaveBeenCalledTimes(3)
    expect(llamar.mock.calls[1][0]).toBe('/api/auth/refresh')
    expect(cabeceras(llamar.mock.calls[2]).Authorization).toBe('Bearer token-nuevo')
    expect(sesionActual()?.accessToken).toBe('token-nuevo')
  })

  it('con dos llamadas fallando a la vez renueva una sola vez', async () => {
    // El backend rota el refresh token: una segunda renovación fallaría.
    guardarSesion(sesionCon('token-viejo', 'refresco-1'))

    const llamar = vi.fn(async (ruta: string) => {
      if (ruta === '/api/auth/refresh') {
        return respuesta(sesionCon('token-nuevo', 'refresco-2'))
      }
      const autorizacion = cabeceras(llamar.mock.calls[llamar.mock.calls.length - 1]).Authorization
      return autorizacion === 'Bearer token-nuevo' ? respuesta([]) : respuesta(null, 401)
    })
    vi.stubGlobal('fetch', llamar)

    await Promise.all([solicitar('/clientes'), solicitar('/vehiculos')])

    const renovaciones = llamar.mock.calls.filter(([ruta]) => ruta === '/api/auth/refresh')
    expect(renovaciones).toHaveLength(1)
  })

  it('si la renovación falla, borra la sesión y avisa', async () => {
    guardarSesion(sesionCon('token-viejo', 'refresco-vencido'))
    const llamar = vi
      .fn()
      .mockResolvedValueOnce(respuesta(null, 401))
      .mockResolvedValueOnce(respuesta(null, 401))
    vi.stubGlobal('fetch', llamar)

    await expect(solicitar('/clientes')).rejects.toBeInstanceOf(ErrorApi)
    expect(sesionActual()).toBeNull()
  })

  it('usa el mensaje de error que manda la API', async () => {
    guardarSesion(sesionCon('token-1', 'refresco-1'))
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(respuesta({ error: 'Stock insuficiente para el producto.' }, 400)),
    )

    await expect(solicitar('/ordenes-servicio/1/detalles', { metodo: 'POST', cuerpo: {} })).rejects.toThrow(
      'Stock insuficiente para el producto.',
    )
  })

  it('devuelve indefinido cuando la API responde 204', async () => {
    guardarSesion(sesionCon('token-1', 'refresco-1'))
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 204 })))

    await expect(solicitar('/clientes/1', { metodo: 'DELETE' })).resolves.toBeUndefined()
  })
})
