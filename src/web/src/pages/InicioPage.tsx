import { Navigate } from 'react-router'
import { useSesion } from '../auth/sesion'
import { PERMISOS, rutaInicial } from '../auth/acceso'
import { TableroPage } from './TableroPage'

/**
 * La raíz muestra el tablero a quien puede verlo. El resto entra a la primera
 * pantalla del menú que tiene permitida: el técnico, por ejemplo, a sus órdenes.
 */
export function InicioPage() {
  const sesion = useSesion()

  if (!sesion.permisosListos) {
    return null
  }

  if (sesion.tienePermiso(PERMISOS.reportesVerOperativos)) {
    return <TableroPage />
  }

  const ruta = rutaInicial(sesion)
  return <Navigate to={ruta && ruta !== '/' ? ruta : '/sin-acceso'} replace />
}
