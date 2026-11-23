using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp.IA;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoOrquestacionWhatsApp
{
    Omitido = 0,
    Respondido = 1,
    RespondidoYEscalado = 2,
    FallbackYEscalado = 3
}

public sealed record ResultadoOrquestacionWhatsApp(
    EstadoOrquestacionWhatsApp Estado,
    string MotivoCodigo,
    IntencionWhatsApp? Intencion,
    Guid? MensajeRespuestaId,
    bool EscaladoAHumano,
    bool IdentidadVerificada);

/// <summary>
/// Orquestador conversacional de WhatsApp Fase 3.
/// Coordina guardias de habilitación/estado/concurrencia/anti-bucle multiinstancia (FOR UPDATE en PostgreSQL),
/// clasificación de intención, herramientas CRM de solo lectura con identidad fail-closed,
/// generación de respuesta fuera de transacción, persistencia atómica de escalamiento + mensaje + Outbox
/// y despacho idempotente.
/// </summary>
public interface IWhatsAppConversacionOrquestador
{
    Task<ResultadoOrquestacionWhatsApp> ProcesarMensajeEntranteAsync(
        Guid conversacionId,
        Guid mensajeEntranteId,
        CancellationToken ct = default);
}

public sealed class WhatsAppConversacionOrquestador : IWhatsAppConversacionOrquestador
{
    private const string MensajeFallbackSeguro =
        "En este momento no puedo procesar tu consulta automáticamente. Te estoy derivando con un asesor de Team Benavides para atenderte a la brevedad.";

    private const string MensajeLimiteFrecuencia =
        "He recibido varios mensajes seguidos en esta conversación. Para atenderte mejor, te derivo con un asesor humano de Team Benavides.";

    private static readonly KeyedAsyncLock<Guid> LocksConversacion = new();

    private readonly ApplicationDbContext _context;
    private readonly IWhatsAppIntentClassifier _intentClassifier;
    private readonly IWhatsAppAiCrmToolsFacade _crmTools;
    private readonly IWhatsAppAiProvider _aiProvider;
    private readonly IWhatsAppEscalamientoService _escalamientoService;
    private readonly IWhatsAppOutboxService _outboxService;
    private readonly IOptionsMonitor<WhatsAppAiSettings> _aiSettingsMonitor;
    private readonly IOptionsMonitor<WhatsAppSettings> _waSettingsMonitor;
    private readonly ILogger<WhatsAppConversacionOrquestador> _logger;

    public WhatsAppConversacionOrquestador(
        ApplicationDbContext context,
        IWhatsAppIntentClassifier intentClassifier,
        IWhatsAppAiCrmToolsFacade crmTools,
        IWhatsAppAiProvider aiProvider,
        IWhatsAppEscalamientoService escalamientoService,
        IWhatsAppOutboxService outboxService,
        IOptionsMonitor<WhatsAppAiSettings> aiSettingsMonitor,
        IOptionsMonitor<WhatsAppSettings> waSettingsMonitor,
        ILogger<WhatsAppConversacionOrquestador> logger)
    {
        _context = context;
        _intentClassifier = intentClassifier;
        _crmTools = crmTools;
        _aiProvider = aiProvider;
        _escalamientoService = escalamientoService;
        _outboxService = outboxService;
        _aiSettingsMonitor = aiSettingsMonitor;
        _waSettingsMonitor = waSettingsMonitor;
        _logger = logger;
    }

    public async Task<ResultadoOrquestacionWhatsApp> ProcesarMensajeEntranteAsync(
        Guid conversacionId,
        Guid mensajeEntranteId,
        CancellationToken ct = default)
    {
        var aiSettings = _aiSettingsMonitor.CurrentValue;
        if (!aiSettings.Enabled)
        {
            return Omitir("AI_DESHABILITADA");
        }

        // F3-H01: jamás generar mensajes ni acumular Outbox si WhatsApp está deshabilitado o mal configurado.
        var waSettings = _waSettingsMonitor.CurrentValue;
        if (!waSettings.Enabled)
        {
            return Omitir("WHATSAPP_DESHABILITADO");
        }

        if (waSettings.ValidarConfiguracionEnvio() is not null)
        {
            return Omitir("ENVIO_NO_CONFIGURADO");
        }

        if (ct.IsCancellationRequested)
        {
            return Omitir("CANCELLED");
        }

        var sw = Stopwatch.StartNew();

        // Bloqueo local por conversación; en PostgreSQL se complementa con SELECT ... FOR UPDATE en la fase de commit.
        IDisposable lockConv;
        try
        {
            lockConv = await LocksConversacion.AcquireAsync(conversacionId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Omitir("CANCELLED");
        }

        using (lockConv)
        {
            // FASE 1 — Validaciones rápidas sin transacción abierta.
            var conversacion = await _context.ConversacionesWhatsApp
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

            var motivoBloqueoInicial = ValidarElegibilidadConversacion(conversacion);
            if (motivoBloqueoInicial is not null)
            {
                return Omitir(motivoBloqueoInicial);
            }

            var mensaje = await _context.MensajesWhatsApp
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == mensajeEntranteId && m.ConversacionId == conversacionId && m.Activo, ct);

            if (mensaje is null)
            {
                return Omitir("MENSAJE_NO_ENCONTRADO");
            }

            // Jamás responder a mensajes salientes (prevención absoluta de bucles).
            if (mensaje.Direccion != DireccionMensajeWhatsApp.Entrante)
            {
                return Omitir("NO_ES_MENSAJE_ENTRANTE");
            }

            var idempotencyKey = $"ai:reply:{mensaje.Id:N}";
            var yaRespondido = await _context.MensajesWhatsApp
                .AsNoTracking()
                .AnyAsync(m => m.IdempotencyKey == idempotencyKey, ct);

            if (yaRespondido)
            {
                return Omitir("RESPUESTA_DUPLICADA");
            }

            // Pre-chequeo de frecuencia: si ya superó el tope en 60s, evitar invocar el proveedor IA e ir directo al commit atómico.
            var ventanaInicioUtc = DateTime.UtcNow.AddMinutes(-1);
            var respuestasRecientesBot = await ContarRespuestasBotRecientesAsync(conversacionId, ventanaInicioUtc, ct);
            var maxPorMinuto = aiSettings.ObtenerMaxRespuestasPorMinutoSeguro();

            string textoFinal;
            bool debeEscalar;
            EstadoOrquestacionWhatsApp estadoFinal;
            string motivoFinal;
            IntencionWhatsApp intencionFinal;
            string proveedorUsado;
            string modeloUsado;
            string? errorCodigo;
            bool identidadVerificada = false;

            if (respuestasRecientesBot >= maxPorMinuto)
            {
                textoFinal = MensajeLimiteFrecuencia;
                debeEscalar = true;
                estadoFinal = EstadoOrquestacionWhatsApp.FallbackYEscalado;
                motivoFinal = "RATE_LIMIT_ANTI_BUCLE";
                intencionFinal = IntencionWhatsApp.SolicitudAgenteHumano;
                proveedorUsado = "policy_guard";
                modeloUsado = "rate_limit";
                errorCodigo = "RATE_LIMIT_ANTI_BUCLE";
            }
            else
            {
                // FASE 2 — Clasificación, contexto autorizado y generación IA FUERA de cualquier transacción de BD.
                var maxInput = aiSettings.ObtenerMaxInputCharsSeguro();
                var textoUsuario = (mensaje.ContenidoTexto ?? string.Empty).Trim();
                if (textoUsuario.Length > maxInput)
                {
                    textoUsuario = textoUsuario[..maxInput];
                }

                var clasificacion = _intentClassifier.Clasificar(textoUsuario);
                intencionFinal = clasificacion.Intencion;

                WhatsAppAiContextoAutorizado contextoAutorizado;
                IReadOnlyList<WhatsAppAiMensajeHistorial> historial;

                // F3-M02: ante prompt injection no consultar BD, ni catálogo, ni info del taller ni cargar historial.
                if (clasificacion.EsIntentoPromptInjection || clasificacion.Intencion == IntencionWhatsApp.IntentoManipulacionPrompt)
                {
                    contextoAutorizado = new WhatsAppAiContextoAutorizado(
                        IdentidadVerificada: false,
                        ConsultaPersonalBloqueadaPorFaltaVerificacion: false,
                        InformacionTaller: null,
                        FaqsCoincidentes: Array.Empty<WhatsAppFaqAutorizada>(),
                        ProductosPublicos: Array.Empty<WhatsAppProductoCatalogoResponse>(),
                        ServiciosPublicos: Array.Empty<WhatsAppServicioCatalogoResponse>());
                    historial = Array.Empty<WhatsAppAiMensajeHistorial>();
                }
                else
                {
                    contextoAutorizado = await ConstruirContextoAutorizadoAsync(
                        conversacionId,
                        textoUsuario,
                        clasificacion,
                        aiSettings,
                        ct);

                    historial = await ObtenerHistorialSanitizadoAsync(
                        conversacionId,
                        mensajeEntranteId,
                        conversacion!.FechaCambioModoHumano,
                        aiSettings.ObtenerMaxHistoryMessagesSeguro(),
                        ct);
                }

                identidadVerificada = contextoAutorizado.IdentidadVerificada;

                var solicitudAi = new WhatsAppAiSolicitud(
                    ConversacionId: conversacionId,
                    MensajeUsuarioNoConfiable: textoUsuario,
                    Clasificacion: clasificacion,
                    HistorialReciente: historial,
                    ContextoAutorizado: contextoAutorizado,
                    MaxOutputTokens: aiSettings.ObtenerMaxOutputTokensSeguro(),
                    MaxOutputChars: aiSettings.ObtenerMaxOutputCharsSeguro());

                WhatsAppAiRespuesta respuestaAi;
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(aiSettings.ObtenerTimeoutSegundosSeguro()));

                try
                {
                    respuestaAi = await _aiProvider.GenerarRespuestaAsync(solicitudAi, timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Timeout interno del proveedor (NO cancelación externa del caller) -> fallback seguro y escalamiento.
                    respuestaAi = new WhatsAppAiRespuesta(
                        Exitoso: false,
                        TextoRespuesta: null,
                        SugiereEscalamientoHumano: true,
                        Proveedor: _aiProvider.NombreProveedor,
                        Modelo: aiSettings.Model,
                        TokensEstimadosEntrada: 0,
                        TokensEstimadosSalida: 0,
                        CodigoError: "TIMEOUT",
                        ErrorSanitizado: "Tiempo de espera agotado al consultar el proveedor de IA.");
                }
                catch (OperationCanceledException)
                {
                    // F3-M01: cancelación externa del caller o apagado del servicio -> omitir limpiamente SIN escrituras ni escalamiento.
                    return Omitir("CANCELLED");
                }
                catch (Exception ex)
                {
                    var detalleSeguro = WhatsAppWebhookSecurity.SanitizarMensajeError(
                        ex.Message,
                        aiSettings.ApiKey,
                        waSettings.AccessToken,
                        waSettings.AppSecret,
                        waSettings.WebhookVerifyToken);

                    respuestaAi = new WhatsAppAiRespuesta(
                        Exitoso: false,
                        TextoRespuesta: null,
                        SugiereEscalamientoHumano: true,
                        Proveedor: _aiProvider.NombreProveedor,
                        Modelo: aiSettings.Model,
                        TokensEstimadosEntrada: 0,
                        TokensEstimadosSalida: 0,
                        CodigoError: "PROVIDER_EXCEPTION",
                        ErrorSanitizado: detalleSeguro);
                }

                proveedorUsado = respuestaAi.Proveedor;
                modeloUsado = respuestaAi.Modelo;
                errorCodigo = respuestaAi.CodigoError;

                if (!respuestaAi.Exitoso || string.IsNullOrWhiteSpace(respuestaAi.TextoRespuesta))
                {
                    textoFinal = MensajeFallbackSeguro;
                    debeEscalar = true;
                    estadoFinal = EstadoOrquestacionWhatsApp.FallbackYEscalado;
                    motivoFinal = respuestaAi.CodigoError ?? "PROVIDER_ERROR";
                }
                else
                {
                    textoFinal = respuestaAi.TextoRespuesta.Trim();
                    var maxOut = Math.Min(aiSettings.ObtenerMaxOutputCharsSeguro(), 4096);
                    var maxOutPorTokens = aiSettings.ObtenerMaxOutputTokensSeguro() * 4;
                    var limiteSalida = Math.Min(maxOut, maxOutPorTokens);
                    if (textoFinal.Length > limiteSalida)
                    {
                        textoFinal = textoFinal[..(limiteSalida - 3)].TrimEnd() + "...";
                    }

                    debeEscalar = clasificacion.RequiereEscalamientoHumano
                        || respuestaAi.SugiereEscalamientoHumano
                        || contextoAutorizado.ConsultaPersonalBloqueadaPorFaltaVerificacion;

                    estadoFinal = debeEscalar
                        ? EstadoOrquestacionWhatsApp.RespondidoYEscalado
                        : EstadoOrquestacionWhatsApp.Respondido;

                    motivoFinal = clasificacion.MotivoClasificacion;
                }
            }

            // F3-M01: si se canceló antes de persistir, jamás intentar escribir en BD con token cancelado.
            if (ct.IsCancellationRequested)
            {
                return Omitir("CANCELLED");
            }

            // FASE 3 — Transacción atómica con SELECT ... FOR UPDATE en PostgreSQL (F3-H02 + F3-M03 + F3-M04):
            // Re-verifica configuración, intervención humana, cierre, idempotencia y rate limit multiinstancia,
            // y persiste atómicamente escalamiento + MensajeWhatsApp + OutboxMensajeWhatsApp en un único commit.
            var persistido = await ConfirmarYPersistirAtomicamenteAsync(
                conversacionId,
                idempotencyKey,
                textoFinal,
                intencionFinal,
                estadoFinal,
                motivoFinal,
                proveedorUsado,
                modeloUsado,
                debeEscalar,
                errorCodigo,
                maxPorMinuto,
                ct);

            if (!persistido.Persistido)
            {
                return Omitir(persistido.MotivoOmision ?? "OMITIDO_EN_COMMIT");
            }

            if (persistido.EscalamientoAplicado)
            {
                _escalamientoService.RegistrarTrazaEscalamiento(conversacionId, persistido.MotivoFinal);
            }

            // FASE 4 — Despacho por Outbox FUERA de la transacción de BD (jamás mantener lock durante HTTP).
            if (persistido.OutboxId.HasValue && _waSettingsMonitor.CurrentValue.ValidarConfiguracionEnvio() is null)
            {
                await _outboxService.ProcesarRegistroAsync(persistido.OutboxId.Value, ct);
            }

            RegistrarTrazaSinPii(
                conversacionId,
                mensajeEntranteId,
                persistido.IntencionFinal,
                persistido.EstadoFinal,
                persistido.EscalamientoAplicado,
                persistido.ProveedorFinal,
                sw.ElapsedMilliseconds);

            return new ResultadoOrquestacionWhatsApp(
                Estado: persistido.EstadoFinal,
                MotivoCodigo: persistido.MotivoFinal,
                Intencion: persistido.IntencionFinal,
                MensajeRespuestaId: persistido.MensajeRespuestaId,
                EscaladoAHumano: persistido.EscalamientoAplicado,
                IdentidadVerificada: identidadVerificada);
        }
    }

    private sealed record ResultadoPersistenciaAtomica(
        bool Persistido,
        string? MotivoOmision,
        Guid? MensajeRespuestaId,
        Guid? OutboxId,
        bool EscalamientoAplicado,
        ModoAtencionWhatsApp ModoAnterior,
        EstadoConversacionWhatsApp EstadoAnterior,
        EstadoOrquestacionWhatsApp EstadoFinal,
        string MotivoFinal,
        IntencionWhatsApp IntencionFinal,
        string ProveedorFinal);

    private async Task<ResultadoPersistenciaAtomica> ConfirmarYPersistirAtomicamenteAsync(
        Guid conversacionId,
        string idempotencyKey,
        string textoFinal,
        IntencionWhatsApp intencionFinal,
        EstadoOrquestacionWhatsApp estadoFinal,
        string motivoFinal,
        string proveedorUsado,
        string modeloUsado,
        bool debeEscalar,
        string? errorCodigo,
        int maxPorMinuto,
        CancellationToken ct)
    {
        static ResultadoPersistenciaAtomica Omitido(string motivo) => new(
            Persistido: false,
            MotivoOmision: motivo,
            MensajeRespuestaId: null,
            OutboxId: null,
            EscalamientoAplicado: false,
            ModoAnterior: ModoAtencionWhatsApp.Bot,
            EstadoAnterior: EstadoConversacionWhatsApp.Abierta,
            EstadoFinal: EstadoOrquestacionWhatsApp.Omitido,
            MotivoFinal: motivo,
            IntencionFinal: IntencionWhatsApp.FueraDeAlcanceOAmbigua,
            ProveedorFinal: string.Empty);

        try
        {
            await using var tx = _context.Database.IsNpgsql()
                ? await _context.Database.BeginTransactionAsync(ct)
                : null;

            // Re-verificar flags de habilitación en el instante del commit (F3-H01).
            var aiNow = _aiSettingsMonitor.CurrentValue;
            var waNow = _waSettingsMonitor.CurrentValue;
            if (!aiNow.Enabled)
            {
                if (tx is not null) await tx.RollbackAsync(CancellationToken.None);
                return Omitido("AI_DESHABILITADA");
            }
            if (!waNow.Enabled || waNow.ValidarConfiguracionEnvio() is not null)
            {
                if (tx is not null) await tx.RollbackAsync(CancellationToken.None);
                return Omitido(!waNow.Enabled ? "WHATSAPP_DESHABILITADO" : "ENVIO_NO_CONFIGURADO");
            }

            // En PostgreSQL adquirir bloqueo exclusivo FOR UPDATE sobre la conversación:
            // serializa toma de control humano, rate limit multiinstancia y persistencia atómica (F3-H02 / F3-M03).
            ConversacionWhatsApp? convActual;
            if (_context.Database.IsNpgsql())
            {
                convActual = await _context.ConversacionesWhatsApp
                    .FromSqlInterpolated($"""
                        SELECT * FROM "ConversacionesWhatsApp"
                        WHERE "Id" = {conversacionId} AND "Activo" = true
                        FOR UPDATE
                        """)
                    .FirstOrDefaultAsync(ct);
            }
            else
            {
                convActual = await _context.ConversacionesWhatsApp
                    .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);
            }

            if (convActual is not null)
            {
                // F3-H02: si la conversación ya estaba rastreada en el mismo DbContext scoped
                // (p. ej. desde WhatsAppWebhookService), EF Core devolvería la instancia en caché sin refrescarla.
                // ReloadAsync dentro de la transacción ya bloqueada con FOR UPDATE garantiza valores reales de PostgreSQL.
                await _context.Entry(convActual).ReloadAsync(ct);
                if (!convActual.Activo)
                {
                    convActual = null;
                }
            }

            // Re-validar que ningún agente tomó ni cerró la conversación durante la generación IA (F3-H02 / F3-M04).
            var motivoBloqueo = ValidarElegibilidadConversacion(convActual);
            if (motivoBloqueo is not null)
            {
                if (tx is not null) await tx.RollbackAsync(CancellationToken.None);
                return Omitido(motivoBloqueo);
            }

            // Re-validar idempotencia bajo el bloqueo exclusivo.
            var yaRespondido = await _context.MensajesWhatsApp
                .AsNoTracking()
                .AnyAsync(m => m.IdempotencyKey == idempotencyKey, ct);
            if (yaRespondido)
            {
                if (tx is not null) await tx.RollbackAsync(CancellationToken.None);
                return Omitido("RESPUESTA_DUPLICADA");
            }

            // Rate limit atómico multiinstancia bajo el bloqueo de fila FOR UPDATE (F3-M03).
            var recientesBajoLock = await ContarRespuestasBotRecientesAsync(conversacionId, DateTime.UtcNow.AddMinutes(-1), ct);
            if (recientesBajoLock >= maxPorMinuto && motivoFinal != "RATE_LIMIT_ANTI_BUCLE")
            {
                textoFinal = MensajeLimiteFrecuencia;
                debeEscalar = true;
                estadoFinal = EstadoOrquestacionWhatsApp.FallbackYEscalado;
                motivoFinal = "RATE_LIMIT_ANTI_BUCLE";
                intencionFinal = IntencionWhatsApp.SolicitudAgenteHumano;
                proveedorUsado = "policy_guard";
                modeloUsado = "rate_limit";
                errorCodigo = "RATE_LIMIT_ANTI_BUCLE";
            }

            var ahora = DateTime.UtcNow;
            var modoAnterior = convActual!.ModoAtencion;
            var estadoAnterior = convActual.Estado;
            var fechaCambioHumanoAnterior = convActual.FechaCambioModoHumano;
            var modificadoPorAnterior = convActual.ModificadoPorId;
            var fechaModificacionAnterior = convActual.FechaModificacion;
            bool escalamientoAplicado = false;

            if (debeEscalar)
            {
                convActual.ModoAtencion = ModoAtencionWhatsApp.Humano;
                convActual.Estado = EstadoConversacionWhatsApp.EnAtencion;
                convActual.FechaCambioModoHumano = ahora;
                convActual.ModificadoPorId = null;
                convActual.FechaModificacion = ahora;
                escalamientoAplicado = true;

                // F3-M03: encolar WHATSAPP_ESCALAR_HUMANO dentro de la misma unidad de trabajo y transacción
                // para que se confirme o revierta atómicamente junto con modo/estado, mensaje y Outbox.
                _escalamientoService.EncolarAuditoriaEscalamientoEnTransaccion(
                    convActual.Id,
                    motivoFinal,
                    modoAnterior,
                    estadoAnterior,
                    ahora);
            }

            var metadataJson = JsonSerializer.Serialize(new
            {
                origen = "ia_fase3",
                intencion = intencionFinal.ToString(),
                proveedor = proveedorUsado,
                modelo = modeloUsado,
                escalado = escalamientoAplicado,
                identidadVerificada = false,
                errorCodigo
            });

            var mensajeSaliente = new MensajeWhatsApp
            {
                ConversacionId = convActual.Id,
                IdempotencyKey = idempotencyKey,
                Direccion = DireccionMensajeWhatsApp.Saliente,
                Tipo = TipoMensajeWhatsApp.Texto,
                ContenidoTexto = textoFinal,
                Estado = EstadoMensajeWhatsApp.Pendiente,
                FechaEventoUtc = ahora,
                FechaRecepcionUtc = ahora,
                EnviadoPorUsuarioId = null,
                MetadataTecnicaJson = metadataJson,
                FechaCreacion = ahora,
                Activo = true
            };

            var outbox = new OutboxMensajeWhatsApp
            {
                MensajeWhatsApp = mensajeSaliente,
                ConversacionId = convActual.Id,
                DestinatarioE164 = convActual.TelefonoE164,
                ContenidoTexto = textoFinal,
                IdempotencyKey = idempotencyKey,
                Estado = EstadoOutboxWhatsApp.Pendiente,
                Intentos = 0,
                MaxIntentos = Math.Max(1, waNow.MaxSendRetries),
                ProximoIntentoUtc = ahora,
                FechaCreacion = ahora,
                Activo = true
            };

            _context.MensajesWhatsApp.Add(mensajeSaliente);
            _context.OutboxMensajesWhatsApp.Add(outbox);

            void LimpiarCambiosFallidosEnTracker()
            {
                // MED-01: limpiar exclusivamente las entidades/propiedades tocadas por esta transacción fallida
                // sin usar ChangeTracker.Clear(), preservando cambios legítimos del webhook y jamás ocultando la excepción original.
                try
                {
                    var entryOutbox = _context.Entry(outbox);
                    if (entryOutbox.State != EntityState.Detached)
                    {
                        entryOutbox.State = EntityState.Detached;
                    }

                    var entryMsg = _context.Entry(mensajeSaliente);
                    if (entryMsg.State != EntityState.Detached)
                    {
                        entryMsg.State = EntityState.Detached;
                    }

                    if (escalamientoAplicado)
                    {
                        var convIdStr = convActual.Id.ToString();
                        foreach (var entryAud in _context.ChangeTracker.Entries<EventoAuditoria>()
                            .Where(e => e.State == EntityState.Added
                                && e.Entity.Entidad == "ConversacionWhatsApp"
                                && e.Entity.EntidadId == convIdStr
                                && e.Entity.Accion == "WHATSAPP_ESCALAR_HUMANO")
                            .ToList())
                        {
                            entryAud.State = EntityState.Detached;
                        }

                        convActual.ModoAtencion = modoAnterior;
                        convActual.Estado = estadoAnterior;
                        convActual.FechaCambioModoHumano = fechaCambioHumanoAnterior;
                        convActual.ModificadoPorId = modificadoPorAnterior;
                        convActual.FechaModificacion = fechaModificacionAnterior;

                        var entryConv = _context.Entry(convActual);
                        if (entryConv.State == EntityState.Modified)
                        {
                            entryConv.State = EntityState.Unchanged;
                        }
                    }
                }
                catch
                {
                    // Jamás ocultar la excepción original durante la limpieza defensiva del ChangeTracker.
                }
            }

            async Task RollbackSeguroAsync()
            {
                if (tx is null) return;
                try
                {
                    await tx.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Jamás ocultar la excepción original si la conexión/transacción ya fue cerrada.
                }
            }

            try
            {
                await _context.SaveChangesAsync(ct);
                if (tx is not null) await tx.CommitAsync(ct);
            }
            catch (DbUpdateException)
            {
                await RollbackSeguroAsync();
                LimpiarCambiosFallidosEnTracker();

                var existeColision = await _context.MensajesWhatsApp
                    .AsNoTracking()
                    .AnyAsync(m => m.IdempotencyKey == idempotencyKey, CancellationToken.None);

                if (existeColision)
                {
                    return Omitido("RESPUESTA_DUPLICADA");
                }

                throw;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await RollbackSeguroAsync();
                LimpiarCambiosFallidosEnTracker();
                return Omitido("CANCELLED");
            }
            catch
            {
                await RollbackSeguroAsync();
                LimpiarCambiosFallidosEnTracker();
                throw;
            }

            return new ResultadoPersistenciaAtomica(
                Persistido: true,
                MotivoOmision: null,
                MensajeRespuestaId: mensajeSaliente.Id,
                OutboxId: outbox.Id,
                EscalamientoAplicado: escalamientoAplicado,
                ModoAnterior: modoAnterior,
                EstadoAnterior: estadoAnterior,
                EstadoFinal: estadoFinal,
                MotivoFinal: motivoFinal,
                IntencionFinal: intencionFinal,
                ProveedorFinal: proveedorUsado);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // F3-M01: cancelación durante persistencia -> rollback implícito al disponer tx, cero escrituras parciales.
            return Omitido("CANCELLED");
        }
    }

    private static string? ValidarElegibilidadConversacion(ConversacionWhatsApp? conversacion)
    {
        if (conversacion is null) return "CONVERSACION_NO_ENCONTRADA";
        if (conversacion.Estado == EstadoConversacionWhatsApp.Cerrada) return "CONVERSACION_CERRADA";
        if (conversacion.ModoAtencion != ModoAtencionWhatsApp.Bot) return "MODO_HUMANO";
        if (conversacion.AgenteAsignadoId.HasValue) return "ASIGNADA_A_AGENTE";
        if (conversacion.Estado == EstadoConversacionWhatsApp.EnAtencion) return "EN_ATENCION_HUMANA";
        return null;
    }

    private Task<int> ContarRespuestasBotRecientesAsync(Guid conversacionId, DateTime ventanaInicioUtc, CancellationToken ct) =>
        _context.MensajesWhatsApp
            .AsNoTracking()
            .CountAsync(m =>
                m.ConversacionId == conversacionId
                && m.Activo
                && m.Direccion == DireccionMensajeWhatsApp.Saliente
                && m.EnviadoPorUsuarioId == null
                && m.FechaRecepcionUtc >= ventanaInicioUtc, ct);

    private async Task<WhatsAppAiContextoAutorizado> ConstruirContextoAutorizadoAsync(
        Guid conversacionId,
        string textoUsuario,
        ResultadoClasificacionIntencion clasificacion,
        WhatsAppAiSettings aiSettings,
        CancellationToken ct)
    {
        // Cualquier intención personal pasa obligatoriamente por la fachada fail-closed.
        if (clasificacion.RequiereIdentidadVerificada)
        {
            var chequeoAcceso = clasificacion.Intencion switch
            {
                IntencionWhatsApp.ConsultaOrdenServicioPersonal =>
                    (await _crmTools.ConsultarOrdenesPersonalesAsync(conversacionId, ct: ct)).Status,
                IntencionWhatsApp.ConsultaVehiculoPersonal =>
                    (await _crmTools.ConsultarVehiculosPersonalesAsync(conversacionId, ct)).Status,
                IntencionWhatsApp.ConsultaCitaPersonal =>
                    (await _crmTools.ConsultarCitasPersonalesAsync(conversacionId, ct: ct)).Status,
                _ =>
                    (await _crmTools.ConsultarPerfilPersonalAsync(conversacionId, ct)).Status
            };

            var bloqueada = chequeoAcceso != ServiceResultStatus.Success;
            var infoTaller = await _crmTools.ConsultarInformacionTallerAsync(ct);

            return new WhatsAppAiContextoAutorizado(
                IdentidadVerificada: false,
                ConsultaPersonalBloqueadaPorFaltaVerificacion: bloqueada,
                InformacionTaller: infoTaller,
                FaqsCoincidentes: Array.Empty<WhatsAppFaqAutorizada>(),
                ProductosPublicos: Array.Empty<WhatsAppProductoCatalogoResponse>(),
                ServiciosPublicos: Array.Empty<WhatsAppServicioCatalogoResponse>());
        }

        var infoPublica = await _crmTools.ConsultarInformacionTallerAsync(ct);
        IReadOnlyList<WhatsAppFaqAutorizada> faqs = Array.Empty<WhatsAppFaqAutorizada>();
        IReadOnlyList<WhatsAppProductoCatalogoResponse> productos = Array.Empty<WhatsAppProductoCatalogoResponse>();
        IReadOnlyList<WhatsAppServicioCatalogoResponse> servicios = Array.Empty<WhatsAppServicioCatalogoResponse>();

        if (clasificacion.Intencion == IntencionWhatsApp.ConsultaCatalogoProducto && aiSettings.AllowPublicCatalogTools)
        {
            var cat = await _crmTools.ConsultarCatalogoPublicoAsync(
                busqueda: clasificacion.TerminoBusquedaExtraido,
                incluirProductos: true,
                incluirServicios: false,
                limite: 5,
                ct: ct);
            productos = cat.Productos;
        }
        else if (clasificacion.Intencion == IntencionWhatsApp.ConsultaCatalogoServicio && aiSettings.AllowPublicCatalogTools)
        {
            var cat = await _crmTools.ConsultarCatalogoPublicoAsync(
                busqueda: clasificacion.TerminoBusquedaExtraido,
                incluirProductos: false,
                incluirServicios: true,
                limite: 5,
                ct: ct);
            servicios = cat.Servicios;
        }
        else if (clasificacion.Intencion is IntencionWhatsApp.ConsultaGeneralTaller or IntencionWhatsApp.PreguntaFrecuente
                 && aiSettings.AllowFaqTools)
        {
            faqs = await _crmTools.BuscarFaqsAutorizadasAsync(textoUsuario, limite: 3, ct: ct);
        }

        return new WhatsAppAiContextoAutorizado(
            IdentidadVerificada: false,
            ConsultaPersonalBloqueadaPorFaltaVerificacion: false,
            InformacionTaller: infoPublica,
            FaqsCoincidentes: faqs,
            ProductosPublicos: productos,
            ServiciosPublicos: servicios);
    }

    private async Task<IReadOnlyList<WhatsAppAiMensajeHistorial>> ObtenerHistorialSanitizadoAsync(
        Guid conversacionId,
        Guid excluirMensajeId,
        DateTime? fechaCambioModoHumano,
        int maxMensajes,
        CancellationToken ct)
    {
        if (maxMensajes <= 0)
        {
            return Array.Empty<WhatsAppAiMensajeHistorial>();
        }

        // F3-M02: excluir SIEMPRE mensajes escritos por agentes humanos (EnviadoPorUsuarioId != null),
        // mensajes entrantes del cliente enviados durante cualquier sesión humana y segmentos anteriores
        // al último retorno a modo Bot (soporta múltiples ciclos Bot -> Humano -> Bot -> Humano -> Bot).
        var (failClosedSinHistorial, corteUtc) = await CalcularCorteHistorialPostHumanoAsync(
            conversacionId,
            fechaCambioModoHumano,
            ct);

        if (failClosedSinHistorial)
        {
            return Array.Empty<WhatsAppAiMensajeHistorial>();
        }

        var query = _context.MensajesWhatsApp
            .AsNoTracking()
            .Where(m =>
                m.ConversacionId == conversacionId
                && m.Activo
                && m.Id != excluirMensajeId
                && m.ContenidoTexto != null
                && m.EnviadoPorUsuarioId == null);

        if (corteUtc.HasValue)
        {
            query = query.Where(m => m.FechaRecepcionUtc > corteUtc.Value);
        }

        var recientes = await query
            .OrderByDescending(m => m.FechaEventoUtc)
            .ThenByDescending(m => m.FechaRecepcionUtc)
            .Take(maxMensajes)
            .Select(m => new { m.Direccion, m.ContenidoTexto, m.FechaEventoUtc })
            .ToListAsync(ct);

        recientes.Reverse();
        return recientes
            .Select(m =>
            {
                var txt = m.ContenidoTexto!.Trim();
                if (txt.Length > 300) txt = txt[..300];
                var rol = m.Direccion == DireccionMensajeWhatsApp.Entrante ? "cliente" : "asistente";
                return new WhatsAppAiMensajeHistorial(rol, txt, m.FechaEventoUtc);
            })
            .ToList();
    }

    /// <summary>
    /// Determina el límite temporal efectivo a partir del cual el bot puede leer historial tras
    /// una o más sesiones de atención humana (F3-M02 / MED-02, sin migraciones):
    /// - Considera inicios de sesión humana tanto por <see cref="ConversacionWhatsApp.FechaCambioModoHumano"/>
    ///   como por eventos auditados (<c>WHATSAPP_CAMBIAR_MODO</c> -> Humano, <c>WHATSAPP_ASIGNAR_AGENTE</c> con agente,
    ///   <c>WHATSAPP_CAMBIAR_ESTADO</c> -> EnAtencion).
    /// - Considera cierres/retornos a bot auditados (<c>WHATSAPP_CAMBIAR_MODO</c> -> Bot,
    ///   <c>WHATSAPP_ASIGNAR_AGENTE</c> con AgenteNuevoId == null, <c>WHATSAPP_CAMBIAR_ESTADO</c> -> Abierta).
    /// - Considera también el último mensaje enviado por un agente humano (<c>EnviadoPorUsuarioId != null</c>).
    /// - Política fail-closed: si existió cualquier sesión humana pero no existe una marca auditable
    ///   de cierre/retorno posterior o igual a su inicio, devuelve historial vacío.
    /// </summary>
    private async Task<(bool FailClosedSinHistorial, DateTime? CorteUtc)> CalcularCorteHistorialPostHumanoAsync(
        Guid conversacionId,
        DateTime? fechaCambioModoHumano,
        CancellationToken ct)
    {
        var ultimoMensajeHumanoUtc = await _context.MensajesWhatsApp
            .AsNoTracking()
            .Where(m =>
                m.ConversacionId == conversacionId
                && m.Activo
                && m.EnviadoPorUsuarioId != null)
            .Select(m => (DateTime?)m.FechaRecepcionUtc)
            .MaxAsync(ct);

        var convIdStr = conversacionId.ToString();
        var eventos = await _context.EventosAuditoria
            .AsNoTracking()
            .Where(e =>
                e.Entidad == "ConversacionWhatsApp"
                && e.EntidadId == convIdStr
                && (e.Accion == "WHATSAPP_CAMBIAR_MODO"
                    || e.Accion == "WHATSAPP_ASIGNAR_AGENTE"
                    || e.Accion == "WHATSAPP_CAMBIAR_ESTADO"))
            .OrderByDescending(e => e.Fecha)
            .Select(e => new { e.Accion, e.Fecha, e.Detalle })
            .Take(50)
            .ToListAsync(ct);

        DateTime? ultimoInicioHumanoUtc = fechaCambioModoHumano;
        DateTime? ultimoFinHumanoUtc = null;

        static void ActualizarMax(ref DateTime? objetivo, DateTime candidato)
        {
            if (!objetivo.HasValue || candidato > objetivo.Value)
            {
                objetivo = candidato;
            }
        }

        foreach (var ev in eventos)
        {
            if (EsInicioSesionHumana(ev.Accion, ev.Detalle))
            {
                ActualizarMax(ref ultimoInicioHumanoUtc, ev.Fecha);
            }
            else if (EsFinSesionHumana(ev.Accion, ev.Detalle))
            {
                ActualizarMax(ref ultimoFinHumanoUtc, ev.Fecha);
            }
        }

        if (ultimoInicioHumanoUtc.HasValue)
        {
            // Fail-closed: si hubo sesión humana (por cambio de modo, escalamiento, asignación de agente o EnAtencion)
            // pero no consta su cierre/retorno a Bot posterior o igual al inicio, jamás exponer historial potencialmente privado.
            if (!ultimoFinHumanoUtc.HasValue || ultimoFinHumanoUtc.Value < ultimoInicioHumanoUtc.Value)
            {
                return (FailClosedSinHistorial: true, CorteUtc: null);
            }

            var corte = ultimoFinHumanoUtc.Value;
            if (ultimoInicioHumanoUtc.Value > corte)
            {
                corte = ultimoInicioHumanoUtc.Value;
            }
            if (ultimoMensajeHumanoUtc.HasValue && ultimoMensajeHumanoUtc.Value > corte)
            {
                corte = ultimoMensajeHumanoUtc.Value;
            }

            return (FailClosedSinHistorial: false, CorteUtc: corte);
        }

        DateTime? corteSinInicio = ultimoMensajeHumanoUtc;
        if (ultimoFinHumanoUtc.HasValue && (!corteSinInicio.HasValue || ultimoFinHumanoUtc.Value > corteSinInicio.Value))
        {
            corteSinInicio = ultimoFinHumanoUtc.Value;
        }

        return (FailClosedSinHistorial: false, CorteUtc: corteSinInicio);
    }

    private static bool EsInicioSesionHumana(string accion, string? detalleJson)
    {
        if (string.IsNullOrWhiteSpace(detalleJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(detalleJson);
            var root = doc.RootElement;

            return accion switch
            {
                "WHATSAPP_CAMBIAR_MODO" =>
                    root.TryGetProperty("ModoNuevo", out var modo)
                    && modo.ValueKind == JsonValueKind.String
                    && string.Equals(modo.GetString(), nameof(ModoAtencionWhatsApp.Humano), StringComparison.OrdinalIgnoreCase),

                "WHATSAPP_ASIGNAR_AGENTE" =>
                    root.TryGetProperty("AgenteNuevoId", out var agente)
                    && agente.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(agente.GetString()),

                "WHATSAPP_CAMBIAR_ESTADO" =>
                    root.TryGetProperty("EstadoNuevo", out var estado)
                    && estado.ValueKind == JsonValueKind.String
                    && string.Equals(estado.GetString(), nameof(EstadoConversacionWhatsApp.EnAtencion), StringComparison.OrdinalIgnoreCase),

                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool EsFinSesionHumana(string accion, string? detalleJson)
    {
        if (string.IsNullOrWhiteSpace(detalleJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(detalleJson);
            var root = doc.RootElement;

            return accion switch
            {
                "WHATSAPP_CAMBIAR_MODO" =>
                    root.TryGetProperty("ModoNuevo", out var modo)
                    && modo.ValueKind == JsonValueKind.String
                    && string.Equals(modo.GetString(), nameof(ModoAtencionWhatsApp.Bot), StringComparison.OrdinalIgnoreCase),

                "WHATSAPP_ASIGNAR_AGENTE" =>
                    !root.TryGetProperty("AgenteNuevoId", out var agente)
                    || agente.ValueKind == JsonValueKind.Null
                    || (agente.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(agente.GetString())),

                "WHATSAPP_CAMBIAR_ESTADO" =>
                    root.TryGetProperty("EstadoNuevo", out var estado)
                    && estado.ValueKind == JsonValueKind.String
                    && string.Equals(estado.GetString(), nameof(EstadoConversacionWhatsApp.Abierta), StringComparison.OrdinalIgnoreCase),

                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ResultadoOrquestacionWhatsApp Omitir(string motivo) => new(
        EstadoOrquestacionWhatsApp.Omitido,
        motivo,
        Intencion: null,
        MensajeRespuestaId: null,
        EscaladoAHumano: false,
        IdentidadVerificada: false);

    private void RegistrarTrazaSinPii(
        Guid conversacionId,
        Guid mensajeEntranteId,
        IntencionWhatsApp intencion,
        EstadoOrquestacionWhatsApp estado,
        bool escalado,
        string proveedor,
        long duracionMs)
    {
        // Registro estrictamente sin PII: sin texto del cliente, sin teléfono, sin documento ni secretos.
        _logger.LogInformation(
            "WhatsApp IA procesado. ConversacionId={ConversacionId}, MensajeEntranteId={MensajeEntranteId}, Intencion={Intencion}, Estado={Estado}, Escalado={Escalado}, Proveedor={Proveedor}, DuracionMs={DuracionMs}",
            conversacionId,
            mensajeEntranteId,
            intencion,
            estado,
            escalado,
            proveedor,
            duracionMs);
    }
}
