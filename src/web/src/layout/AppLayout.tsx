import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router'
import { Logo } from '../components/Logo'
import { ChatbotWidget } from '../components/ChatbotWidget'
import { ModalCambiarPassword } from '../components/ModalCambiarPassword'
import { useSesion } from '../auth/sesion'
import { cumpleAcceso, enlaces } from '../auth/acceso'

export function AppLayout() {
  const sesion = useSesion()
  const { usuario, roles, salir } = sesion
  const navigate = useNavigate()
  const [cambiarPassword, setCambiarPassword] = useState(false)

  const cerrar = () => {
    salir()
    navigate('/login', { replace: true })
  }

  // La web es del personal del taller y la tienda. Un cliente usa la app.
  if (sesion.permisosListos && sesion.esCliente && !sesion.esPersonal) {
    return (
      <div className="login">
        <div className="login-marca">
          <Logo variante="oscuro" alto={40} />
        </div>
        <div className="login-formulario">
          <h2 className="login-titulo">Tu cuenta es de cliente</h2>
          <p>Sigue tus unidades, órdenes y comprobantes desde la app de Team Benavides.</p>
          <button type="button" className="sidebar-salir" onClick={cerrar}>
            Cerrar sesión
          </button>
        </div>
      </div>
    )
  }

  const enlacesVisibles = enlaces.filter((enlace) => cumpleAcceso(enlace, sesion))

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
          <button type="button" className="sidebar-salir" onClick={() => setCambiarPassword(true)}>
            Cambiar contraseña
          </button>
          <button type="button" className="sidebar-salir" onClick={cerrar}>
            Cerrar sesión
          </button>
        </div>
      </aside>
      <div className="contenido">
        <Outlet />
      </div>
      <ChatbotWidget />
      <ModalCambiarPassword abierto={cambiarPassword} onCerrar={() => setCambiarPassword(false)} />
    </div>
  )
}
