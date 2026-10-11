using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CRMTeamBenavides.Api.Services.Notificaciones;

/// <summary>
/// Detecta los eventos que se notifican al guardar cualquier cambio y agrega las
/// notificaciones en la misma transacción: si la operación falla no se avisa nada,
/// y si sale bien el aviso queda en la cola sí o sí. Así no depende de que cada
/// servicio se acuerde de notificar (las solicitudes de aprobación nacen en varios).
/// </summary>
public sealed class NotificacionesInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ApplicationDbContext contexto)
        {
            await DetectorNotificaciones.AgregarAsync(contexto, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is ApplicationDbContext contexto)
        {
            DetectorNotificaciones.AgregarAsync(contexto, CancellationToken.None).GetAwaiter().GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        DescartarAvisosDelIntento(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        DescartarAvisosDelIntento(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    /// <summary>
    /// Si el guardado falla, sus avisos no cuentan: quedarían en memoria y se
    /// guardarían, o se duplicarían, si el servicio vuelve a intentar con el mismo contexto.
    /// </summary>
    private static void DescartarAvisosDelIntento(DbContext? contexto)
    {
        if (contexto is null) return;

        foreach (var entrada in contexto.ChangeTracker.Entries<Notificacion>().ToList())
        {
            if (entrada.State == EntityState.Added)
            {
                entrada.State = EntityState.Detached;
            }
            else if (entrada.State == EntityState.Modified)
            {
                // Un aviso agrupado vuelve a como estaba en la cola.
                entrada.CurrentValues.SetValues(entrada.OriginalValues);
                entrada.State = EntityState.Unchanged;
            }
        }
    }
}

/// <summary>Las reglas de qué se notifica, a quién y con qué texto.</summary>
public static class DetectorNotificaciones
{
    public const string EntidadAprobaciones = "Aprobaciones";
    public const string EntidadOrden = "OrdenServicio";
    public const string EntidadCita = "Cita";
    public const string EntidadPedidoLima = "PedidoLima";

    private sealed record Evento(TipoNotificacion Tipo, object Entidad);

    public static async Task AgregarAsync(ApplicationDbContext contexto, CancellationToken ct)
    {
        // El interceptor corre antes de que EF detecte los cambios por su cuenta.
        contexto.ChangeTracker.DetectChanges();

        var eventos = new List<Evento>();
        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            switch (entrada.Entity)
            {
                case SolicitudAprobacion solicitud
                    when entrada.State == EntityState.Added
                         && solicitud.Activo
                         && solicitud.Estado == EstadoAprobacionGerencia.Pendiente:
                    eventos.Add(new Evento(TipoNotificacion.SolicitudAprobacion, solicitud));
                    break;

                case OrdenServicio orden:
                    if (PasaA(entrada, nameof(OrdenServicio.Estado), EstadoOrdenServicio.Lista))
                        eventos.Add(new Evento(TipoNotificacion.OrdenLista, orden));
                    if (TecnicoNuevo(entrada, orden))
                        eventos.Add(new Evento(TipoNotificacion.OrdenAsignada, orden));
                    break;

                case PedidoLima pedido when PasaA(entrada, nameof(PedidoLima.Estado), EstadoPedidoLima.Recibido):
                    eventos.Add(new Evento(TipoNotificacion.PedidoLimaRecibido, pedido));
                    break;

                case Cita cita when PasaA(entrada, nameof(Cita.Estado), EstadoCita.Confirmada):
                    eventos.Add(new Evento(TipoNotificacion.CitaConfirmada, cita));
                    break;
            }
        }

        if (eventos.Count == 0) return;

        List<Guid>? aprobadores = null;
        foreach (var evento in eventos)
        {
            switch (evento.Entidad)
            {
                case SolicitudAprobacion solicitud:
                    aprobadores ??= await AprobadoresAsync(contexto, ct);
                    await NotificarSolicitudAsync(contexto, solicitud, aprobadores, ct);
                    break;

                case OrdenServicio orden when evento.Tipo == TipoNotificacion.OrdenLista:
                    await NotificarClienteAsync(contexto, orden.ClienteId, TipoNotificacion.OrdenLista,
                        "Tu unidad está lista",
                        $"{await UnidadAsync(contexto, orden.VehiculoId, ct)} · {orden.NumeroOrden}. Ya puedes recogerla en el taller.",
                        EntidadOrden, orden.Id, ct);
                    break;

                case OrdenServicio orden when evento.Tipo == TipoNotificacion.OrdenAsignada:
                    Agregar(contexto, orden.TecnicoAsignadoId!.Value, TipoNotificacion.OrdenAsignada,
                        "Orden asignada",
                        $"{orden.NumeroOrden} · {await UnidadAsync(contexto, orden.VehiculoId, ct)}",
                        EntidadOrden, orden.Id);
                    break;

                case PedidoLima pedido:
                    await NotificarClienteAsync(contexto, pedido.ClienteId, TipoNotificacion.PedidoLimaRecibido,
                        "Llegó tu pedido",
                        $"{pedido.NumeroPedido}: ya puedes recogerlo en el taller.",
                        EntidadPedidoLima, pedido.Id, ct);
                    break;

                case Cita cita:
                    await NotificarClienteAsync(contexto, cita.ClienteId, TipoNotificacion.CitaConfirmada,
                        "Cita confirmada",
                        $"{HoraPeru.Texto(cita.FechaHoraProgramada)} · {await UnidadAsync(contexto, cita.VehiculoId, ct)}",
                        EntidadCita, cita.Id, ct);
                    break;
            }
        }
    }

    /// <summary>
    /// Agrega la notificación del presupuesto enviado. La llama la orden al
    /// enviarlo; devuelve falso si el cliente todavía no tiene cuenta en la app.
    /// </summary>
    public static async Task<bool> NotificarPresupuestoAsync(ApplicationDbContext contexto, OrdenServicio orden, CancellationToken ct)
    {
        var usuarioId = await UsuarioDelClienteAsync(contexto, orden.ClienteId, ct);
        if (usuarioId is null) return false;

        Agregar(contexto, usuarioId.Value, TipoNotificacion.PresupuestoEnviado,
            "Presupuesto listo",
            $"{orden.NumeroOrden} · {await UnidadAsync(contexto, orden.VehiculoId, ct)}: revísalo y respóndelo en la app.",
            EntidadOrden, orden.Id);
        return true;
    }

    public static Task<Guid?> UsuarioDelClienteAsync(ApplicationDbContext contexto, Guid clienteId, CancellationToken ct) =>
        contexto.Clientes.AsNoTracking()
            .Where(c => c.Id == clienteId && c.Activo && c.UsuarioId != null && c.Usuario!.Activo)
            .Select(c => c.UsuarioId)
            .FirstOrDefaultAsync(ct);

    // ------------------------------------------------------------------

    /// <summary>Si la propiedad pasa al valor ahora: un registro nuevo que nace así o uno que cambia a él.</summary>
    private static bool PasaA<T>(EntityEntry entrada, string propiedad, T valor)
    {
        var dato = entrada.Property(propiedad);
        if (!Equals(dato.CurrentValue, valor)) return false;
        return entrada.State == EntityState.Added
            || (entrada.State == EntityState.Modified && dato.IsModified && !Equals(dato.OriginalValue, valor));
    }

    /// <summary>Se asigna un técnico a la orden: al abrirla con técnico o al reasignarla a otro.</summary>
    private static bool TecnicoNuevo(EntityEntry entrada, OrdenServicio orden)
    {
        if (orden.TecnicoAsignadoId is null) return false;
        if (entrada.State == EntityState.Added) return true;
        var dato = entrada.Property(nameof(OrdenServicio.TecnicoAsignadoId));
        return entrada.State == EntityState.Modified && dato.IsModified && !Equals(dato.OriginalValue, orden.TecnicoAsignadoId);
    }

    /// <summary>Quienes aprueban: Gerencia y cualquier rol con el permiso de aprobar.</summary>
    private static Task<List<Guid>> AprobadoresAsync(ApplicationDbContext contexto, CancellationToken ct) =>
        contexto.UsuarioRoles.AsNoTracking()
            .Where(ur => ur.Rol.Activo && ur.Usuario.Activo
                && (ur.Rol.Nombre == RolesDefinidos.GerenciaAdmin
                    || ur.Rol.RolPermisos.Any(rp => rp.Permiso.Activo && rp.Permiso.Codigo == PermisosDefinidos.OrdenesAprobarGerencia)))
            .Select(ur => ur.UsuarioId)
            .Distinct()
            .ToListAsync(ct);

    /// <summary>
    /// Una solicitud nueva avisa a cada aprobador, salvo a quien la pidió. Mientras
    /// el aviso de la misma orden o pedido siga en la cola, se suma a él en vez de
    /// crear otro: cinco precios cambiados en una orden son un solo aviso.
    /// </summary>
    private static async Task NotificarSolicitudAsync(
        ApplicationDbContext contexto, SolicitudAprobacion solicitud, List<Guid> aprobadores, CancellationToken ct)
    {
        var destinatarios = aprobadores.Where(id => id != solicitud.UsuarioSolicitanteId).ToList();
        if (destinatarios.Count == 0) return;

        var referencia = await ReferenciaAsync(contexto, solicitud, ct);
        var clave = $"aprobacion:{solicitud.Entidad}:{solicitud.EntidadId}";
        var titulo = solicitud.Tipo == "CambioPrecio" ? "Precio por aprobar" : "Solicitud por aprobar";
        // Algunos servicios guardan solo el id del solicitante: el nombre se busca.
        var nombre = solicitud.UsuarioSolicitanteNombre;
        if (string.IsNullOrWhiteSpace(nombre) && solicitud.UsuarioSolicitanteId is { } solicitanteId)
        {
            nombre = await contexto.Usuarios.AsNoTracking()
                .Where(u => u.Id == solicitanteId)
                .Select(u => u.NombreCompleto)
                .FirstOrDefaultAsync(ct);
        }
        var quien = string.IsNullOrWhiteSpace(nombre) ? string.Empty : $" · pedida por {nombre}";

        foreach (var usuarioId in destinatarios)
        {
            // Primero entre lo que se agrega en este mismo guardado; si no, en la cola ya guardada.
            // Uno ya reservado para enviarse (ProximoIntento) no se toca: saldría con el texto viejo.
            var enCola = contexto.Notificaciones.Local.FirstOrDefault(n => EsAgrupable(n, usuarioId, clave))
                ?? await contexto.Notificaciones.FirstOrDefaultAsync(n =>
                    n.UsuarioId == usuarioId
                    && n.ClaveAgrupacion == clave
                    && n.Activo
                    && n.EstadoEnvio == EstadoEnvioNotificacion.Pendiente
                    && n.Intentos == 0
                    && n.ProximoIntento == null
                    && n.FechaLeida == null, ct);

            if (enCola is not null)
            {
                enCola.Cantidad += 1;
                enCola.Titulo = solicitud.Tipo == "CambioPrecio" ? "Precios por aprobar" : "Solicitudes por aprobar";
                enCola.Mensaje = $"{enCola.Cantidad} solicitudes en {referencia}";
                enCola.FechaModificacion = DateTime.UtcNow;
                continue;
            }

            var nueva = Agregar(contexto, usuarioId, TipoNotificacion.SolicitudAprobacion, titulo, $"{referencia}{quien}",
                EntidadAprobaciones, solicitud.Id);
            nueva.ClaveAgrupacion = clave;
        }
    }

    private static bool EsAgrupable(Notificacion n, Guid usuarioId, string clave) =>
        n.UsuarioId == usuarioId && n.ClaveAgrupacion == clave && n.EstadoEnvio == EstadoEnvioNotificacion.Pendiente
        && n.Intentos == 0 && n.ProximoIntento == null && n.FechaLeida == null;

    private static async Task NotificarClienteAsync(
        ApplicationDbContext contexto, Guid clienteId, TipoNotificacion tipo, string titulo, string mensaje,
        string entidad, Guid entidadId, CancellationToken ct)
    {
        // Un cliente sin cuenta en la app no tiene a dónde recibirlo.
        var usuarioId = await UsuarioDelClienteAsync(contexto, clienteId, ct);
        if (usuarioId is not null)
        {
            Agregar(contexto, usuarioId.Value, tipo, titulo, mensaje, entidad, entidadId);
        }
    }

    private static Notificacion Agregar(
        ApplicationDbContext contexto, Guid usuarioId, TipoNotificacion tipo, string titulo, string mensaje,
        string entidad, Guid entidadId)
    {
        var notificacion = new Notificacion
        {
            UsuarioId = usuarioId,
            Tipo = tipo,
            Titulo = Recortar(titulo, 120),
            Mensaje = Recortar(mensaje, 300),
            Entidad = entidad,
            EntidadId = entidadId,
            EstadoEnvio = EstadoEnvioNotificacion.Pendiente,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };
        contexto.Notificaciones.Add(notificacion);
        return notificacion;
    }

    /// <summary>«OS-000123», «PL-000045» o «la venta de Juan Pérez»: sin montos, se ve en la pantalla bloqueada.</summary>
    private static async Task<string> ReferenciaAsync(ApplicationDbContext contexto, SolicitudAprobacion solicitud, CancellationToken ct)
    {
        if (!Guid.TryParse(solicitud.EntidadId, out var id)) return "una operación";

        switch (solicitud.Entidad)
        {
            case "OrdenServicio":
                var numeroOrden = contexto.OrdenesServicio.Local.FirstOrDefault(o => o.Id == id)?.NumeroOrden
                    ?? await contexto.OrdenesServicio.AsNoTracking().Where(o => o.Id == id).Select(o => o.NumeroOrden).FirstOrDefaultAsync(ct);
                return numeroOrden ?? "una orden";
            case "PedidoLima":
                var numeroPedido = contexto.PedidosLima.Local.FirstOrDefault(p => p.Id == id)?.NumeroPedido
                    ?? await contexto.PedidosLima.AsNoTracking().Where(p => p.Id == id).Select(p => p.NumeroPedido).FirstOrDefaultAsync(ct);
                return numeroPedido ?? "un pedido a Lima";
            case "Venta":
                var cliente = await contexto.Ventas.AsNoTracking().Where(v => v.Id == id).Select(v => v.Cliente.NombreCompleto).FirstOrDefaultAsync(ct);
                return cliente is null ? "una venta" : $"la venta de {cliente}";
            default:
                return "una operación";
        }
    }

    /// <summary>«Yamaha VX Cruiser». La unidad puede ser nueva y estar en este mismo guardado.</summary>
    private static async Task<string> UnidadAsync(ApplicationDbContext contexto, Guid vehiculoId, CancellationToken ct)
    {
        var local = contexto.Vehiculos.Local.FirstOrDefault(v => v.Id == vehiculoId);
        if (local is not null) return $"{local.Marca} {local.Modelo}";

        var unidad = await contexto.Vehiculos.AsNoTracking()
            .Where(v => v.Id == vehiculoId)
            .Select(v => v.Marca + " " + v.Modelo)
            .FirstOrDefaultAsync(ct);
        return unidad ?? "tu unidad";
    }

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
