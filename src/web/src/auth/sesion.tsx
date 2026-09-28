import { createContext, use, useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { escucharSesion, iniciarSesion, sesionActual, terminarSesion } from '../api/http'
import type { Sesion } from '../api/http'
import { clavesUsuarioActual, useUsuarioActual } from '../api/usuarioActual'
import { leerUsuarioDelToken } from './jwt'
import type { UsuarioSesion } from './jwt'

export const ROLES = {
  GERENCIA_ADMIN: 'Gerencia/Admin',
  RECEPCION: 'Recepcion',
  TECNICO: 'Tecnico',
  VENDEDOR: 'Vendedor',
  CLIENTE: 'Cliente',
} as const

type ContextoSesion = {
  usuario: UsuarioSesion | null
  /** Roles del usuario, desde /api/auth/me. Vacío mientras carga. */
  roles: string[]
  /** Permisos del usuario, desde /api/auth/me. */
  permisos: string[]
  /** ID del cliente si el usuario pertenece al rol Cliente. */
  clienteId: string | null
  autenticado: boolean
  esGerencia: boolean
  esRecepcion: boolean
  esTecnico: boolean
  esVendedor: boolean
  esCliente: boolean
  tienePermiso: (permiso: string) => boolean
  tieneAlgunPermiso: (permisos: string[]) => boolean
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

  const roles = useMemo(() => yo.data?.roles ?? [], [yo.data?.roles])
  const permisos = useMemo(() => yo.data?.permisos ?? [], [yo.data?.permisos])
  const clienteId = yo.data?.clienteId ?? null

  const esGerencia = roles.includes('Gerencia/Admin') || roles.includes('Admin')
  const esRecepcion = roles.includes('Recepcion') || roles.includes('Recepción')
  const esTecnico = roles.includes('Tecnico') || roles.includes('Técnico')
  const esVendedor = roles.includes('Vendedor')
  const esCliente = roles.includes('Cliente')

  const tienePermiso = useCallback(
    (permiso: string) => {
      if (esGerencia) return true
      return permisos.includes(permiso)
    },
    [esGerencia, permisos]
  )

  const tieneAlgunPermiso = useCallback(
    (listaPermisos: string[]) => {
      if (esGerencia) return true
      return listaPermisos.some((p) => permisos.includes(p))
    },
    [esGerencia, permisos]
  )

  const valor = useMemo<ContextoSesion>(() => {
    const delToken = sesion ? leerUsuarioDelToken(sesion.accessToken) : null
    const usuario = yo.data
      ? { id: yo.data.id, email: yo.data.email, nombre: yo.data.nombreCompleto }
      : delToken

    return {
      usuario,
      roles,
      permisos,
      clienteId,
      autenticado: sesion !== null,
      esGerencia,
      esRecepcion,
      esTecnico,
      esVendedor,
      esCliente,
      tienePermiso,
      tieneAlgunPermiso,
      entrar,
      salir,
    }
  }, [
    sesion,
    yo.data,
    roles,
    permisos,
    clienteId,
    esGerencia,
    esRecepcion,
    esTecnico,
    esVendedor,
    esCliente,
    tienePermiso,
    tieneAlgunPermiso,
    entrar,
    salir,
  ])

  return <Contexto value={valor}>{children}</Contexto>
}

export function useSesion(): ContextoSesion {
  const contexto = use(Contexto)
  if (!contexto) {
    throw new Error('useSesion se usa dentro de ProveedorSesion')
  }
  return contexto
}
