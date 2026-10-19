// Ayudas para probar pantallas en jsdom con la misma cadena que la app: React Query,
// la sesión real (con /api/auth/me simulado), antd y el router. Los archivos de prueba
// que las usan empiezan con `// @vitest-environment jsdom`.
import type { ReactElement } from 'react'
import { cleanup, render } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { App as AppAntd, ConfigProvider } from 'antd'
import esES from 'antd/locale/es_ES'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, vi } from 'vitest'
import { guardarSesion } from '../api/http'
import { ProveedorSesion } from '../auth/sesion'
import type { MeResponse } from '../api/tipos'

// Lo que antd usa del navegador y jsdom no trae.
if (typeof window !== 'undefined') {
  window.matchMedia ??= (consulta: string) =>
    ({
      matches: false,
      media: consulta,
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }) as MediaQueryList
  globalThis.ResizeObserver ??= class {
    observe = () => undefined
    unobserve = () => undefined
    disconnect = () => undefined
  }
  // jsdom no calcula estilos de pseudo-elementos y lo avisa por consola en cada tabla.
  const estilosOriginales = window.getComputedStyle.bind(window)
  window.getComputedStyle = (elemento: Element) => estilosOriginales(elemento)
}

afterEach(() => {
  cleanup()
  guardarSesion(null)
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

type Pedido = { metodo: string; ruta: string; url: URL; cuerpo: BodyInit | null | undefined }
type Respuesta = ((pedido: Pedido) => unknown) | object | string | number | boolean | null

const responder = (estado: number, cuerpo: unknown) =>
  new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'content-type': 'application/json' },
  })

/** «/vehiculos/:id/historial» coincide con «/vehiculos/abc/historial». */
const coincide = (patron: string, ruta: string) => {
  const a = patron.split('/')
  const b = ruta.split('/')
  return a.length === b.length && a.every((parte, i) => parte.startsWith(':') || parte === b[i])
}

/**
 * Reemplaza fetch por una API en memoria. Las claves son «MÉTODO /ruta» sin el prefijo
 * /api; el valor es el JSON que se responde o una función que lo arma. Lo que no está
 * definido responde 404, para que una llamada inesperada no pase en silencio.
 */
export function apiFalsa(rutas: Record<string, Respuesta>) {
  const llamadas: Pedido[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (entrada: RequestInfo | URL, opciones: RequestInit = {}) => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost')
      const pedido: Pedido = {
        metodo: (opciones.method ?? 'GET').toUpperCase(),
        ruta: url.pathname.replace(/^\/api/, ''),
        url,
        cuerpo: opciones.body,
      }
      llamadas.push(pedido)
      const clave = Object.keys(rutas).find((k) => {
        const [metodo, patron] = k.split(' ')
        return metodo === pedido.metodo && coincide(patron, pedido.ruta)
      })
      if (!clave) return responder(404, { error: `Sin respuesta falsa para ${pedido.metodo} ${pedido.ruta}` })
      const definida = rutas[clave]
      const valor = typeof definida === 'function' ? await definida(pedido) : definida
      return valor instanceof Response ? valor : responder(200, valor)
    }),
  )
  return { llamadas }
}

/** Un usuario con sesión iniciada; `GET /auth/me` devuelve estos roles y permisos. */
export function usuarioConSesion({ roles = [] as string[], permisos = [] as string[] } = {}) {
  const claims = btoa(JSON.stringify({ sub: 'u1', email: 'prueba@teambenavides.pe', name: 'Usuaria de prueba' }))
  const manana = new Date(Date.now() + 86_400_000).toISOString()
  guardarSesion({
    accessToken: `e30.${claims}.firma`,
    accessTokenExpiration: manana,
    refreshToken: 'renovacion',
    refreshTokenExpiration: manana,
  })
  const yo: MeResponse = {
    id: 'u1',
    email: 'prueba@teambenavides.pe',
    nombreCompleto: 'Usuaria de prueba',
    activo: true,
    roles,
    permisos,
    clienteId: null,
  }
  return { 'GET /auth/me': yo }
}

/**
 * Monta la pantalla con los proveedores de la app. Con `patron` la pantalla queda en
 * una ruta con parámetros (por ejemplo «/ordenes/:id») y `ruta` es la URL que se abre.
 */
export function montar(pantalla: ReactElement, { ruta = '/', patron }: { ruta?: string; patron?: string } = {}) {
  const consultas = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <ConfigProvider locale={esES}>
      <AppAntd component={false}>
        <QueryClientProvider client={consultas}>
          <ProveedorSesion>
            <MemoryRouter initialEntries={[ruta]}>
              {patron ? (
                <Routes>
                  <Route path={patron} element={pantalla} />
                </Routes>
              ) : (
                pantalla
              )}
            </MemoryRouter>
          </ProveedorSesion>
        </QueryClientProvider>
      </AppAntd>
    </ConfigProvider>,
  )
}
