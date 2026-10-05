/**
 * Códigos de permiso del backend (PermisosDefinidos en
 * src/CRMTeamBenavides.Api/Configuration/Autorizacion/Permisos.cs).
 *
 * La API es la que decide: aquí solo se usan para no mostrar lo que igual
 * respondería 403.
 */
export const PERMISOS = {
  usuariosVer: 'usuarios.ver',
  usuariosCrear: 'usuarios.crear',
  usuariosEditar: 'usuarios.editar',
  usuariosEliminar: 'usuarios.eliminar',
  usuariosResetPassword: 'usuarios.reset_password',
  rolesVer: 'roles.ver',
  rolesGestionar: 'roles.gestionar',
  clientesVer: 'clientes.ver',
  clientesCrear: 'clientes.crear',
  clientesEditar: 'clientes.editar',
  clientesEliminar: 'clientes.eliminar',
  unidadesVer: 'unidades.ver',
  unidadesCrear: 'unidades.crear',
  unidadesEditar: 'unidades.editar',
  unidadesEliminar: 'unidades.eliminar',
  ordenesVerTodas: 'ordenes.ver_todas',
  ordenesVerAsignadas: 'ordenes.ver_asignadas',
  ordenesCrear: 'ordenes.crear',
  ordenesEditar: 'ordenes.editar',
  ordenesDiagnostico: 'ordenes.diagnostico',
  ordenesCambiarEstado: 'ordenes.cambiar_estado',
  ordenesAsignarTecnico: 'ordenes.asignar_tecnico',
  ordenesAprobarGerencia: 'ordenes.aprobar_gerencia',
  ordenesAgregarItems: 'ordenes.agregar_items',
  preciosModificar: 'precios.modificar',
  serviciosVer: 'servicios.ver',
  serviciosCrear: 'servicios.crear',
  serviciosEditar: 'servicios.editar',
  serviciosEliminar: 'servicios.eliminar',
  inventarioVer: 'inventario.ver',
  inventarioCrear: 'inventario.crear',
  inventarioEditar: 'inventario.editar',
  inventarioAjustar: 'inventario.ajustar',
  inventarioEliminar: 'inventario.eliminar',
  ventasVer: 'ventas.ver',
  ventasCrear: 'ventas.crear',
  ventasAnular: 'ventas.anular',
  descuentosAplicar: 'descuentos.aplicar',
  cajaConsultar: 'caja.consultar',
  cajaRegistrarIngreso: 'caja.registrar_ingreso',
  cajaRegistrarEgreso: 'caja.registrar_egreso',
  cajaAperturar: 'caja.aperturar',
  cajaCerrar: 'caja.cerrar',
  citasVer: 'citas.ver',
  citasCrear: 'citas.crear',
  citasEditar: 'citas.editar',
  citasCancelar: 'citas.cancelar',
  pedidosLimaVer: 'pedidos_lima.ver',
  pedidosLimaCrear: 'pedidos_lima.crear',
  pedidosLimaEditar: 'pedidos_lima.editar',
  pedidosLimaDespachar: 'pedidos_lima.despachar',
  pedidosLimaCancelar: 'pedidos_lima.cancelar',
  reportesVerOperativos: 'reportes.ver_operativos',
  reportesVerFinancieros: 'reportes.ver_financieros',
  configuracionEditar: 'configuracion.editar',
  auditoriaVer: 'auditoria.ver',
  portalAcceso: 'portal.acceso',
} as const

/** Quién puede entrar a una pantalla: basta con cumplir una de las condiciones que tenga. */
export type Acceso = {
  permiso?: string
  permisos?: string[]
  roles?: string[]
}

type SesionAcceso = {
  esGerencia: boolean
  roles: string[]
  tienePermiso: (permiso: string) => boolean
  tieneAlgunPermiso: (permisos: string[]) => boolean
}

const ROLES_PERSONAL = ['Gerencia/Admin', 'Admin', 'Recepcion', 'Recepción', 'Tecnico', 'Técnico', 'Vendedor']

export function esSoloCliente(sesion: SesionAcceso): boolean {
  return sesion.roles.includes('Cliente') && !sesion.roles.some((rol) => ROLES_PERSONAL.includes(rol))
}

const esPantallaDePortal = (acceso: Acceso) =>
  acceso.permiso === PERMISOS.portalAcceso || (acceso.permisos?.includes(PERMISOS.portalAcceso) ?? false)

export function cumpleAcceso(acceso: Acceso, sesion: SesionAcceso): boolean {
  // El portal es la vista del cliente. El personal no la usa, ni siquiera
  // Gerencia, que tiene todos los permisos.
  if (esPantallaDePortal(acceso)) {
    return esSoloCliente(sesion) && sesion.tienePermiso(PERMISOS.portalAcceso)
  }

  // Un cliente solo puede ingresar al Portal del Cliente; nunca a pantallas operativas o administrativas del taller
  if (esSoloCliente(sesion)) return false

  if (sesion.esGerencia) return true

  if (acceso.roles && !acceso.roles.some((rol) => sesion.roles.includes(rol))) return false
  if (acceso.permiso && !sesion.tienePermiso(acceso.permiso)) return false
  if (acceso.permisos && !sesion.tieneAlgunPermiso(acceso.permisos)) return false
  return true
}

export type EnlaceMenu = Acceso & {
  ruta: string
  texto: string
  exacto?: boolean
}

export const ACCESO_PORTAL: Acceso = {
  permiso: PERMISOS.portalAcceso,
}

export const ACCESO_ORDENES: Acceso = {
  permisos: [PERMISOS.ordenesVerTodas, PERMISOS.ordenesVerAsignadas],
}

/** La bandeja del chatbot no tiene permiso propio en el backend: se limita por rol. */
export const ACCESO_CHATBOT: Acceso = {
  roles: ['Gerencia/Admin', 'Admin', 'Recepcion', 'Recepción'],
}

/** El menú, en orden. La primera pantalla permitida es la de inicio del usuario. */
export const enlaces: EnlaceMenu[] = [
  { ruta: '/portal', texto: 'Mi Portal', exacto: true, permiso: PERMISOS.portalAcceso },
  { ruta: '/', texto: 'Tablero', exacto: true, permiso: PERMISOS.reportesVerOperativos },
  { ruta: '/ordenes', texto: 'Órdenes', ...ACCESO_ORDENES },
  { ruta: '/citas', texto: 'Agenda', permiso: PERMISOS.citasVer },
  { ruta: '/clientes', texto: 'Clientes', permiso: PERMISOS.clientesVer },
  { ruta: '/unidades', texto: 'Unidades', permiso: PERMISOS.unidadesVer },
  { ruta: '/repuestos', texto: 'Repuestos', permiso: PERMISOS.inventarioVer },
  { ruta: '/pedidos-lima', texto: 'Pedidos a Lima', permiso: PERMISOS.pedidosLimaVer },
  { ruta: '/ventas', texto: 'Ventas', permiso: PERMISOS.ventasVer },
  { ruta: '/caja', texto: 'Caja chica', permiso: PERMISOS.cajaConsultar },
  { ruta: '/reportes', texto: 'Reportes', permiso: PERMISOS.reportesVerOperativos },
  { ruta: '/chatbot', texto: 'Chatbot', ...ACCESO_CHATBOT },
  { ruta: '/usuarios', texto: 'Usuarios', permiso: PERMISOS.usuariosVer },
  { ruta: '/auditoria', texto: 'Auditoría', permiso: PERMISOS.auditoriaVer },
  { ruta: '/configuracion', texto: 'Configuración', permiso: PERMISOS.configuracionEditar },
]

/** Primera pantalla del menú a la que puede entrar, o null si no tiene ninguna. */
export function rutaInicial(sesion: SesionAcceso): string | null {
  if (esSoloCliente(sesion)) {
    return sesion.tienePermiso(PERMISOS.portalAcceso) ? '/portal' : null
  }
  const enlacesPersonal = enlaces.filter((e) => e.ruta !== '/portal')
  return enlacesPersonal.find((enlace) => cumpleAcceso(enlace, sesion))?.ruta ?? null
}
