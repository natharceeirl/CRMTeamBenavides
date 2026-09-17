import { Navigate, Outlet, useLocation } from 'react-router'
import { useSesion } from './sesion'

/** Cualquier pantalla dentro de esta ruta exige sesión iniciada. */
export function RutaProtegida() {
  const { autenticado } = useSesion()
  const ubicacion = useLocation()

  if (!autenticado) {
    return <Navigate to="/login" replace state={{ desde: ubicacion.pathname }} />
  }

  return <Outlet />
}
