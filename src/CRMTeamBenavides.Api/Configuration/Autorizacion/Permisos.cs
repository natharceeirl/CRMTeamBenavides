namespace CRMTeamBenavides.Api.Configuration.Autorizacion;

public static class PermisosDefinidos
{
    // Usuarios y Roles
    public const string UsuariosVer = "usuarios.ver";
    public const string UsuariosCrear = "usuarios.crear";
    public const string UsuariosEditar = "usuarios.editar";
    public const string UsuariosEliminar = "usuarios.eliminar";
    public const string UsuariosResetPassword = "usuarios.reset_password";
    public const string RolesVer = "roles.ver";
    public const string RolesGestionar = "roles.gestionar";

    // Clientes (CRM)
    public const string ClientesVer = "clientes.ver";
    public const string ClientesCrear = "clientes.crear";
    public const string ClientesEditar = "clientes.editar";
    public const string ClientesEliminar = "clientes.eliminar";

    // Unidades (Vehículos)
    public const string UnidadesVer = "unidades.ver";
    public const string UnidadesCrear = "unidades.crear";
    public const string UnidadesEditar = "unidades.editar";
    public const string UnidadesEliminar = "unidades.eliminar";

    // Órdenes de Servicio
    public const string OrdenesVerTodas = "ordenes.ver_todas";
    public const string OrdenesVerAsignadas = "ordenes.ver_asignadas";
    public const string OrdenesCrear = "ordenes.crear";
    public const string OrdenesEditar = "ordenes.editar";
    public const string OrdenesDiagnostico = "ordenes.diagnostico";
    public const string OrdenesCambiarEstado = "ordenes.cambiar_estado";
    public const string OrdenesAsignarTecnico = "ordenes.asignar_tecnico";
    public const string OrdenesAprobarGerencia = "ordenes.aprobar_gerencia";
    public const string OrdenesAgregarItems = "ordenes.agregar_items";
    public const string PreciosModificar = "precios.modificar";

    // Inventario y Repuestos
    public const string InventarioVer = "inventario.ver";
    public const string InventarioCrear = "inventario.crear";
    public const string InventarioEditar = "inventario.editar";
    public const string InventarioAjustar = "inventario.ajustar";
    public const string InventarioEliminar = "inventario.eliminar";

    // Ventas y Comprobantes
    public const string VentasVer = "ventas.ver";
    public const string VentasCrear = "ventas.crear";
    public const string VentasAnular = "ventas.anular";
    public const string DescuentosAplicar = "descuentos.aplicar";

    // Caja
    public const string CajaConsultar = "caja.consultar";
    public const string CajaRegistrarIngreso = "caja.registrar_ingreso";
    public const string CajaRegistrarEgreso = "caja.registrar_egreso";

    // Reportes y Dashboard
    public const string ReportesVerOperativos = "reportes.ver_operativos";
    public const string ReportesVerFinancieros = "reportes.ver_financieros";

    // Configuración y Auditoría
    public const string ConfiguracionEditar = "configuracion.editar";
    public const string AuditoriaVer = "auditoria.ver";

    // Portal Cliente
    public const string PortalAcceso = "portal.acceso";

    public static readonly IReadOnlyList<string> Todos = new[]
    {
        UsuariosVer, UsuariosCrear, UsuariosEditar, UsuariosEliminar, UsuariosResetPassword,
        RolesVer, RolesGestionar,
        ClientesVer, ClientesCrear, ClientesEditar, ClientesEliminar,
        UnidadesVer, UnidadesCrear, UnidadesEditar, UnidadesEliminar,
        OrdenesVerTodas, OrdenesVerAsignadas, OrdenesCrear, OrdenesEditar, OrdenesDiagnostico,
        OrdenesCambiarEstado, OrdenesAsignarTecnico, OrdenesAprobarGerencia, OrdenesAgregarItems,
        PreciosModificar,
        InventarioVer, InventarioCrear, InventarioEditar, InventarioAjustar, InventarioEliminar,
        VentasVer, VentasCrear, VentasAnular, DescuentosAplicar,
        CajaConsultar, CajaRegistrarIngreso, CajaRegistrarEgreso,
        ReportesVerOperativos, ReportesVerFinancieros,
        ConfiguracionEditar, AuditoriaVer,
        PortalAcceso
    };
}

public static class RolesDefinidos
{
    public const string GerenciaAdmin = "Gerencia/Admin";
    public const string Recepcion = "Recepcion";
    public const string Tecnico = "Tecnico";
    public const string Vendedor = "Vendedor";
    public const string Cliente = "Cliente";

    public static readonly IReadOnlyList<string> Todos = new[]
    {
        GerenciaAdmin, Recepcion, Tecnico, Vendedor, Cliente
    };
}
