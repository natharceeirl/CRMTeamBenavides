import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { ConfigProvider } from 'antd'
import esES from 'antd/locale/es_ES'
import '@fontsource/ibm-plex-sans/400.css'
import '@fontsource/ibm-plex-sans/500.css'
import '@fontsource/ibm-plex-sans/600.css'
import '@fontsource/space-grotesk/500.css'
import '@fontsource/space-grotesk/700.css'
import './styles/global.css'
import { aplicarVariablesCss } from './theme/tokens'
import { temaAntd } from './theme/antdTheme'
import App from './App'

aplicarVariablesCss()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ConfigProvider locale={esES} theme={temaAntd}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </ConfigProvider>
  </StrictMode>,
)
