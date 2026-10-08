import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { Logo } from '../components/Logo'
import { ChatbotWidget } from '../components/ChatbotWidget'
import { ModalCambiarPassword } from '../components/ModalCambiarPassword'
import { useSesion } from '../auth/sesion'
import { cumpleAcceso, enlaces } from '../auth/acceso'
import { useTipoCambio } from '../api/configuracion'
import { resumenTipoCambio } from '../utils/tipoCambio'

export function AppLayout() {
  const sesion = useSesion()
  const { usuario, roles, salir } = sesion
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const [cambiarPassword, setCambiarPassword] = useState(false)
  // En el celular el menú se pliega bajo un botón; en pantallas más anchas siempre se ve.
  const [menuAbierto, setMenuAbierto] = useState(false)
  useEffect(() => setMenuAbierto(false), [pathname])
  // El tipo de cambio es del personal: el cliente en su portal no lo necesita.
  const tipoCambio = useTipoCambio(sesion.esPersonal)
  const textoTipoCambio = sesion.esPersonal ? resumenTipoCambio(tipoCambio.data) : null

  const cerrar = () => {
    salir()
    navigate('/login', { replace: true })
  }


  const enlacesVisibles = enlaces.filter((enlace) => cumpleAcceso(enlace, sesion))

  return (
    <div className="app">
      <aside className={menuAbierto ? 'sidebar abierta' : 'sidebar'}>
        <div className="sidebar-logo">
          <Logo variante="oscuro" alto={26} />
          {textoTipoCambio && (
            <div className="sidebar-tipo-cambio" title="Tipo de cambio de venta registrado en Configuración">
              {textoTipoCambio}
            </div>
          )}
        </div>
        <button
          type="button"
          className="sidebar-alternar"
          aria-expanded={menuAbierto}
          aria-controls="menu-principal"
          onClick={() => setMenuAbierto((abierto) => !abierto)}
        >
          {menuAbierto ? 'Cerrar' : 'Menú'}
        </button>
        <nav id="menu-principal" className="sidebar-nav" aria-label="Menú principal">
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
