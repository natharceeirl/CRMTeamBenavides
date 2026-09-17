import { Navigate, Route, Routes } from 'react-router'
import { RutaProtegida } from './auth/RutaProtegida'
import { AppLayout } from './layout/AppLayout'
import { LoginPage } from './pages/LoginPage'
import { TableroPage } from './pages/TableroPage'
import { OrdenesPage } from './pages/OrdenesPage'
import { NuevaOrdenPage } from './pages/NuevaOrdenPage'
import { OrdenDetallePage } from './pages/OrdenDetallePage'
import { ClientesPage } from './pages/ClientesPage'
import { ClienteDetallePage } from './pages/ClienteDetallePage'
import { UnidadesPage } from './pages/UnidadesPage'
import { RepuestosPage } from './pages/RepuestosPage'
import { VentasPage } from './pages/VentasPage'
import { ReportesPage } from './pages/ReportesPage'
import { UsuariosPage } from './pages/UsuariosPage'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RutaProtegida />}>
        <Route element={<AppLayout />}>
          <Route index element={<TableroPage />} />
          <Route path="ordenes" element={<OrdenesPage />} />
          <Route path="ordenes/nueva" element={<NuevaOrdenPage />} />
          <Route path="ordenes/:numero" element={<OrdenDetallePage />} />
          <Route path="clientes" element={<ClientesPage />} />
          <Route path="clientes/:id" element={<ClienteDetallePage />} />
          <Route path="unidades" element={<UnidadesPage />} />
          <Route path="repuestos" element={<RepuestosPage />} />
          <Route path="ventas" element={<VentasPage />} />
          <Route path="reportes" element={<ReportesPage />} />
          <Route path="usuarios" element={<UsuariosPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
