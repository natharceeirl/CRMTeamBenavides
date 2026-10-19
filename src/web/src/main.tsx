import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { App as AppAntd, ConfigProvider } from 'antd'
import esES from 'antd/locale/es_ES'
import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
// Solo el subconjunto latino (incluye tildes, ñ y rayas): el cirílico y el latino
// extendido no se usan y se sumaban a la descarga.
import '@fontsource/ibm-plex-sans/latin-400.css'
import '@fontsource/ibm-plex-sans/latin-500.css'
import '@fontsource/ibm-plex-sans/latin-600.css'
import '@fontsource/space-grotesk/latin-500.css'
import '@fontsource/space-grotesk/latin-700.css'
import './styles/global.css'
import { aplicarVariablesCss } from './theme/tokens'
import { temaAntd } from './theme/antdTheme'
import { ProveedorSesion } from './auth/sesion'
import { textoDeAviso } from './api/avisos'
import { avisos } from './utils/avisos'
import { PuenteAvisos } from './components/PuenteAvisos'
import { BarreraErrores } from './components/BarreraErrores'
import App from './App'

aplicarVariablesCss()

const clienteConsultas = new QueryClient({
  // Cada mutación trae su aviso de éxito en `meta.exito` (ver api/avisos.ts).
  mutationCache: new MutationCache({
    onSuccess: (respuesta, variables, _contexto, mutacion) => {
      const texto = textoDeAviso(mutacion.meta, variables, respuesta)
      if (texto) avisos.exito(texto)
    },
  }),
  defaultOptions: {
    queries: {
      // Un 401 lo resuelve la capa de API renovando el token, no el reintento.
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ConfigProvider locale={esES} theme={temaAntd}>
      <AppAntd component={false}>
        <PuenteAvisos />
        <QueryClientProvider client={clienteConsultas}>
          <ProveedorSesion>
            <BrowserRouter>
              <BarreraErrores alcance="aplicacion">
                <App />
              </BarreraErrores>
            </BrowserRouter>
          </ProveedorSesion>
        </QueryClientProvider>
      </AppAntd>
    </ConfigProvider>
  </StrictMode>,
)
