import { describe, expect, it } from 'vitest'
import { ACCESO_ORDENES, ACCESO_PORTAL, PERMISOS, cumpleAcceso, enlaces, rutaInicial } from './acceso'

/** Sesión mínima con los permisos que da el backend a cada rol (RolSeeder). */
function sesionCon(roles: string[], permisos: string[]) {
  const esGerencia = roles.includes('Gerencia/Admin')
  return {
    esGerencia,
    roles,
    tienePermiso: (permiso: string) => esGerencia || permisos.includes(permiso),
    tieneAlgunPermiso: (lista: string[]) => esGerencia || lista.some((permiso) => permisos.includes(permiso)),
  }
}

const tecnico = sesionCon(
  ['Tecnico'],
  [
    PERMISOS.ordenesVerAsignadas,
    PERMISOS.ordenesDiagnostico,
    PERMISOS.ordenesAgregarItems,
    PERMISOS.ordenesCambiarEstado,
    PERMISOS.unidadesVer,
    PERMISOS.inventarioVer,
  ],
)

const vendedor = sesionCon(
  ['Vendedor'],
  [PERMISOS.ventasVer, PERMISOS.ventasCrear, PERMISOS.clientesVer, PERMISOS.inventarioVer, PERMISOS.reportesVerOperativos],
)

describe('cumpleAcceso', () => {
  it('deja pasar a Gerencia a todo', () => {
    const gerencia = sesionCon(['Gerencia/Admin'], [])
    expect(cumpleAcceso({ permiso: PERMISOS.usuariosVer }, gerencia)).toBe(true)
    expect(cumpleAcceso({ roles: ['Recepcion'] }, gerencia)).toBe(true)
  })

  it('pide el permiso exacto', () => {
    expect(cumpleAcceso({ permiso: PERMISOS.inventarioVer }, tecnico)).toBe(true)
    expect(cumpleAcceso({ permiso: PERMISOS.usuariosVer }, tecnico)).toBe(false)
  })

  it('con varios permisos basta uno', () => {
    expect(cumpleAcceso(ACCESO_ORDENES, tecnico)).toBe(true)
    expect(cumpleAcceso(ACCESO_ORDENES, vendedor)).toBe(false)
  })

  it('por rol exige tener uno de los roles', () => {
    expect(cumpleAcceso({ roles: ['Recepcion'] }, vendedor)).toBe(false)
  })

  it('solo quien edita la configuración ve la opción en el menú', () => {
    const configuracion = enlaces.find((enlace) => enlace.ruta === '/configuracion')!
    expect(cumpleAcceso(configuracion, sesionCon(['Gerencia/Admin'], []))).toBe(true)
    expect(cumpleAcceso(configuracion, vendedor)).toBe(false)
    expect(cumpleAcceso(configuracion, tecnico)).toBe(false)
  })

  it('cliente solo puede entrar al portal y no a pantallas de taller', () => {
    const cliente = sesionCon(['Cliente'], [PERMISOS.portalAcceso, PERMISOS.clientesVer, PERMISOS.unidadesVer, PERMISOS.ordenesVerAsignadas])
    expect(cumpleAcceso(ACCESO_PORTAL, cliente)).toBe(true)
    expect(cumpleAcceso(ACCESO_ORDENES, cliente)).toBe(false)
    expect(cumpleAcceso({ permiso: PERMISOS.clientesVer }, cliente)).toBe(false)
    expect(cumpleAcceso({ permiso: PERMISOS.unidadesVer }, cliente)).toBe(false)
  })
})

describe('rutaInicial', () => {
  it('manda al técnico a sus órdenes porque no ve el tablero', () => {
    expect(rutaInicial(tecnico)).toBe('/ordenes')
  })

  it('deja al vendedor en el tablero', () => {
    expect(rutaInicial(vendedor)).toBe('/')
  })

  it('manda al cliente a su portal cuando tiene portal.acceso', () => {
    expect(rutaInicial(sesionCon(['Cliente'], [PERMISOS.portalAcceso]))).toBe('/portal')
  })

  it('devuelve null si no puede entrar a nada', () => {
    expect(rutaInicial(sesionCon(['Cliente'], []))).toBeNull()
    expect(rutaInicial(sesionCon([], []))).toBeNull()
  })
})
