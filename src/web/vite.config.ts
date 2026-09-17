import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// La API todavía no tiene CORS configurado, así que en desarrollo el navegador
// llama a /api en el mismo origen y Vite reenvía al backend. Si Paolo levanta
// el perfil https, exportar VITE_API_URL=https://localhost:7142.
const destinoApi = process.env.VITE_API_URL ?? 'http://localhost:5021'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: destinoApi,
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
