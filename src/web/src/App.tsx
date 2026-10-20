import { lazy, Suspense, type ComponentType } from 'react'
import { Navigate, Route, Routes } from 'react-router'
import { RutaProtegida } from './auth/RutaProtegida'
import { RutaConPermiso } from './auth/RutaConPermiso'
import { ACCESO_CHATBOT, ACCESO_ORDENES, ACCESO_PORTAL, PERMISOS } from './auth/acceso'
import { AppLayout } from './layout/AppLayout'
import { LoginPage } from './pages/LoginPage'

/**
 * Cada pantalla se descarga recién al abrirla, así la primera carga no las trae todas.
 * Las páginas exportan con nombre y `lazy` espera un `default`.
 */
function pantalla<M, K extends keyof M>(importar: () => Promise<M>, nombre: K) {
  return lazy(async () => ({ default: (await importar())[nombre] as ComponentType }))
}

const InicioPage = pantalla(() => import('./pages/InicioPage'), 'InicioPage')
const SinAccesoPage = pantalla(() => import('./pages/SinAccesoPage'), 'SinAccesoPage')
const PortalClientePage = pantalla(() => import('./pages/PortalClientePage'), 'PortalClientePage')
const OrdenesPage = pantalla(() => import('./pages/OrdenesPage'), 'OrdenesPage')
const NuevaOrdenPage = pantalla(() => import('./pages/NuevaOrdenPage'), 'NuevaOrdenPage')
const OrdenDetallePage = pantalla(() => import('./pages/OrdenDetallePage'), 'OrdenDetallePage')
const ClientesPage = pantalla(() => import('./pages/ClientesPage'), 'ClientesPage')
const ClienteDetallePage = pantalla(() => import('./pages/ClienteDetallePage'), 'ClienteDetallePage')
const UnidadesPage = pantalla(() => import('./pages/UnidadesPage'), 'UnidadesPage')
const RepuestosPage = pantalla(() => import('./pages/RepuestosPage'), 'RepuestosPage')
const VentasPage = pantalla(() => import('./pages/VentasPage'), 'VentasPage')
const ReportesPage = pantalla(() => import('./pages/ReportesPage'), 'ReportesPage')
const UsuariosPage = pantalla(() => import('./pages/UsuariosPage'), 'UsuariosPage')
const ChatbotPage = pantalla(() => import('./pages/ChatbotPage'), 'ChatbotPage')
const AuditoriaPage = pantalla(() => import('./pages/AuditoriaPage'), 'AuditoriaPage')
const CajaPage = pantalla(() => import('./pages/CajaPage'), 'CajaPage')
const ConfiguracionPage = pantalla(() => import('./pages/ConfiguracionPage'), 'ConfiguracionPage')
const CitasPage = pantalla(() => import('./pages/CitasPage'), 'CitasPage')
const PedidosLimaPage = pantalla(() => import('./pages/PedidosLimaPage'), 'PedidosLimaPage')
const AprobacionesPage = pantalla(() => import('./pages/AprobacionesPage'), 'AprobacionesPage')
const ComprasPage = pantalla(() => import('./pages/ComprasPage'), 'ComprasPage')
const NuevaCompraPage = pantalla(() => import('./pages/NuevaCompraPage'), 'NuevaCompraPage')
const ImportacionesPage = pantalla(() => import('./pages/ImportacionesPage'), 'ImportacionesPage')
const PrivacidadPage = pantalla(() => import('./pages/PrivacidadPage'), 'PrivacidadPage')

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      {/* Pública: la enlazan la app y las tiendas, sin iniciar sesión. */}
      <Route
        path="/privacidad"
        element={
          <Suspense fallback={null}>
            <PrivacidadPage />
          </Suspense>
        }
      />
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
          <Route element={<RutaConPermiso permiso={PERMISOS.comprasVer} />}>
            <Route path="compras" element={<ComprasPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.comprasRegistrar} />}>
            <Route path="compras/nueva" element={<NuevaCompraPage />} />
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
          <Route element={<RutaConPermiso permiso={PERMISOS.ordenesAprobarGerencia} />}>
            <Route path="aprobaciones" element={<AprobacionesPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.cajaConsultar} />}>
            <Route path="caja" element={<CajaPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.auditoriaVer} />}>
            <Route path="auditoria" element={<AuditoriaPage />} />
          </Route>
          <Route element={<RutaConPermiso permiso={PERMISOS.configuracionEditar} />}>
            <Route path="configuracion" element={<ConfiguracionPage />} />
            <Route path="importaciones" element={<ImportacionesPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
