import { Navigate, Outlet } from 'react-router'
import { useSesion } from './sesion'
import { cumpleAcceso, type Acceso } from './acceso'

/**
 * Las pantallas dentro de esta ruta exigen el permiso indicado. Sin él se va a
 * «Sin acceso» en vez de mostrar una pantalla que solo recibiría 403.
 */
export function RutaConPermiso(acceso: Readonly<Acceso>) {
  const sesion = useSesion()

  // Hasta que llega /api/auth/me no se sabe qué puede ver: no se redirige a ciegas.
  if (!sesion.permisosListos) {
    return null
  }

  if (!cumpleAcceso(acceso, sesion)) {
    return <Navigate to="/sin-acceso" replace />
  }

  return <Outlet />
}
