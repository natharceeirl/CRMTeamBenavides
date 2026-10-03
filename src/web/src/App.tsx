import { Navigate, Route, Routes } from 'react-router'
import { RutaProtegida } from './auth/RutaProtegida'
import { RutaConPermiso } from './auth/RutaConPermiso'
import { ACCESO_CHATBOT, ACCESO_ORDENES, ACCESO_PORTAL, PERMISOS } from './auth/acceso'
import { AppLayout } from './layout/AppLayout'
import { LoginPage } from './pages/LoginPage'
import { InicioPage } from './pages/InicioPage'
import { SinAccesoPage } from './pages/SinAccesoPage'
import { PortalClientePage } from './pages/PortalClientePage'
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
import { ChatbotPage } from './pages/ChatbotPage'
import { AuditoriaPage } from './pages/AuditoriaPage'
import { CajaPage } from './pages/CajaPage'
import { ConfiguracionPage } from './pages/ConfiguracionPage'
import { CitasPage } from './pages/CitasPage'
import { PedidosLimaPage } from './pages/PedidosLimaPage'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RutaProtegida />}>
        <Route element={<AppLayout />}>
          <Route index element={<InicioPage />} />
          <Route path="sin-acceso" element={<SinAccesoPage />} />
          <Route element={<RutaConPermiso {...ACCESO_PORTAL} />}>
            <Route path="portal" element={<PortalClientePage />} />
          </Route>
          <Route element={<RutaConPermiso {...ACCESO_ORDENES} />}>
            <Route path="ordenes" element={<OrdenesPage />} />
            <Route path="ordenes/:id" element={<OrdenDetallePage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.ordenesCrear} />}>
            <Route path="ordenes/nueva" element={<NuevaOrdenPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.citasVer} />}>
            <Route path="citas" element={<CitasPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.clientesVer} />}>
            <Route path="clientes" element={<ClientesPage />} />
            <Route path="clientes/:id" element={<ClienteDetallePage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.unidadesVer} />}>
            <Route path="unidades" element={<UnidadesPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.inventarioVer} />}>
            <Route path="repuestos" element={<RepuestosPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.pedidosLimaVer} />}>
            <Route path="pedidos-lima" element={<PedidosLimaPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.ventasVer} />}>
            <Route path="ventas" element={<VentasPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.reportesVerOperativos} />}>
            <Route path="reportes" element={<ReportesPage />} />
          </Route>
          <Route element={<RutaConPermiso {...ACCESO_CHATBOT} />}>
            <Route path="chatbot" element={<ChatbotPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.usuariosVer} />}>
            <Route path="usuarios" element={<UsuariosPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.cajaConsultar} />}>
            <Route path="caja" element={<CajaPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.auditoriaVer} />}>
            <Route path="auditoria" element={<AuditoriaPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.configuracionEditar} />}>
            <Route path="configuracion" element={<ConfiguracionPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
