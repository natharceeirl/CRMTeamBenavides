import { createContext, use, useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { escucharSesion, iniciarSesion, sesionActual, terminarSesion } from '../api/http'
import type { Sesion } from '../api/http'
import { leerUsuarioDelToken } from './jwt'
import type { UsuarioSesion } from './jwt'

type ContextoSesion = {
  usuario: UsuarioSesion | null
  autenticado: boolean
  entrar: (email: string, password: string) => Promise<void>
  salir: () => void
}

const Contexto = createContext<ContextoSesion | null>(null)

export function ProveedorSesion({ children }: Readonly<{ children: ReactNode }>) {
  const [sesion, setSesion] = useState<Sesion | null>(() => sesionActual())

  // La sesión también cambia fuera de React: cuando el refresh falla, la capa
  // de API la borra y la app tiene que enterarse para mandar al login.
  useEffect(() => escucharSesion(setSesion), [])

  const entrar = useCallback(async (email: string, password: string) => {
    await iniciarSesion({ email, password })
  }, [])

  const salir = useCallback(() => {
    terminarSesion()
  }, [])

  const valor = useMemo<ContextoSesion>(
    () => ({
      usuario: sesion ? leerUsuarioDelToken(sesion.accessToken) : null,
      autenticado: sesion !== null,
      entrar,
      salir,
    }),
    [sesion, entrar, salir],
  )

  return <Contexto value={valor}>{children}</Contexto>
}

export function useSesion(): ContextoSesion {
  const contexto = use(Contexto)
  if (!contexto) {
    throw new Error('useSesion se usa dentro de ProveedorSesion')
  }
  return contexto
}
