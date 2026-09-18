import { NavLink, Outlet, useNavigate } from 'react-router'
import { Logo } from '../components/Logo'
import { ChatbotWidget } from '../components/ChatbotWidget'
import { useSesion } from '../auth/sesion'

const enlaces = [
  { ruta: '/', texto: 'Tablero', exacto: true },
  { ruta: '/ordenes', texto: 'Órdenes' },
  { ruta: '/clientes', texto: 'Clientes' },
  { ruta: '/unidades', texto: 'Unidades' },
  { ruta: '/repuestos', texto: 'Repuestos' },
  { ruta: '/ventas', texto: 'Ventas' },
  { ruta: '/reportes', texto: 'Reportes' },
  { ruta: '/chatbot', texto: 'Chatbot' },
  { ruta: '/usuarios', texto: 'Usuarios' },
]

export function AppLayout() {
  const { usuario, roles, salir } = useSesion()
  const navigate = useNavigate()

  const cerrar = () => {
    salir()
    navigate('/login', { replace: true })
  }

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="sidebar-logo">
          <Logo variante="oscuro" alto={26} />
        </div>
        <nav className="sidebar-nav" aria-label="Menú principal">
          {/* El menú todavía no se filtra por permiso: la matriz de roles y
              permisos sigue pendiente de que el cliente la confirme. */}
          {enlaces.map((enlace) => (
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
