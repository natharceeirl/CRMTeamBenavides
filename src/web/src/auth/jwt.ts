export type UsuarioSesion = {
  id: string
  email: string
  nombre: string
}

type ClaimsToken = {
  sub?: string
  email?: string
  name?: string
}

/**
 * Saca los datos del usuario del access token.
 *
 * Provisional: el backend no expone `GET /api/auth/me` y el token solo trae los
 * claims sub, email, name y jti. En cuanto Paolo agregue el usuario con sus
 * roles a la respuesta del login, esto se reemplaza por ese dato.
 */
export function leerUsuarioDelToken(token: string): UsuarioSesion | null {
  const cuerpo = token.split('.')[1]
  if (!cuerpo) {
    return null
  }

  try {
    const base64 = cuerpo.replace(/-/g, '+').replace(/_/g, '/')
    const texto = decodeURIComponent(
      atob(base64)
        .split('')
        .map((caracter) => `%${caracter.charCodeAt(0).toString(16).padStart(2, '0')}`)
        .join(''),
    )
    const claims = JSON.parse(texto) as ClaimsToken

    if (!claims.sub) {
      return null
    }

    return {
      id: claims.sub,
      email: claims.email ?? '',
      nombre: claims.name ?? claims.email ?? 'Usuario',
    }
  } catch {
    return null
  }
}
