import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorApi, guardarSesion, iniciarSesion, sesionActual, solicitar } from './http'
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
  // La API responde Results.NotFound() o Results.Forbid() sin cuerpo y con content-length 0.
  const vacia = (estado: number) => new Response(null, { status: estado, headers: { 'content-length': '0' } })

  it('un 404 sin cuerpo es un error, no un éxito sin datos', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(vacia(404)))

    await expect(solicitar('/clientes/documento/12345678')).rejects.toMatchObject({
      estado: 404,
      message: 'No se encontró el registro.',
    })
  })

  it('un 403 sin cuerpo es un error con el mensaje de permisos', async () => {
    guardarSesion(sesionCon('token-1', 'refresco-1'))
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(vacia(403)))

    await expect(solicitar('/ventas/1/anular', { metodo: 'PUT' })).rejects.toBeInstanceOf(ErrorApi)
  })

  it('un 204 o un 200 sin cuerpo sigue siendo un éxito sin datos', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(vacia(204)))
    await expect(solicitar('/productos/1', { metodo: 'DELETE' })).resolves.toBeUndefined()
  })

  it('un error con cuerpo que no es JSON usa el mensaje genérico', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('<html>Bad Gateway</html>', { status: 502 })))
    await expect(solicitar('/clientes')).rejects.toMatchObject({ estado: 502, message: 'No se pudo completar la operación.' })
  })

  it('un error con cuerpo JSON lo guarda en el error', async () => {
    const cuerpo = { exitoso: false, errores: [{ hoja: 'Clientes', fila: 3 }] }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(cuerpo, 400)))

    await expect(solicitar('/importaciones/ejecutar', { metodo: 'POST' })).rejects.toMatchObject({ estado: 400, cuerpo })
  })

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

describe('iniciarSesion', () => {
  const credenciales = { email: 'tecnico@teambenavides.pe', password: 'no-es-la-clave' }

  it('muestra el mensaje de la API, que avisa del bloqueo por intentos fallidos', async () => {
    const mensaje = 'Correo o contraseña incorrectos. Después de 5 intentos fallidos la cuenta se bloquea 15 minutos.'
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta({ error: mensaje }, 401)))

    await expect(iniciarSesion(credenciales)).rejects.toMatchObject({ estado: 401, message: mensaje })
    expect(sesionActual()).toBeNull()
  })

  it('un 401 sin cuerpo sigue diciendo que el correo o la contraseña no coinciden', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(null, 401)))

    await expect(iniciarSesion(credenciales)).rejects.toMatchObject({ message: 'Correo o contraseña incorrectos.' })
  })

  it('con demasiados intentos desde la misma conexión dice que espere', async () => {
    const mensaje = 'Demasiados intentos de inicio de sesión desde esta conexión. Espera un minuto y vuelve a intentar.'
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta({ error: mensaje }, 429)))

    await expect(iniciarSesion(credenciales)).rejects.toMatchObject({ estado: 429, message: mensaje })
  })
})
