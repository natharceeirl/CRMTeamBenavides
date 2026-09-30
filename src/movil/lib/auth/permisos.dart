/// Códigos de permiso del backend (PermisosDefinidos en
/// src/CRMTeamBenavides.Api/Configuration/Autorizacion/Permisos.cs).
///
/// La API es la que decide: la app solo los usa para no mostrar lo que igual
/// respondería 403.
class Permisos {
  const Permisos._();

  static const clientesVer = 'clientes.ver';
  static const unidadesVer = 'unidades.ver';
  static const ordenesVerTodas = 'ordenes.ver_todas';
  static const ordenesVerAsignadas = 'ordenes.ver_asignadas';
  static const ordenesDiagnostico = 'ordenes.diagnostico';
  static const ordenesCambiarEstado = 'ordenes.cambiar_estado';
  static const ordenesAgregarItems = 'ordenes.agregar_items';
  static const ordenesEditar = 'ordenes.editar';
  static const ventasCrear = 'ventas.crear';
  static const portalAcceso = 'portal.acceso';

  /// Quienes pueden registrar la respuesta al presupuesto (PoliticaAprobacionCliente).
  static const responderPresupuesto = [ordenesEditar, ventasCrear, portalAcceso];
  static const preciosModificar = 'precios.modificar';
  static const inventarioVer = 'inventario.ver';
  static const serviciosVer = 'servicios.ver';
  static const inventarioEditar = 'inventario.editar';
  static const ventasVer = 'ventas.ver';
  static const cajaConsultar = 'caja.consultar';
  static const reportesVerOperativos = 'reportes.ver_operativos';
  static const reportesVerFinancieros = 'reportes.ver_financieros';

  static const verOrdenes = [ordenesVerTodas, ordenesVerAsignadas];
}
