import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { ConfigProvider } from 'antd'
import esES from 'antd/locale/es_ES'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '@fontsource/ibm-plex-sans/400.css'
import '@fontsource/ibm-plex-sans/500.css'
import '@fontsource/ibm-plex-sans/600.css'
import '@fontsource/space-grotesk/500.css'
import '@fontsource/space-grotesk/700.css'
import './styles/global.css'
import { aplicarVariablesCss } from './theme/tokens'
import { temaAntd } from './theme/antdTheme'
import { ProveedorSesion } from './auth/sesion'
import App from './App'

aplicarVariablesCss()

const clienteConsultas = new QueryClient({
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
      <QueryClientProvider client={clienteConsultas}>
        <ProveedorSesion>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </ProveedorSesion>
      </QueryClientProvider>
    </ConfigProvider>
  </StrictMode>,
)
