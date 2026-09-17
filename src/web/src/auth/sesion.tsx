import { createContext, use, useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { escucharSesion, iniciarSesion, sesionActual, terminarSesion } from '../api/http'
import type { Sesion } from '../api/http'
import { clavesUsuarioActual, useUsuarioActual } from '../api/usuarioActual'
import { leerUsuarioDelToken } from './jwt'
import type { UsuarioSesion } from './jwt'

type ContextoSesion = {
  usuario: UsuarioSesion | null
  /** Roles del usuario, desde /api/auth/me. Vacío mientras carga. */
  roles: string[]
  autenticado: boolean
  entrar: (email: string, password: string) => Promise<void>
  salir: () => void
}

const Contexto = createContext<ContextoSesion | null>(null)

export function ProveedorSesion({ children }: Readonly<{ children: ReactNode }>) {
  const [sesion, setSesion] = useState<Sesion | null>(() => sesionActual())
  const consultas = useQueryClient()

  // La sesión también cambia fuera de React: cuando el refresh falla, la capa
  // de API la borra y la app tiene que enterarse para mandar al login.
  useEffect(() => escucharSesion(setSesion), [])

  // El nombre sale del token al instante; /api/auth/me lo confirma y agrega los roles.
  const yo = useUsuarioActual(sesion !== null)

  const entrar = useCallback(async (email: string, password: string) => {
    await iniciarSesion({ email, password })
  }, [])

  const salir = useCallback(() => {
    terminarSesion()
    consultas.removeQueries({ queryKey: clavesUsuarioActual.yo })
  }, [consultas])

  const valor = useMemo<ContextoSesion>(() => {
    const delToken = sesion ? leerUsuarioDelToken(sesion.accessToken) : null
    const usuario = yo.data
      ? { id: yo.data.id, email: yo.data.email, nombre: yo.data.nombreCompleto }
      : delToken

    return {
      usuario,
      roles: yo.data?.roles ?? [],
      autenticado: sesion !== null,
      entrar,
      salir,
    }
  }, [sesion, yo.data, entrar, salir])

  return <Contexto value={valor}>{children}</Contexto>
}

export function useSesion(): ContextoSesion {
  const contexto = use(Contexto)
  if (!contexto) {
    throw new Error('useSesion se usa dentro de ProveedorSesion')
  }
  return contexto
}
