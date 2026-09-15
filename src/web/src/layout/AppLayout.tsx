import { NavLink, Outlet } from 'react-router'
import { Logo } from '../components/Logo'
import { ChatbotWidget } from '../components/ChatbotWidget'
import { usuarioActual } from '../data/ejemplo'

const enlaces = [
  { ruta: '/', texto: 'Tablero', exacto: true },
  { ruta: '/ordenes', texto: 'Órdenes' },
  { ruta: '/clientes', texto: 'Clientes' },
  { ruta: '/unidades', texto: 'Unidades' },
  { ruta: '/repuestos', texto: 'Repuestos' },
  { ruta: '/ventas', texto: 'Ventas' },
  { ruta: '/reportes', texto: 'Reportes' },
  { ruta: '/usuarios', texto: 'Usuarios' },
]

export function AppLayout() {
  return (
    <div className="app">
      <aside className="sidebar">
        <div className="sidebar-logo">
          <Logo variante="oscuro" alto={26} />
        </div>
        <nav className="sidebar-nav" aria-label="Menú principal">
          {enlaces.map((enlace) => (
            <NavLink key={enlace.ruta} to={enlace.ruta} end={enlace.exacto}>
              {enlace.texto}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-pie">
          <strong>{usuarioActual.nombre}</strong>
          {usuarioActual.rol}
        </div>
      </aside>
      <div className="contenido">
        <Outlet />
      </div>
      <ChatbotWidget />
    </div>
  )
}
