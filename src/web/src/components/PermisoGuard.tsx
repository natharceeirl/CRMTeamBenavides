import type { ReactNode } from 'react'
import { useSesion } from '../auth/sesion'

export type PermisoGuardProps = {
  children: ReactNode
  permiso?: string
  permisos?: string[]
  requiereTodos?: boolean
  rol?: string
  roles?: string[]
  fallback?: ReactNode
}

/**
 * Renderiza sus hijos únicamente si el usuario autenticado cumple con las
 * condiciones de permiso o rol requeridas (o si es Gerencia/Admin).
 */
export function PermisoGuard({
  children,
  permiso,
  permisos,
  requiereTodos = false,
  rol,
  roles,
  fallback = null,
}: PermisoGuardProps) {
  const { esGerencia, tienePermiso, tieneAlgunPermiso, roles: rolesUsuario } = useSesion()

  if (esGerencia) {
    return <>{children}</>
  }

  // Comprobar rol único
  if (rol && !rolesUsuario.includes(rol)) {
    return <>{fallback}</>
  }

  // Comprobar múltiples roles
  if (roles && roles.length > 0) {
    const tieneRol = roles.some((r) => rolesUsuario.includes(r))
    if (!tieneRol) {
      return <>{fallback}</>
    }
  }

  // Comprobar permiso único
  if (permiso && !tienePermiso(permiso)) {
    return <>{fallback}</>
  }

  // Comprobar lista de permisos
  if (permisos && permisos.length > 0) {
    if (requiereTodos) {
      const cumpleTodos = permisos.every((p) => tienePermiso(p))
      if (!cumpleTodos) return <>{fallback}</>
    } else {
      const cumpleAlguno = tieneAlgunPermiso(permisos)
      if (!cumpleAlguno) return <>{fallback}</>
    }
  }

  return <>{children}</>
}
