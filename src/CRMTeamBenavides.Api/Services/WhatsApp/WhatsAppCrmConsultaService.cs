using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

/// <summary>
/// Separa de forma estricta las consultas administrativas internas (personal autorizado del CRM)
/// de las consultas conversacionales destinadas a responder al remitente de WhatsApp (bot / futura IA).
/// </summary>
public enum WhatsAppModoConsultaCrm
{
    /// <summary>
    /// Canal conversacional / futura IA (Fase 3).
    /// SIEMPRE exige verificación explícita de identidad mediante <see cref="IWhatsAppVerificadorIdentidad"/>;
    /// la coincidencia por teléfono ni <c>ConversacionWhatsApp.ClienteId</c> bastan para acreditar titularidad.
    /// Mientras no exista un mecanismo real aprobado, toda consulta personal en este modo queda denegada por defecto.
    /// </summary>
    ConversacionalCliente = 0,

    /// <summary>
    /// Uso interno desde endpoints administrativos autenticados y autorizados con los permisos del módulo CRM correspondiente.
    /// Jamás debe reutilizarse desde un flujo automatizado o de IA como si fuera una consulta autenticada del cliente.
    /// </summary>
    AdministrativoInterno = 1
}

/// <summary>
/// Estado explícito de verificación de titularidad del remitente de una conversación WhatsApp.
/// IMPORTANTE: la coincidencia telefónica tiene confianza baja/indiciaria y NUNCA equivale a identidad verificada.
/// </summary>
public enum EstadoVerificacionIdentidadWhatsApp
{
    NoVerificada = 0,
    VerificadaPorMecanismoAprobado = 1
}

/// <summary>
/// Contexto obligatorio para ejecutar consultas personales del CRM desde el canal WhatsApp.
/// Por defecto falla cerrado (<see cref="ConversacionalSinVerificar"/>).
/// </summary>
public sealed record WhatsAppContextoConsultaCrm(
    WhatsAppModoConsultaCrm Modo,
    bool PermitirDocumentoCompleto = false,
    EstadoVerificacionIdentidadWhatsApp EstadoVerificacionIdentidad = EstadoVerificacionIdentidadWhatsApp.NoVerificada)
{
    public static WhatsAppContextoConsultaCrm ConversacionalSinVerificar() =>
        new(
            WhatsAppModoConsultaCrm.ConversacionalCliente,
            PermitirDocumentoCompleto: false,
            EstadoVerificacionIdentidadWhatsApp.NoVerificada);

    public static WhatsAppContextoConsultaCrm Administrativo(bool permitirDocumentoCompleto = false) =>
        new(
            WhatsAppModoConsultaCrm.AdministrativoInterno,
            PermitirDocumentoCompleto: permitirDocumentoCompleto,
            EstadoVerificacionIdentidadWhatsApp.NoVerificada);
}

/// <summary>
/// Frontera técnica obligatoria para verificar si el remitente de una conversación WhatsApp
/// superó un mecanismo real y aprobado de verificación de identidad.
/// En Fase 2 no existe verificación implementada: la implementación por defecto devuelve siempre <c>false</c>.
/// </summary>
public interface IWhatsAppVerificadorIdentidad
{
    Task<bool> EstaIdentidadVerificadaAsync(Guid conversacionId, CancellationToken ct = default);
}

/// <summary>
/// Implementación segura por defecto para Fase 2: deniega siempre la verificación conversacional
/// hasta que Fase 3 incorpore un mecanismo real y aprobado de verificación de titularidad.
/// No implementa OTP ficticios ni marca conversaciones como verificadas por coincidencia telefónica.
/// </summary>
public sealed class WhatsAppVerificadorIdentidadNoImplementado : IWhatsAppVerificadorIdentidad
{
    public static WhatsAppVerificadorIdentidadNoImplementado Instancia { get; } = new();

    public Task<bool> EstaIdentidadVerificadaAsync(Guid conversacionId, CancellationToken ct = default) =>
        Task.FromResult(false);
}

/// <summary>
/// Capa controlada de consultas de solo lectura del CRM para el canal WhatsApp y futura integración de IA.
/// Reglas de seguridad obligatorias:
/// - Todas las operaciones asociadas a un cliente reciben <c>conversacionId</c> y resuelven <c>ClienteId</c> en servidor.
/// - El modo por defecto es <see cref="WhatsAppModoConsultaCrm.ConversacionalCliente"/> sin verificar (denegado por defecto).
/// - El modo <see cref="WhatsAppModoConsultaCrm.AdministrativoInterno"/> es exclusivo para personal autorizado del CRM.
/// - Jamás expone SQL libre ni <c>Producto.Costo</c>.
/// </summary>
public interface IWhatsAppCrmConsultaService
{
    Task<ServiceResult<WhatsAppPerfilClienteResponse>> ConsultarPerfilClienteAsync(
        Guid conversacionId,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default);

    Task<ServiceResult<List<WhatsAppVehiculoClienteResponse>>> ConsultarVehiculosClienteAsync(
        Guid conversacionId,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default);

    Task<ServiceResult<List<WhatsAppOrdenServicioResumenResponse>>> ConsultarOrdenesServicioClienteAsync(
        Guid conversacionId,
        bool incluirFinalizadasYCanceladas = true,
        int limite = 10,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppOrdenServicioDetalleResponse>> ConsultarEstadoOrdenServicioAsync(
        Guid conversacionId,
        Guid? ordenServicioId = null,
        string? numeroOrden = null,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default);

    Task<ServiceResult<List<WhatsAppCitaClienteResponse>>> ConsultarCitasClienteAsync(
        Guid conversacionId,
        bool soloActivas = false,
        int limite = 10,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default);

    Task<WhatsAppCatalogoConsultaResponse> ConsultarProductosYServiciosAsync(
        string? busqueda = null,
        bool incluirProductos = true,
        bool incluirServicios = true,
        int limite = 15,
        WhatsAppModoConsultaCrm modo = WhatsAppModoConsultaCrm.ConversacionalCliente,
        CancellationToken ct = default);
}

public sealed class WhatsAppCrmConsultaService : IWhatsAppCrmConsultaService
{
    private readonly ApplicationDbContext _context;
    private readonly IOptionsMonitor<WhatsAppSettings> _settingsMonitor;
    private readonly IWhatsAppVerificadorIdentidad _verificadorIdentidad;
    private readonly ILogger<WhatsAppCrmConsultaService> _logger;

    public WhatsAppCrmConsultaService(
        ApplicationDbContext context,
        IOptionsMonitor<WhatsAppSettings> settingsMonitor,
        IWhatsAppVerificadorIdentidad verificadorIdentidad,
        ILogger<WhatsAppCrmConsultaService> logger)
    {
        _context = context;
        _settingsMonitor = settingsMonitor;
        _verificadorIdentidad = verificadorIdentidad;
        _logger = logger;
    }

    public async Task<ServiceResult<WhatsAppPerfilClienteResponse>> ConsultarPerfilClienteAsync(
        Guid conversacionId,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default)
    {
        var ctx = contexto ?? WhatsAppContextoConsultaCrm.ConversacionalSinVerificar();
        var resolucion = await ResolverClienteVinculadoAsync(conversacionId, ctx, ct);
        if (!resolucion.IsSuccess)
        {
            return MapError<WhatsAppPerfilClienteResponse>(resolucion);
        }

        var (conversacion, cliente) = resolucion.Data;
        var codigoPais = _settingsMonitor.CurrentValue.DefaultCountryCode;

        var cantidadVehiculos = await _context.Vehiculos
            .AsNoTracking()
            .CountAsync(v => v.ClienteId == cliente.Id && v.Activo, ct);

        var cantidadOrdenesActivas = await _context.OrdenesServicio
            .AsNoTracking()
            .CountAsync(o =>
                o.ClienteId == cliente.Id
                && o.Activo
                && o.Estado != EstadoOrdenServicio.Entregada
                && o.Estado != EstadoOrdenServicio.Cancelada, ct);

        var cantidadCitasActivas = await _context.Citas
            .AsNoTracking()
            .CountAsync(c =>
                c.ClienteId == cliente.Id
                && c.Activo
                && c.Estado != EstadoCita.Completada
                && c.Estado != EstadoCita.Cancelada
                && c.Estado != EstadoCita.NoAsistio, ct);

        var docCrudo = !string.IsNullOrWhiteSpace(cliente.NumeroDocumento)
            ? cliente.NumeroDocumento.Trim()
            : cliente.DocumentoIdentidad?.Trim();

        var mostrarDocumentoCompleto = ctx.Modo == WhatsAppModoConsultaCrm.AdministrativoInterno
            && ctx.PermitirDocumentoCompleto;

        var documentoVisible = mostrarDocumentoCompleto
            ? docCrudo
            : WhatsAppPhoneNormalizer.EnmascararDocumento(docCrudo);

        // En alcance conversacional jamás se exponen teléfonos secundarios del cliente.
        var telefonos = ctx.Modo == WhatsAppModoConsultaCrm.AdministrativoInterno
            ? WhatsAppPhoneNormalizer.ExtraerTelefonosNormalizadosE164(cliente.Telefono, codigoPais).ToList()
            : new List<string> { conversacion.TelefonoE164 };

        _logger.LogInformation(
            "WhatsApp CRM consulta perfil. ConversacionId={ConversacionId}, ClienteId={ClienteId}, Modo={Modo}",
            conversacionId,
            cliente.Id,
            ctx.Modo);

        return ServiceResult<WhatsAppPerfilClienteResponse>.Success(new WhatsAppPerfilClienteResponse(
            ClienteId: cliente.Id,
            NombreCompleto: cliente.NombreCompleto,
            RazonSocial: cliente.RazonSocial,
            TipoDocumento: cliente.TipoDocumento,
            DocumentoVisible: documentoVisible,
            TelefonosE164: telefonos,
            TieneEmail: !string.IsNullOrWhiteSpace(cliente.Email),
            CantidadVehiculosActivos: cantidadVehiculos,
            CantidadOrdenesActivas: cantidadOrdenesActivas,
            CantidadCitasActivas: cantidadCitasActivas));
    }

    public async Task<ServiceResult<List<WhatsAppVehiculoClienteResponse>>> ConsultarVehiculosClienteAsync(
        Guid conversacionId,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default)
    {
        var ctx = contexto ?? WhatsAppContextoConsultaCrm.ConversacionalSinVerificar();
        var resolucion = await ResolverClienteVinculadoAsync(conversacionId, ctx, ct);
        if (!resolucion.IsSuccess)
        {
            return MapError<List<WhatsAppVehiculoClienteResponse>>(resolucion);
        }

        var clienteId = resolucion.Data.Cliente.Id;

        var vehiculos = await _context.Vehiculos
            .AsNoTracking()
            .Where(v => v.ClienteId == clienteId && v.Activo)
            .OrderByDescending(v => v.FechaCreacion)
            .ToListAsync(ct);

        var items = vehiculos.Select(v => new WhatsAppVehiculoClienteResponse(
            Id: v.Id,
            TipoUnidad: v.TipoUnidad,
            Marca: v.Marca,
            Modelo: v.Modelo,
            Anio: v.Anio,
            Placa: v.Placa,
            Color: v.Color,
            TipoMedidor: v.TipoMedidor,
            LecturaMedidorActual: v.LecturaMedidorActual
                ?? (v.TipoMedidor == TipoMedidor.Horas ? v.HorasUso : v.Kilometraje),
            SerieIdentificacionParcial: WhatsAppPhoneNormalizer.EnmascararSerie(v.NumeroSerieVIN)))
            .ToList();

        _logger.LogInformation(
            "WhatsApp CRM consulta vehículos. ConversacionId={ConversacionId}, ClienteId={ClienteId}, Modo={Modo}, Total={Total}",
            conversacionId,
            clienteId,
            ctx.Modo,
            items.Count);

        return ServiceResult<List<WhatsAppVehiculoClienteResponse>>.Success(items);
    }

    public async Task<ServiceResult<List<WhatsAppOrdenServicioResumenResponse>>> ConsultarOrdenesServicioClienteAsync(
        Guid conversacionId,
        bool incluirFinalizadasYCanceladas = true,
        int limite = 10,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default)
    {
        var ctx = contexto ?? WhatsAppContextoConsultaCrm.ConversacionalSinVerificar();
        var resolucion = await ResolverClienteVinculadoAsync(conversacionId, ctx, ct);
        if (!resolucion.IsSuccess)
        {
            return MapError<List<WhatsAppOrdenServicioResumenResponse>>(resolucion);
        }

        var clienteId = resolucion.Data.Cliente.Id;
        var tope = Math.Clamp(limite, 1, 25);

        var query = _context.OrdenesServicio
            .AsNoTracking()
            .Include(o => o.Vehiculo)
            .Include(o => o.Detalles.Where(d => d.Activo))
            .Where(o => o.ClienteId == clienteId && o.Activo);

        if (!incluirFinalizadasYCanceladas)
        {
            query = query.Where(o =>
                o.Estado != EstadoOrdenServicio.Entregada
                && o.Estado != EstadoOrdenServicio.Cancelada);
        }

        var ordenes = await query
            .OrderByDescending(o => o.FechaIngreso)
            .ThenByDescending(o => o.FechaCreacion)
            .Take(tope)
            .ToListAsync(ct);

        var items = ordenes.Select(o =>
        {
            var tipos = ObtenerTiposAtencion(o);
            return new WhatsAppOrdenServicioResumenResponse(
                Id: o.Id,
                NumeroOrden: o.NumeroOrden,
                FechaIngreso: o.FechaIngreso,
                FechaEstimadaEntrega: o.FechaEstimadaEntrega,
                FechaSalida: o.FechaSalida,
                Estado: o.Estado,
                EstadoEntrega: TraducirEstadoEntrega(o.Estado),
                VehiculoId: o.VehiculoId,
                VehiculoResumen: FormatearVehiculoResumen(o.Vehiculo),
                TipoAtencionPrincipal: o.TipoAtencion,
                TiposAtencion: tipos,
                ResumenTrabajos: ConstruirResumenTrabajos(o));
        }).ToList();

        _logger.LogInformation(
            "WhatsApp CRM consulta órdenes. ConversacionId={ConversacionId}, ClienteId={ClienteId}, Modo={Modo}, Total={Total}",
            conversacionId,
            clienteId,
            ctx.Modo,
            items.Count);

        return ServiceResult<List<WhatsAppOrdenServicioResumenResponse>>.Success(items);
    }

    public async Task<ServiceResult<WhatsAppOrdenServicioDetalleResponse>> ConsultarEstadoOrdenServicioAsync(
        Guid conversacionId,
        Guid? ordenServicioId = null,
        string? numeroOrden = null,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default)
    {
        if (!ordenServicioId.HasValue && string.IsNullOrWhiteSpace(numeroOrden))
        {
            return ServiceResult<WhatsAppOrdenServicioDetalleResponse>.Invalid(
                "Debe indicar el identificador o número de orden de servicio a consultar.");
        }

        var ctx = contexto ?? WhatsAppContextoConsultaCrm.ConversacionalSinVerificar();
        var resolucion = await ResolverClienteVinculadoAsync(conversacionId, ctx, ct);
        if (!resolucion.IsSuccess)
        {
            return MapError<WhatsAppOrdenServicioDetalleResponse>(resolucion);
        }

        var clienteId = resolucion.Data.Cliente.Id;
        var numNorm = string.IsNullOrWhiteSpace(numeroOrden) ? null : numeroOrden.Trim().ToUpperInvariant();

        var query = _context.OrdenesServicio
            .AsNoTracking()
            .Include(o => o.Vehiculo)
            .Include(o => o.Detalles.Where(d => d.Activo))
            .Where(o => o.ClienteId == clienteId && o.Activo);

        OrdenServicio? orden;
        if (ordenServicioId.HasValue)
        {
            orden = await query.FirstOrDefaultAsync(o => o.Id == ordenServicioId.Value, ct);
            if (orden is null)
            {
                // Nunca revelar si la orden existe para otro cliente: devolver NotFound genérico.
                return ServiceResult<WhatsAppOrdenServicioDetalleResponse>.NotFound(
                    "Orden de servicio no encontrada para el cliente de esta conversación.");
            }

            // Si se enviaron ambos parámetros simultáneamente, validar consistencia estricta en lugar de ignorar numeroOrden.
            if (numNorm is not null && !string.Equals(orden.NumeroOrden?.Trim(), numNorm, StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<WhatsAppOrdenServicioDetalleResponse>.Invalid(
                    "El identificador y el número de orden de servicio no corresponden a la misma orden.");
            }
        }
        else
        {
            orden = await query.FirstOrDefaultAsync(
                o => o.NumeroOrden != null && o.NumeroOrden.ToUpper() == numNorm!,
                ct);

            if (orden is null)
            {
                return ServiceResult<WhatsAppOrdenServicioDetalleResponse>.NotFound(
                    "Orden de servicio no encontrada para el cliente de esta conversación.");
            }
        }

        var trabajos = orden.Detalles
            .Where(d => d.Activo)
            .OrderBy(d => d.FechaCreacion)
            .Select(d => new WhatsAppTrabajoOrdenResponse(d.TipoItem, d.Descripcion, d.Cantidad))
            .ToList();

        _logger.LogInformation(
            "WhatsApp CRM consulta detalle orden. ConversacionId={ConversacionId}, ClienteId={ClienteId}, OrdenId={OrdenId}, Modo={Modo}",
            conversacionId,
            clienteId,
            orden.Id,
            ctx.Modo);

        return ServiceResult<WhatsAppOrdenServicioDetalleResponse>.Success(new WhatsAppOrdenServicioDetalleResponse(
            Id: orden.Id,
            NumeroOrden: orden.NumeroOrden,
            FechaApertura: orden.FechaApertura,
            FechaIngreso: orden.FechaIngreso,
            FechaEstimadaEntrega: orden.FechaEstimadaEntrega,
            FechaSalida: orden.FechaSalida,
            FechaCierre: orden.FechaCierre,
            Estado: orden.Estado,
            EstadoEntrega: TraducirEstadoEntrega(orden.Estado),
            EstadoPresupuestoCliente: orden.EstadoPresupuestoCliente,
            VehiculoId: orden.VehiculoId,
            VehiculoResumen: FormatearVehiculoResumen(orden.Vehiculo),
            VehiculoPlaca: orden.Vehiculo?.Placa,
            TipoAtencionPrincipal: orden.TipoAtencion,
            TiposAtencion: ObtenerTiposAtencion(orden),
            ModalidadAtencion: orden.ModalidadAtencion,
            MotivoFalla: orden.MotivoFalla,
            DiagnosticoResumen: orden.Diagnostico,
            SolucionResumen: orden.Solucion,
            Trabajos: trabajos,
            Total: orden.Total));
    }

    public async Task<ServiceResult<List<WhatsAppCitaClienteResponse>>> ConsultarCitasClienteAsync(
        Guid conversacionId,
        bool soloActivas = false,
        int limite = 10,
        WhatsAppContextoConsultaCrm? contexto = null,
        CancellationToken ct = default)
    {
        var ctx = contexto ?? WhatsAppContextoConsultaCrm.ConversacionalSinVerificar();
        var resolucion = await ResolverClienteVinculadoAsync(conversacionId, ctx, ct);
        if (!resolucion.IsSuccess)
        {
            return MapError<List<WhatsAppCitaClienteResponse>>(resolucion);
        }

        var clienteId = resolucion.Data.Cliente.Id;
        var tope = Math.Clamp(limite, 1, 25);

        var query = _context.Citas
            .AsNoTracking()
            .Include(c => c.Vehiculo)
            .Where(c => c.ClienteId == clienteId && c.Activo);

        if (soloActivas)
        {
            query = query.Where(c =>
                c.Estado != EstadoCita.Completada
                && c.Estado != EstadoCita.Cancelada
                && c.Estado != EstadoCita.NoAsistio);
        }

        var citas = await query
            .OrderByDescending(c => c.FechaHoraProgramada)
            .Take(tope)
            .ToListAsync(ct);

        var items = citas.Select(c => new WhatsAppCitaClienteResponse(
            Id: c.Id,
            NumeroCita: c.NumeroCita,
            FechaHoraProgramada: c.FechaHoraProgramada,
            DuracionMinutos: c.DuracionMinutos,
            Estado: c.Estado,
            Motivo: c.Motivo,
            VehiculoId: c.VehiculoId,
            VehiculoResumen: c.Vehiculo is not null ? FormatearVehiculoResumen(c.Vehiculo) : null,
            OrdenServicioId: c.OrdenServicioId))
            .ToList();

        _logger.LogInformation(
            "WhatsApp CRM consulta citas. ConversacionId={ConversacionId}, ClienteId={ClienteId}, Modo={Modo}, Total={Total}",
            conversacionId,
            clienteId,
            ctx.Modo,
            items.Count);

        return ServiceResult<List<WhatsAppCitaClienteResponse>>.Success(items);
    }

    public async Task<WhatsAppCatalogoConsultaResponse> ConsultarProductosYServiciosAsync(
        string? busqueda = null,
        bool incluirProductos = true,
        bool incluirServicios = true,
        int limite = 15,
        WhatsAppModoConsultaCrm modo = WhatsAppModoConsultaCrm.ConversacionalCliente,
        CancellationToken ct = default)
    {
        var tope = Math.Clamp(limite, 1, 50);
        var termino = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim().ToLower();
        var parametrosPrecio = await PrecioLista.ParametrosAsync(_context, ct);

        var productosResp = new List<WhatsAppProductoCatalogoResponse>();
        if (incluirProductos)
        {
            // Solo productos activos cuya categoría también esté activa (B-04).
            var prodQuery = _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Where(p => p.Activo && p.Categoria.Activo);

            if (termino is not null)
            {
                prodQuery = prodQuery.Where(p =>
                    p.Codigo.ToLower().Contains(termino)
                    || p.Nombre.ToLower().Contains(termino)
                    || (p.Marca != null && p.Marca.ToLower().Contains(termino))
                    || (p.Descripcion != null && p.Descripcion.ToLower().Contains(termino)));
            }

            var productos = await prodQuery
                .OrderBy(p => p.Nombre)
                .Take(tope)
                .ToListAsync(ct);

            foreach (var p in productos)
            {
                // Nunca exponer Producto.Costo, nunca usar PrecioVenta directo como soles ni presentar <= 0 como precio válido.
                bool precioDisponible = false;
                decimal? precioBaseSinIgv = null;
                decimal? precioConIgv = null;

                if (p.PrecioVenta > 0)
                {
                    var precioLista = PrecioLista.Calcular(p, parametrosPrecio.TipoCambio, parametrosPrecio.PorcentajeIgv);
                    if (precioLista.HasValue)
                    {
                        var (baseSinIgv, _, totalConIgv, _) = OrdenServicioService.CalcularFinanzasLinea(
                            cantidad: 1,
                            precioUnitario: precioLista.Value.SinIgv,
                            tipoAfectacion: TipoAfectacionIgv.Gravado,
                            porcentajeIgvVigente: parametrosPrecio.PorcentajeIgv,
                            precioUnitarioConIgv: precioLista.Value.ConIgv);

                        precioDisponible = true;
                        precioBaseSinIgv = baseSinIgv;
                        precioConIgv = totalConIgv;
                    }
                }

                // Stock exacto solo para uso administrativo interno; en modo conversacional solo se informa disponibilidad booleana (B-01).
                int? stockVisible = modo == WhatsAppModoConsultaCrm.AdministrativoInterno
                    ? p.StockActual
                    : null;

                productosResp.Add(new WhatsAppProductoCatalogoResponse(
                    Id: p.Id,
                    Codigo: p.Codigo,
                    Nombre: p.Nombre,
                    Marca: p.Marca,
                    CategoriaNombre: p.Categoria?.Nombre,
                    Tipo: p.Tipo,
                    Unidad: p.Unidad,
                    Disponible: p.StockActual > 0,
                    StockDisponible: stockVisible,
                    PrecioDisponible: precioDisponible,
                    PrecioBaseSinIgvSoles: precioBaseSinIgv,
                    PrecioReferencialConIgvSoles: precioConIgv,
                    PrecioIncluyeIgv: p.PrecioIncluyeIgv));
            }
        }

        var serviciosResp = new List<WhatsAppServicioCatalogoResponse>();
        if (incluirServicios)
        {
            var servQuery = _context.Servicios
                .AsNoTracking()
                .Where(s => s.Activo);

            if (termino is not null)
            {
                servQuery = servQuery.Where(s => s.Nombre.ToLower().Contains(termino));
            }

            var servicios = await servQuery
                .OrderBy(s => s.Nombre)
                .Take(tope)
                .ToListAsync(ct);

            foreach (var s in servicios)
            {
                bool precioDisponible = s.PrecioSugerido > 0;
                decimal? precioSugeridoVisible = null;
                decimal? precioConIgvVisible = null;

                if (precioDisponible)
                {
                    var precioEscrito = PrecioLista.DeEscrito(
                        s.PrecioSugerido,
                        s.PrecioIncluyeIgv,
                        s.TipoAfectacionIgv,
                        parametrosPrecio.PorcentajeIgv);

                    var (_, _, totalConIgv, _) = OrdenServicioService.CalcularFinanzasLinea(
                        cantidad: 1,
                        precioUnitario: precioEscrito.SinIgv,
                        tipoAfectacion: s.TipoAfectacionIgv,
                        porcentajeIgvVigente: parametrosPrecio.PorcentajeIgv,
                        precioUnitarioConIgv: precioEscrito.ConIgv);

                    precioSugeridoVisible = s.PrecioSugerido;
                    precioConIgvVisible = totalConIgv;
                }

                serviciosResp.Add(new WhatsAppServicioCatalogoResponse(
                    Id: s.Id,
                    Nombre: s.Nombre,
                    PrecioDisponible: precioDisponible,
                    PrecioSugeridoSoles: precioSugeridoVisible,
                    PrecioIncluyeIgv: s.PrecioIncluyeIgv,
                    TipoAfectacionIgv: s.TipoAfectacionIgv,
                    PrecioReferencialConIgv: precioConIgvVisible));
            }
        }

        return new WhatsAppCatalogoConsultaResponse(productosResp, serviciosResp);
    }

    private async Task<ServiceResult<(ConversacionWhatsApp Conversacion, Cliente Cliente)>> ResolverClienteVinculadoAsync(
        Guid conversacionId,
        WhatsAppContextoConsultaCrm contexto,
        CancellationToken ct)
    {
        // Barrera obligatoria H-02: ninguna consulta personal en modo conversacional/IA puede proceder
        // únicamente por coincidencia de teléfono ni por ConversacionWhatsApp.ClienteId != null.
        if (contexto.Modo == WhatsAppModoConsultaCrm.ConversacionalCliente)
        {
            if (contexto.EstadoVerificacionIdentidad != EstadoVerificacionIdentidadWhatsApp.VerificadaPorMecanismoAprobado
                || !await _verificadorIdentidad.EstaIdentidadVerificadaAsync(conversacionId, ct))
            {
                return ServiceResult<(ConversacionWhatsApp, Cliente)>.Forbidden(
                    "Consulta personal denegada en modo conversacional: la coincidencia telefónica no acredita titularidad y requiere verificación explícita de identidad.");
            }
        }

        var conversacion = await _context.ConversacionesWhatsApp
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return ServiceResult<(ConversacionWhatsApp, Cliente)>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        // Validación defensiva B-03: jamás resolver datos personales si la asociación está marcada como ambigua.
        if (conversacion.AsociacionClienteAmbigua)
        {
            return ServiceResult<(ConversacionWhatsApp, Cliente)>.Conflict(
                "La conversación tiene una asociación de cliente ambigua pendiente de resolución manual.");
        }

        if (!conversacion.ClienteId.HasValue)
        {
            return ServiceResult<(ConversacionWhatsApp, Cliente)>.Invalid(
                "La conversación no tiene un cliente vinculado. Vincule un cliente antes de consultar información del CRM.");
        }

        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversacion.ClienteId.Value && c.Activo, ct);

        if (cliente is null)
        {
            return ServiceResult<(ConversacionWhatsApp, Cliente)>.NotFound(
                "El cliente vinculado a la conversación no existe o se encuentra inactivo.");
        }

        // Defensa adicional para modo conversacional (números reciclados o teléfono actualizado en CRM).
        if (contexto.Modo == WhatsAppModoConsultaCrm.ConversacionalCliente)
        {
            var codigoPais = _settingsMonitor.CurrentValue.DefaultCountryCode;
            var telefonosCliente = WhatsAppPhoneNormalizer.ExtraerTelefonosNormalizadosE164(cliente.Telefono, codigoPais);
            if (!telefonosCliente.Contains(conversacion.TelefonoE164, StringComparer.Ordinal))
            {
                return ServiceResult<(ConversacionWhatsApp, Cliente)>.Forbidden(
                    "El teléfono de la conversación no coincide actualmente con el cliente vinculado; se requiere verificación manual.");
            }
        }

        return ServiceResult<(ConversacionWhatsApp, Cliente)>.Success((conversacion, cliente));
    }

    private static ServiceResult<T> MapError<T>(ServiceResult<(ConversacionWhatsApp Conversacion, Cliente Cliente)> origen) => origen.Status switch
    {
        ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(origen.Error),
        ServiceResultStatus.Forbidden => ServiceResult<T>.Forbidden(origen.Error),
        ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(origen.Error ?? "Conflicto de estado."),
        _ => ServiceResult<T>.Invalid(origen.Error ?? "Operación inválida.")
    };

    private static string TraducirEstadoEntrega(EstadoOrdenServicio estado) => estado switch
    {
        EstadoOrdenServicio.Entregada => "Entregada",
        EstadoOrdenServicio.Lista => "Lista para entrega",
        EstadoOrdenServicio.Cancelada => "Cancelada",
        _ => "Pendiente de entrega"
    };

    private static string FormatearVehiculoResumen(Vehiculo? v)
    {
        if (v is null) return "Vehículo no especificado";
        var baseTexto = string.Join(" ", new[] { v.Marca, v.Modelo }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (v.Anio.HasValue) baseTexto = $"{baseTexto} ({v.Anio.Value})";
        if (!string.IsNullOrWhiteSpace(v.Placa)) baseTexto = $"{baseTexto} - Placa {v.Placa.Trim()}";
        return string.IsNullOrWhiteSpace(baseTexto) ? "Vehículo sin descripción" : baseTexto;
    }

    private static List<TipoAtencion> ObtenerTiposAtencion(OrdenServicio orden) =>
        orden.TiposAtencion is { Count: > 0 }
            ? orden.TiposAtencion.ToList()
            : new List<TipoAtencion> { orden.TipoAtencion };

    private static string? ConstruirResumenTrabajos(OrdenServicio orden)
    {
        if (!string.IsNullOrWhiteSpace(orden.Solucion))
        {
            return orden.Solucion.Trim();
        }

        var items = orden.Detalles
            .Where(d => d.Activo && !string.IsNullOrWhiteSpace(d.Descripcion))
            .OrderBy(d => d.FechaCreacion)
            .Select(d => d.Descripcion.Trim())
            .Take(5)
            .ToList();

        return items.Count > 0 ? string.Join("; ", items) : null;
    }
}
