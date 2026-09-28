import { NavLink, Outlet, useNavigate } from 'react-router'
import { Logo } from '../components/Logo'
import { ChatbotWidget } from '../components/ChatbotWidget'
import { useSesion } from '../auth/sesion'

type EnlaceMenu = {
  ruta: string
  texto: string
  exacto?: boolean
  permiso?: string
  permisos?: string[]
  roles?: string[]
}

const enlaces: EnlaceMenu[] = [
  { ruta: '/', texto: 'Tablero', exacto: true, permiso: 'reportes.ver_operativos' },
  { ruta: '/ordenes', texto: 'Órdenes', permisos: ['ordenes.ver_todas', 'ordenes.ver_asignadas'] },
  { ruta: '/clientes', texto: 'Clientes', permiso: 'clientes.ver' },
  { ruta: '/unidades', texto: 'Unidades', permiso: 'unidades.ver' },
  { ruta: '/repuestos', texto: 'Repuestos', permiso: 'inventario.ver' },
  { ruta: '/ventas', texto: 'Ventas', permiso: 'ventas.ver' },
  { ruta: '/reportes', texto: 'Reportes', permiso: 'reportes.ver_operativos' },
  { ruta: '/chatbot', texto: 'Chatbot', roles: ['Gerencia/Admin', 'Admin', 'Recepcion', 'Recepción'] },
  { ruta: '/usuarios', texto: 'Usuarios', permiso: 'usuarios.ver' },
]

export function AppLayout() {
  const { usuario, roles, esGerencia, tienePermiso, tieneAlgunPermiso, salir } = useSesion()
  const navigate = useNavigate()

  const cerrar = () => {
    salir()
    navigate('/login', { replace: true })
  }

  const enlacesVisibles = enlaces.filter((item) => {
    if (esGerencia) return true
    if (item.roles && !item.roles.some((r) => roles.includes(r))) {
      return false
    }
    if (item.permiso && !tienePermiso(item.permiso)) {
      return false
    }
    if (item.permisos && !tieneAlgunPermiso(item.permisos)) {
      return false
    }
    return true
  })

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="sidebar-logo">
          <Logo variante="oscuro" alto={26} />
        </div>
        <nav className="sidebar-nav" aria-label="Menú principal">
          {enlacesVisibles.map((enlace) => (
            <NavLink key={enlace.ruta} to={enlace.ruta} end={enlace.exacto}>
              {enlace.texto}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-pie">
          <strong>{usuario?.nombre ?? 'Usuario'}</strong>
          {roles.length > 0 ? roles.join(' · ') : usuario?.email}
          <button type="button" className="sidebar-salir" onClick={cerrar}>
            Cerrar sesión
          </button>
        </div>
      </aside>
      <div className="contenido">
        <Outlet />
      </div>
      <ChatbotWidget />
    </div>
  )
}
