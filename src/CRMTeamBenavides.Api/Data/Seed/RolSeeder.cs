using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Data.Seed;

public static class RolSeeder
{
    public const string RolGerenciaAdmin = RolesDefinidos.GerenciaAdmin;
    public const string RolRecepcion = RolesDefinidos.Recepcion;
    public const string RolTecnico = RolesDefinidos.Tecnico;
    public const string RolVendedor = RolesDefinidos.Vendedor;
    public const string RolCliente = RolesDefinidos.Cliente;

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // -------------------------------------------------------------------
        // 1. Catálogo de Permisos (Semilla idempotente)
        // -------------------------------------------------------------------
        var permisosExistentes = await context.Permisos.ToDictionaryAsync(p => p.Codigo);

        var descripcionesPermisos = new Dictionary<string, string>
        {
            [PermisosDefinidos.UsuariosVer] = "Ver listado y detalle de usuarios",
            [PermisosDefinidos.UsuariosCrear] = "Crear nuevos usuarios en el sistema",
            [PermisosDefinidos.UsuariosEditar] = "Modificar datos y roles de usuarios",
            [PermisosDefinidos.UsuariosEliminar] = "Desactivar o dar de baja usuarios",
            [PermisosDefinidos.UsuariosResetPassword] = "Restablecer contraseñas de otros usuarios",
            [PermisosDefinidos.RolesVer] = "Consultar roles del sistema",
            [PermisosDefinidos.RolesGestionar] = "Administrar roles y asignación de permisos",
            [PermisosDefinidos.ClientesVer] = "Ver clientes registrados",
            [PermisosDefinidos.ClientesCrear] = "Registrar nuevos clientes",
            [PermisosDefinidos.ClientesEditar] = "Modificar información de clientes",
            [PermisosDefinidos.ClientesEliminar] = "Dar de baja clientes",
            [PermisosDefinidos.UnidadesVer] = "Consultar unidades y vehículos",
            [PermisosDefinidos.UnidadesCrear] = "Registrar nuevas unidades",
            [PermisosDefinidos.UnidadesEditar] = "Modificar datos de unidades",
            [PermisosDefinidos.UnidadesEliminar] = "Dar de baja unidades",
            [PermisosDefinidos.OrdenesVerTodas] = "Ver todas las órdenes de servicio del taller",
            [PermisosDefinidos.OrdenesVerAsignadas] = "Ver únicamente las órdenes asignadas al técnico",
            [PermisosDefinidos.OrdenesCrear] = "Aperturar nuevas órdenes de servicio",
            [PermisosDefinidos.OrdenesEditar] = "Modificar órdenes de servicio",
            [PermisosDefinidos.OrdenesDiagnostico] = "Registrar y actualizar diagnóstico técnico",
            [PermisosDefinidos.OrdenesCambiarEstado] = "Efectuar transiciones de estado en la orden",
            [PermisosDefinidos.OrdenesAsignarTecnico] = "Asignar o reasignar técnicos a una orden",
            [PermisosDefinidos.OrdenesAprobarGerencia] = "Aprobación formal de gerencia en órdenes",
            [PermisosDefinidos.OrdenesAgregarItems] = "Agregar repuestos o servicios a una orden",
            [PermisosDefinidos.PreciosModificar] = "Fijar o modificar precios unitarios de servicios y repuestos",
            [PermisosDefinidos.InventarioVer] = "Consultar productos, repuestos y stock",
            [PermisosDefinidos.InventarioCrear] = "Registrar nuevos repuestos en catálogo",
            [PermisosDefinidos.InventarioEditar] = "Modificar repuestos y umbrales de stock",
            [PermisosDefinidos.InventarioAjustar] = "Registrar movimientos y ajustes de inventario",
            [PermisosDefinidos.InventarioEliminar] = "Dar de baja repuestos del catálogo",
            [PermisosDefinidos.ServiciosVer] = "Consultar catálogo de servicios del taller",
            [PermisosDefinidos.ServiciosCrear] = "Registrar nuevos servicios en el catálogo",
            [PermisosDefinidos.ServiciosEditar] = "Modificar servicios y precios sugeridos",
            [PermisosDefinidos.ServiciosEliminar] = "Dar de baja servicios del catálogo",
            [PermisosDefinidos.VentasVer] = "Consultar ventas y cotizaciones",
            [PermisosDefinidos.VentasCrear] = "Registrar cotizaciones y ventas directas",
            [PermisosDefinidos.VentasAnular] = "Anular ventas emitidas",
            [PermisosDefinidos.DescuentosAplicar] = "Aplicar descuentos comerciales sobre ventas",
            [PermisosDefinidos.CajaConsultar] = "Consultar saldo y movimientos de caja chica",
            [PermisosDefinidos.CajaRegistrarIngreso] = "Registrar ingresos a la caja chica",
            [PermisosDefinidos.CajaRegistrarEgreso] = "Registrar egresos y gastos en caja chica",
            [PermisosDefinidos.CajaAperturar] = "Aperturar turno de caja chica",
            [PermisosDefinidos.CajaCerrar] = "Cerrar turno de caja chica",
            [PermisosDefinidos.ReportesVerOperativos] = "Visualizar reportes operativos y tablero general",
            [PermisosDefinidos.ReportesVerFinancieros] = "Visualizar reportes de rentabilidad, ganancias y finanzas",
            [PermisosDefinidos.ConfiguracionEditar] = "Modificar parámetros generales, empresa e IGV",
            [PermisosDefinidos.AuditoriaVer] = "Consultar bitácora de auditoría del sistema",
            [PermisosDefinidos.PortalAcceso] = "Acceso a la plataforma y app de clientes"
        };

        var nuevosPermisos = new List<Permiso>();
        foreach (var codigo in PermisosDefinidos.Todos)
        {
            if (!permisosExistentes.TryGetValue(codigo, out var permiso))
            {
                var nuevo = new Permiso
                {
                    Codigo = codigo,
                    Descripcion = descripcionesPermisos.GetValueOrDefault(codigo, codigo),
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };
                nuevosPermisos.Add(nuevo);
                permisosExistentes[codigo] = nuevo;
            }
            else if (!permiso.Activo)
            {
                permiso.Activo = true;
                permiso.FechaModificacion = DateTime.UtcNow;
            }
        }

        if (nuevosPermisos.Count > 0)
        {
            context.Permisos.AddRange(nuevosPermisos);
            await context.SaveChangesAsync();
        }

        // -------------------------------------------------------------------
        // 2. Roles Definitivos (Migración canónica de nombres)
        // -------------------------------------------------------------------
        // Migrar roles antiguos a nombres canónicos si existen. Solo mientras el
        // canónico no exista: si quedaban «Admin» y «Administrador», el segundo
        // arranque intentaba renombrar el otro y chocaba con el índice único.
        var existeGerenciaAdmin = await context.Roles.AnyAsync(r => r.Nombre == RolGerenciaAdmin);
        var rolAdminAntiguo = existeGerenciaAdmin
            ? null
            : await context.Roles
                .Where(r => r.Nombre == "Admin" || r.Nombre == "Administrador")
                .OrderByDescending(r => r.Activo)
                .FirstOrDefaultAsync();

        if (rolAdminAntiguo is not null)
        {
            rolAdminAntiguo.Nombre = RolGerenciaAdmin;
            rolAdminAntiguo.Descripcion = "Gerencia y Administrador del sistema con acceso total";
            rolAdminAntiguo.Activo = true;
            rolAdminAntiguo.FechaModificacion = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        var rolesInfo = new (string Nombre, string Descripcion)[]
        {
            (RolGerenciaAdmin, "Gerencia y Administrador del sistema con acceso total"),
            (RolRecepcion, "Personal de recepción y atención operativa del taller"),
            (RolTecnico, "Técnico de taller asignado a órdenes de servicio"),
            (RolVendedor, "Personal comercial y ventas de repuestos en mostrador"),
            (RolCliente, "Cliente propietario de unidades atendidas en el taller")
        };

        var rolesEnBd = await context.Roles.ToDictionaryAsync(r => r.Nombre);

        foreach (var (nombre, descripcion) in rolesInfo)
        {
            if (!rolesEnBd.TryGetValue(nombre, out var rol))
            {
                var nuevoRol = new Rol
                {
                    Nombre = nombre,
                    Descripcion = descripcion,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };
                context.Roles.Add(nuevoRol);
                rolesEnBd[nombre] = nuevoRol;
            }
            else if (!rol.Activo)
            {
                rol.Activo = true;
                rol.FechaModificacion = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();

        // -------------------------------------------------------------------
        // 3. Matriz Rol-Permiso (Semilla de políticas de seguridad)
        // -------------------------------------------------------------------
        var matrizPermisos = new Dictionary<string, HashSet<string>>
        {
            // Gerencia/Admin: TIENE TODOS LOS PERMISOS
            [RolGerenciaAdmin] = new HashSet<string>(PermisosDefinidos.Todos),

            // Recepción: Operativa de taller, clientes, unidades, ventas, caja y reporte operativo
            [RolRecepcion] = new HashSet<string>
            {
                PermisosDefinidos.UsuariosVer, PermisosDefinidos.RolesVer,
                PermisosDefinidos.ClientesVer, PermisosDefinidos.ClientesCrear, PermisosDefinidos.ClientesEditar,
                PermisosDefinidos.UnidadesVer, PermisosDefinidos.UnidadesCrear, PermisosDefinidos.UnidadesEditar,
                PermisosDefinidos.OrdenesVerTodas, PermisosDefinidos.OrdenesCrear, PermisosDefinidos.OrdenesEditar,
                PermisosDefinidos.OrdenesDiagnostico, PermisosDefinidos.OrdenesCambiarEstado,
                PermisosDefinidos.OrdenesAsignarTecnico, PermisosDefinidos.OrdenesAgregarItems,
                PermisosDefinidos.PreciosModificar,
                PermisosDefinidos.InventarioVer,
                PermisosDefinidos.ServiciosVer, PermisosDefinidos.ServiciosCrear, PermisosDefinidos.ServiciosEditar,
                PermisosDefinidos.VentasVer, PermisosDefinidos.VentasCrear,
                PermisosDefinidos.CajaConsultar, PermisosDefinidos.CajaRegistrarIngreso, PermisosDefinidos.CajaRegistrarEgreso,
                PermisosDefinidos.CajaAperturar, PermisosDefinidos.CajaCerrar,
                PermisosDefinidos.ReportesVerOperativos
            },

            // Técnico: Sólo sus OS asignadas, diagnóstico, agregar repuestos/mano de obra, y transiciones operativas.
            // NO modifica precios, NO aprueba finalmente, NO ve otras órdenes ni finanzas.
            [RolTecnico] = new HashSet<string>
            {
                PermisosDefinidos.OrdenesVerAsignadas,
                PermisosDefinidos.OrdenesDiagnostico,
                PermisosDefinidos.OrdenesAgregarItems,
                PermisosDefinidos.OrdenesCambiarEstado,
                PermisosDefinidos.UnidadesVer,
                PermisosDefinidos.InventarioVer,
                PermisosDefinidos.ServiciosVer
            },

            // Vendedor: Gestiona ventas y mostrador.
            // NO modifica precios de catálogo, NO aplica descuentos libres, NO anula ventas.
            [RolVendedor] = new HashSet<string>
            {
                PermisosDefinidos.VentasVer, PermisosDefinidos.VentasCrear,
                PermisosDefinidos.ClientesVer, PermisosDefinidos.ClientesCrear,
                PermisosDefinidos.InventarioVer,
                PermisosDefinidos.ServiciosVer,
                PermisosDefinidos.CajaConsultar, PermisosDefinidos.CajaRegistrarIngreso,
                PermisosDefinidos.ReportesVerOperativos
            },

            // Cliente: Sólo accede a sus propios datos a través del portal
            [RolCliente] = new HashSet<string>
            {
                PermisosDefinidos.PortalAcceso,
                PermisosDefinidos.ClientesVer,
                PermisosDefinidos.UnidadesVer,
                PermisosDefinidos.OrdenesVerAsignadas
            }
        };

        var rolPermisosExistentes = await context.RolPermisos
            .Select(rp => new { rp.RolId, rp.PermisoId })
            .ToListAsync();

        var setExistente = new HashSet<(Guid RolId, Guid PermisoId)>(
            rolPermisosExistentes.Select(rp => (rp.RolId, rp.PermisoId)));

        var nuevosRolPermisos = new List<RolPermiso>();

        foreach (var (nombreRol, codigosPermisos) in matrizPermisos)
        {
            if (!rolesEnBd.TryGetValue(nombreRol, out var rol))
            {
                continue;
            }

            foreach (var codigo in codigosPermisos)
            {
                if (!permisosExistentes.TryGetValue(codigo, out var permiso))
                {
                    continue;
                }

                if (!setExistente.Contains((rol.Id, permiso.Id)))
                {
                    nuevosRolPermisos.Add(new RolPermiso
                    {
                        RolId = rol.Id,
                        PermisoId = permiso.Id
                    });
                    setExistente.Add((rol.Id, permiso.Id));
                }
            }
        }

        if (nuevosRolPermisos.Count > 0)
        {
            context.RolPermisos.AddRange(nuevosRolPermisos);
            await context.SaveChangesAsync();
        }
    }
}
