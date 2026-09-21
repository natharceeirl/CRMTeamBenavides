import type { RespuestaLogin, SolicitudLogin } from './tipos'

// Todas las llamadas salen contra el mismo origen; el proxy de Vite las manda
// al backend (ver vite.config.ts).
const RUTA_BASE = '/api'
const CLAVE_SESION = 'tb.sesion'

/** Lo que devuelve el login: los dos tokens y sus vencimientos. */
export type Sesion = RespuestaLogin

export class ErrorApi extends Error {
  readonly estado: number

  constructor(estado: number, mensaje: string) {
    super(mensaje)
    this.name = 'ErrorApi'
    this.estado = estado
  }
}

type Opciones = {
  metodo?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  cuerpo?: unknown
}

let sesion: Sesion | null = recuperarSesion()
let renovacionEnCurso: Promise<Sesion | null> | null = null
const oyentes = new Set<(sesion: Sesion | null) => void>()

function recuperarSesion(): Sesion | null {
  try {
    const guardada = localStorage.getItem(CLAVE_SESION)
    return guardada ? (JSON.parse(guardada) as Sesion) : null
  } catch {
    // Navegación privada o almacenamiento bloqueado: la sesión vive en memoria.
    return null
  }
}

export function sesionActual(): Sesion | null {
  return sesion
}

export function guardarSesion(nueva: Sesion | null): void {
  sesion = nueva

  try {
    if (nueva) {
      localStorage.setItem(CLAVE_SESION, JSON.stringify(nueva))
    } else {
      localStorage.removeItem(CLAVE_SESION)
    }
  } catch {
    // Sin almacenamiento la sesión se pierde al recargar, pero la app funciona.
  }

  for (const oyente of oyentes) {
    oyente(nueva)
  }
}

/** Avisa cuando la sesión cambia: al entrar, al salir o cuando el refresh falla. */
export function escucharSesion(oyente: (sesion: Sesion | null) => void): () => void {
  oyentes.add(oyente)
  return () => {
    oyentes.delete(oyente)
  }
}

function venció(fecha: string): boolean {
  const vencimiento = Date.parse(fecha)
  if (Number.isNaN(vencimiento)) {
    return false
  }
  // Medio minuto de margen para no mandar un token que vence en el camino.
  return vencimiento - 30_000 <= Date.now()
}

async function interpretar<T>(respuesta: Response): Promise<T> {
  if (respuesta.status === 204 || respuesta.headers.get('content-length') === '0') {
    return undefined as T
  }

  const texto = await respuesta.text()
  const datos: unknown = texto ? JSON.parse(texto) : null

  if (!respuesta.ok) {
    throw new ErrorApi(respuesta.status, mensajeDeError(respuesta.status, datos))
  }

  return datos as T
}

function mensajeDeError(estado: number, datos: unknown): string {
  if (datos && typeof datos === 'object' && 'error' in datos) {
    const detalle = (datos as { error?: unknown }).error
    if (typeof detalle === 'string' && detalle.length > 0) {
      return detalle
    }
  }

  if (estado === 401) return 'La sesión expiró. Vuelve a iniciar sesión.'
  if (estado === 403) return 'No tienes permiso para esta operación.'
  if (estado === 404) return 'No se encontró el registro.'
  return 'No se pudo completar la operación.'
}

async function enviar(ruta: string, opciones: Opciones): Promise<Response> {
  const cabeceras: Record<string, string> = {}

  if (opciones.cuerpo !== undefined) {
    cabeceras['Content-Type'] = 'application/json'
  }
  if (sesion) {
    cabeceras.Authorization = `Bearer ${sesion.accessToken}`
  }

  return await fetch(`${RUTA_BASE}${ruta}`, {
    method: opciones.metodo ?? 'GET',
    headers: cabeceras,
    body: opciones.cuerpo === undefined ? undefined : JSON.stringify(opciones.cuerpo),
  })
}

/**
 * Llama a la API con el token puesto. Si el access token venció, lo renueva
 * antes de salir; si el backend igual responde 401, lo intenta una sola vez más
 * y, si tampoco, cierra la sesión para que la app mande al login.
 */
export async function solicitar<T>(ruta: string, opciones: Opciones = {}): Promise<T> {
  if (sesion && venció(sesion.accessTokenExpiration)) {
    await renovarSesion()
  }

  const respuesta = await enviar(ruta, opciones)

  if (respuesta.status !== 401 || !sesion) {
    return await interpretar<T>(respuesta)
  }

  const renovada = await renovarSesion()
  if (!renovada) {
    guardarSesion(null)
    throw new ErrorApi(401, 'La sesión expiró. Vuelve a iniciar sesión.')
  }

  return await interpretar<T>(await enviar(ruta, opciones))
}

/**
 * Renueva los tokens. Varias llamadas en paralelo comparten una sola renovación,
 * porque el backend rota el refresh token: el segundo intento fallaría.
 */
export function renovarSesion(): Promise<Sesion | null> {
  const refreshToken = sesion?.refreshToken
  if (!refreshToken) {
    return Promise.resolve(null)
  }

  renovacionEnCurso ??= (async () => {
    try {
      const respuesta = await fetch(`${RUTA_BASE}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })

      if (!respuesta.ok) {
        return null
      }

      const renovada = (await respuesta.json()) as Sesion
      guardarSesion(renovada)
      return renovada
    } catch {
      return null
    } finally {
      renovacionEnCurso = null
    }
  })()

  return renovacionEnCurso
}

export async function iniciarSesion(datos: SolicitudLogin): Promise<Sesion> {
  let respuesta: Response

  try {
    respuesta = await fetch(`${RUTA_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(datos),
    })
  } catch {
    throw new ErrorApi(0, 'No se pudo conectar con el servidor. ¿Está levantada la API?')
  }

  if (respuesta.status === 401) {
    throw new ErrorApi(401, 'Correo o contraseña incorrectos.')
  }
  if (!respuesta.ok) {
    throw new ErrorApi(respuesta.status, 'No se pudo iniciar sesión.')
  }

  const nueva = (await respuesta.json()) as Sesion
  guardarSesion(nueva)
  return nueva
}

/** Cierra la sesión en el servidor y limpia el almacenamiento local. */
export async function terminarSesion(): Promise<void> {
  try {
    if (sesion) {
      await fetch(`${RUTA_BASE}/auth/logout`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${sesion.accessToken}`,
        },
      })
    }
  } catch {
    // Si la llamada falla o no hay conexión, la sesión local se descarta de todas formas.
  } finally {
    guardarSesion(null)
  }
}
