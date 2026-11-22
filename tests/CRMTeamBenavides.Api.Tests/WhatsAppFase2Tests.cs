using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.WhatsApp;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Data.Seed;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Suite automatizada obligatoria — Fase 2: Identificación de clientes, vinculación manual/reversible,
/// barrera de verificación de identidad, permisos granulares y adaptadores controlados del CRM para WhatsApp.
/// 100 % local y sintética: sin llamadas a Meta, sin tocar crm_teambenavides_qa ni producción.
/// </summary>
public class WhatsAppFase2Tests
{
    private sealed class AuditoriaEnMemoria : IAuditoriaService
    {
        public List<(Guid? UsuarioId, string Accion, string Entidad, string EntidadId, object? Detalle)> Eventos { get; } = new();

        public Task RegistrarEventoAsync(
            Guid? usuarioId,
            string accion,
            string entidad,
            string entidadId,
            object? detalle = null,
            CancellationToken ct = default)
        {
            Eventos.Add((usuarioId, accion, entidad, entidadId, detalle));
            return Task.CompletedTask;
        }
    }

    private sealed class VerificadorIdentidadSimulado(HashSet<Guid> verificadas) : IWhatsAppVerificadorIdentidad
    {
        public Task<bool> EstaIdentidadVerificadaAsync(Guid conversacionId, CancellationToken ct = default) =>
            Task.FromResult(verificadas.Contains(conversacionId));
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; set; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestAuthF2";
        public const string HeaderUserId = "X-Test-UserId";
        public const string HeaderRole = "X-Test-Role";
        public const string HeaderStalePermission = "X-Test-Permission";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderUserId, out var userIdHeader) ||
                string.IsNullOrWhiteSpace(userIdHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userIdHeader.ToString()),
                new("sub", userIdHeader.ToString())
            };

            if (Request.Headers.TryGetValue(HeaderRole, out var roleHeader) && !string.IsNullOrWhiteSpace(roleHeader))
            {
                claims.Add(new Claim(ClaimTypes.Role, roleHeader.ToString()));
            }

            if (Request.Headers.TryGetValue(HeaderStalePermission, out var permHeader) && !string.IsNullOrWhiteSpace(permHeader))
            {
                claims.Add(new Claim("permission", permHeader.ToString()));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private static ApplicationDbContext CrearContexto(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static StaticOptionsMonitor<WhatsAppSettings> CrearSettingsMonitor() =>
        new(new WhatsAppSettings { Enabled = false, Provider = WhatsAppSettings.ProviderMock, DefaultCountryCode = "51" });

    private static WhatsAppCrmConsultaService CrearCrmService(
        ApplicationDbContext db,
        IWhatsAppVerificadorIdentidad? verificador = null) =>
        new(
            db,
            CrearSettingsMonitor(),
            verificador ?? WhatsAppVerificadorIdentidadNoImplementado.Instancia,
            NullLogger<WhatsAppCrmConsultaService>.Instance);

    private static async Task<(WebApplication App, string DbName, AuditoriaEnMemoria Auditoria)> CrearServidorPruebaAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        var auditoria = new AuditoriaEnMemoria();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddDbContext<ApplicationDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName)
               .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        builder.Services.AddSingleton<IOptionsMonitor<WhatsAppSettings>>(CrearSettingsMonitor());
        builder.Services.AddSingleton<IAuditoriaService>(auditoria);
        builder.Services.AddSingleton<IWhatsAppAutoResponder, NoOpWhatsAppAutoResponder>();
        builder.Services.AddSingleton<IWhatsAppProvider>(new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance));
        builder.Services.AddSingleton<IWhatsAppVerificadorIdentidad, WhatsAppVerificadorIdentidadNoImplementado>();
        builder.Services.AddScoped<IWhatsAppClienteIdentificacionService, WhatsAppClienteIdentificacionService>();
        builder.Services.AddScoped<IWhatsAppCrmConsultaService, WhatsAppCrmConsultaService>();
        builder.Services.AddScoped<IWhatsAppOutboxService, WhatsAppOutboxService>();
        builder.Services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
        builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();

        builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permiso in PermisosDefinidos.Todos)
            {
                options.AddPolicy(permiso, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permiso)));
            }
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapWhatsAppEndpoints();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await RolSeeder.SeedAsync(db);
        }

        await app.StartAsync();
        return (app, dbName, auditoria);
    }

    // =========================================================================
    // 1. Normalización telefónica estricta (H-01) y enmascaramiento de series (M-02)
    // =========================================================================

    [Theory]
    // Formatos peruanos válidos
    [InlineData("987654321", "+51987654321", true)]
    [InlineData("0987654321", "+51987654321", true)]
    [InlineData("987-654-321", "+51987654321", true)]
    [InlineData("987 654 321", "+51987654321", true)]
    [InlineData("+51 987 654 321", "+51987654321", true)]
    [InlineData("+51 (987) 654-321", "+51987654321", true)]
    [InlineData("(51) 987-654-321", "+51987654321", true)]
    [InlineData("0051 987654321", "+51987654321", true)]
    [InlineData("51987654321", "+51987654321", true)]
    [InlineData("+51 0987654321", "+51987654321", true)]
    [InlineData("+51 (054) 223344", "+5154223344", true)]
    [InlineData("054223344", "+5154223344", true)]
    [InlineData("054-223344", "+5154223344", true)]
    [InlineData("012345678", "+5112345678", true)]
    [InlineData("01-2345678", "+5112345678", true)]
    // Prefijos internacionales explícitos válidos
    [InlineData("+56 9 8765 4321", "+56987654321", true)]
    [InlineData("001 555 123 4567", "+15551234567", true)]
    // Casos estrictamente RECHAZADOS (H-01)
    [InlineData("+51 98765432", null, false)]           // Celular peruano incompleto
    [InlineData("5198765432", null, false)]             // Celular peruano incompleto sin '+'
    [InlineData("54-223344", null, false)]              // Fijo peruano sin cero inicial
    [InlineData("1-2345678", null, false)]              // Fijo Lima sin cero inicial
    [InlineData("45678901", null, false)]               // DNI 8 dígitos
    [InlineData("20455687765", null, false)]            // RUC 11 dígitos
    [InlineData("Tel 1: 987654321", null, false)]       // Texto arbitrario + dígito
    [InlineData("REF 12345678", null, false)]           // Referencia con texto y dígitos
    [InlineData("987654321 ext 12", null, false)]       // Extensión / anexo
    [InlineData("+51 54 223344 anexo 12", null, false)] // Anexo en número con +51
    [InlineData("054-223344 ext. 102", null, false)]    // Extensión en fijo
    [InlineData("56987654321", null, false)]            // Extranjero sin prefijo '+' ni '00'
    [InlineData("+012345678", null, false)]             // Prefijo internacional inválido empezando en 0
    [InlineData("+123456", null, false)]                // Demasiado corto
    [InlineData("987654", null, false)]
    [InlineData("sin-telefono", null, false)]
    [InlineData("", null, false)]
    public void F2_01_TelefonoPeruanoYInternacional_NormalizacionEstrictaE164(
        string entrada,
        string? e164Esperado,
        bool validoEsperado)
    {
        var res = WhatsAppPhoneNormalizer.NormalizarE164(entrada);
        Assert.Equal(validoEsperado, res.EsValido);
        Assert.Equal(e164Esperado, res.TelefonoE164);
    }

    [Theory]
    [InlineData("51987654321", "+51987654321", true)]
    [InlineData("56987654321", "+56987654321", true)]
    [InlineData("15551234567", "+15551234567", true)]
    [InlineData("5198765432", null, false)]       // Celular peruano incompleto en Meta
    [InlineData("987654321 ext 12", null, false)] // Texto no permitido
    public void F2_01B_NormalizarRemitenteMeta_PreservaCompatibilidadInternacional(
        string waIdEntrada,
        string? e164Esperado,
        bool validoEsperado)
    {
        var res = WhatsAppPhoneNormalizer.NormalizarRemitenteMeta(waIdEntrada);
        Assert.Equal(validoEsperado, res.EsValido);
        Assert.Equal(e164Esperado, res.TelefonoE164);
    }

    [Fact]
    public void F2_02_MultiplesTelefonosEnCampo_ExtraeSoloNumerosValidosSinCorromperAnexos()
    {
        var extraidos = WhatsAppPhoneNormalizer.ExtraerTelefonosNormalizadosE164(
            "987-654-321 / +51 (054) 223344 ; 054-223344 anexo 10 , 45678901 | +56 9 8765 4321");

        Assert.Equal(3, extraidos.Count);
        Assert.Contains("+51987654321", extraidos);
        Assert.Contains("+5154223344", extraidos);
        Assert.Contains("+56987654321", extraidos);
    }

    [Theory]
    [InlineData("AB12", "****")]
    [InlineData("12345", "***45")]
    [InlineData("AB1234", "****34")]
    [InlineData("MH3RH25RXTK004019", "*************4019")]
    public void F2_02B_EnmascararSerie_NuncaDevuelveSerieCompletaInclusoEnSeisCaracteresOMenos(
        string serieOriginal,
        string esperado)
    {
        var enmascarada = WhatsAppPhoneNormalizer.EnmascararSerie(serieOriginal);
        Assert.Equal(esperado, enmascarada);
        Assert.NotEqual(serieOriginal, enmascarada);
    }

    // =========================================================================
    // 2. Identificación automática (prefiltro SQL + igualdad E.164 exacta)
    // =========================================================================

    [Fact]
    public async Task F2_03_IdentificacionAutomatica_ClienteUnivoco_IgnoraInactivosYCoincidenciasParciales()
    {
        using var db = CrearContexto();
        var clienteActivo = new Cliente
        {
            NombreCompleto = "Carlos Mendoza",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "45678901",
            Telefono = "987-654-321 / +51 (054) 221100",
            Activo = true
        };
        var clienteInactivo = new Cliente
        {
            NombreCompleto = "Registro Antiguo",
            Telefono = "+51 987654321",
            Activo = false
        };
        var clienteConAnexo = new Cliente
        {
            NombreCompleto = "Cliente Con Anexo",
            Telefono = "987654321 ext 12",
            Activo = true
        };
        var clienteParecido = new Cliente
        {
            NombreCompleto = "Otro Cliente",
            Telefono = "+51987654329",
            Activo = true
        };
        db.Clientes.AddRange(clienteActivo, clienteInactivo, clienteConAnexo, clienteParecido);
        await db.SaveChangesAsync();

        var svc = new WhatsAppClienteIdentificacionService(db, CrearSettingsMonitor(), NullLogger<WhatsAppClienteIdentificacionService>.Instance);
        var res = await svc.IdentificarPorTelefonoAsync("51987654321");

        Assert.Equal(EstadoIdentificacionClienteWhatsApp.IdentificadoUnivoco, res.Estado);
        Assert.True(res.EsVinculableAutomaticamente);
        Assert.Equal(clienteActivo.Id, res.ClienteId);
        Assert.Equal(1, res.TotalCandidatos);
        Assert.Equal("+51987654321", res.TelefonoE164Normalizado);
    }

    [Fact]
    public async Task F2_04_IdentificacionAutomatica_ClienteNoEncontrado_NoCreaClientes()
    {
        using var db = CrearContexto();
        db.Clientes.Add(new Cliente { NombreCompleto = "Otro", Telefono = "911222333", Activo = true });
        await db.SaveChangesAsync();

        var svc = new WhatsAppClienteIdentificacionService(db, CrearSettingsMonitor(), NullLogger<WhatsAppClienteIdentificacionService>.Instance);
        var res = await svc.IdentificarPorTelefonoAsync("+51999888777");

        Assert.Equal(EstadoIdentificacionClienteWhatsApp.NoEncontrado, res.Estado);
        Assert.Null(res.ClienteId);
        Assert.False(res.EsVinculableAutomaticamente);
        Assert.False(res.EsAmbiguo);
        Assert.Equal(1, await db.Clientes.CountAsync());
    }

    [Fact]
    public async Task F2_05_IdentificacionAutomatica_MultiplesClientesMismoTelefono_MarcaAmbiguoYSinVincular()
    {
        using var db = CrearContexto();
        db.Clientes.AddRange(
            new Cliente { NombreCompleto = "Juan Uno", Telefono = "987654321", Activo = true },
            new Cliente { NombreCompleto = "Juan Dos", Telefono = "+51 0987654321", Activo = true });
        await db.SaveChangesAsync();

        var svc = new WhatsAppClienteIdentificacionService(db, CrearSettingsMonitor(), NullLogger<WhatsAppClienteIdentificacionService>.Instance);
        var res = await svc.IdentificarPorTelefonoAsync("51987654321");

        Assert.Equal(EstadoIdentificacionClienteWhatsApp.Ambiguo, res.Estado);
        Assert.Null(res.ClienteId);
        Assert.True(res.EsAmbiguo);
        Assert.False(res.EsVinculableAutomaticamente);
        Assert.Equal(2, res.TotalCandidatos);
    }

    // =========================================================================
    // 3. Barrera técnica de verificación de identidad (H-02, M-01, B-03)
    // =========================================================================

    [Fact]
    public async Task F2_06_BarreraIdentidad_VinculacionTelefonicaNoHabilitaConsultasConversacionales()
    {
        using var db = CrearContexto();
        var cliente = new Cliente
        {
            NombreCompleto = "Ana Paredes",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "44556677",
            Telefono = "987654321 / 911222333",
            Activo = true
        };
        var veh = new Vehiculo { Cliente = cliente, Marca = "Yamaha", Modelo = "FZ-25", Placa = "4444-AA", Activo = true };
        var orden = new OrdenServicio { Cliente = cliente, Vehiculo = veh, NumeroOrden = "OS-000501", Estado = EstadoOrdenServicio.EnProceso, Total = 500m, Activo = true };
        var cita = new Cita { Cliente = cliente, Vehiculo = veh, NumeroCita = "CIT-000501", FechaHoraProgramada = DateTime.UtcNow.AddDays(1), Motivo = "Servicio", Estado = EstadoCita.Confirmada, Activo = true };
        // Conversación vinculada automáticamente por coincidencia telefónica (ClienteId != null)
        var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Cliente = cliente, Activo = true };

        db.Clientes.Add(cliente);
        db.Vehiculos.Add(veh);
        db.OrdenesServicio.Add(orden);
        db.Citas.Add(cita);
        db.ConversacionesWhatsApp.Add(conv);
        await db.SaveChangesAsync();

        // Con la implementación real de Fase 2 (WhatsAppVerificadorIdentidadNoImplementado),
        // toda consulta conversacional personal queda DENEGADA (Forbidden), incluso intentando forzar el enum en contexto.
        var crm = CrearCrmService(db);

        Assert.Equal(ServiceResultStatus.Forbidden, (await crm.ConsultarPerfilClienteAsync(conv.Id)).Status);
        Assert.Equal(ServiceResultStatus.Forbidden, (await crm.ConsultarVehiculosClienteAsync(conv.Id)).Status);
        Assert.Equal(ServiceResultStatus.Forbidden, (await crm.ConsultarOrdenesServicioClienteAsync(conv.Id)).Status);
        Assert.Equal(ServiceResultStatus.Forbidden, (await crm.ConsultarEstadoOrdenServicioAsync(conv.Id, ordenServicioId: orden.Id)).Status);
        Assert.Equal(ServiceResultStatus.Forbidden, (await crm.ConsultarCitasClienteAsync(conv.Id)).Status);

        // Incluso si un llamador pasa VerificadaPorMecanismoAprobado sin verificación real en servidor, sigue denegado
        var contextoForzado = new WhatsAppContextoConsultaCrm(
            WhatsAppModoConsultaCrm.ConversacionalCliente,
            PermitirDocumentoCompleto: true,
            EstadoVerificacionIdentidad: EstadoVerificacionIdentidadWhatsApp.VerificadaPorMecanismoAprobado);

        Assert.Equal(ServiceResultStatus.Forbidden, (await crm.ConsultarPerfilClienteAsync(conv.Id, contextoForzado)).Status);

        // En modo AdministrativoInterno, sí funciona para personal autorizado
        var perfilAdmin = await crm.ConsultarPerfilClienteAsync(conv.Id, WhatsAppContextoConsultaCrm.Administrativo());
        Assert.True(perfilAdmin.IsSuccess);
        Assert.Equal(2, perfilAdmin.Data!.TelefonosE164.Count);

        // Simulando un futuro verificador aprobado en Fase 3: modo conversacional NUNCA expone teléfonos secundarios (M-01) ni documento sin máscara
        var crmConVerificadorFuturo = CrearCrmService(db, new VerificadorIdentidadSimulado([conv.Id]));
        var perfilConvVerif = await crmConVerificadorFuturo.ConsultarPerfilClienteAsync(conv.Id, contextoForzado);
        Assert.True(perfilConvVerif.IsSuccess);
        Assert.Equal("****6677", perfilConvVerif.Data!.DocumentoVisible);
        var telUnico = Assert.Single(perfilConvVerif.Data.TelefonosE164);
        Assert.Equal("+51987654321", telUnico);
    }

    [Fact]
    public async Task F2_06B_AsociacionAmbiguaConClienteIdInconsistente_BloqueaConsultasDefensivamente()
    {
        using var db = CrearContexto();
        var cliente = new Cliente { NombreCompleto = "Cliente Ambiguo", Telefono = "987654321", Activo = true };
        var conv = new ConversacionWhatsApp
        {
            TelefonoE164 = "+51987654321",
            Cliente = cliente,
            AsociacionClienteAmbigua = true, // Estado inconsistente defensivo (B-03)
            Activo = true
        };
        db.Clientes.Add(cliente);
        db.ConversacionesWhatsApp.Add(conv);
        await db.SaveChangesAsync();

        var crm = CrearCrmService(db);
        var ctxAdmin = WhatsAppContextoConsultaCrm.Administrativo();

        Assert.Equal(ServiceResultStatus.Conflict, (await crm.ConsultarPerfilClienteAsync(conv.Id, ctxAdmin)).Status);
        Assert.Equal(ServiceResultStatus.Conflict, (await crm.ConsultarVehiculosClienteAsync(conv.Id, ctxAdmin)).Status);
        Assert.Equal(ServiceResultStatus.Conflict, (await crm.ConsultarOrdenesServicioClienteAsync(conv.Id, contexto: ctxAdmin)).Status);
    }

    [Fact]
    public async Task F2_06C_ConversacionNoVinculada_RechazaConsultasCrmSinExponerDatos()
    {
        using var db = CrearContexto();
        var conv = new ConversacionWhatsApp { TelefonoE164 = "+51955444333", ClienteId = null, Activo = true };
        db.ConversacionesWhatsApp.Add(conv);
        await db.SaveChangesAsync();

        var crm = CrearCrmService(db);
        var ctxAdmin = WhatsAppContextoConsultaCrm.Administrativo();

        Assert.Equal(ServiceResultStatus.ValidationError, (await crm.ConsultarPerfilClienteAsync(conv.Id, ctxAdmin)).Status);
        Assert.Equal(ServiceResultStatus.ValidationError, (await crm.ConsultarVehiculosClienteAsync(conv.Id, ctxAdmin)).Status);
        Assert.Equal(ServiceResultStatus.ValidationError, (await crm.ConsultarOrdenesServicioClienteAsync(conv.Id, contexto: ctxAdmin)).Status);
        Assert.Equal(ServiceResultStatus.ValidationError, (await crm.ConsultarEstadoOrdenServicioAsync(conv.Id, numeroOrden: "OS-000001", contexto: ctxAdmin)).Status);
        Assert.Equal(ServiceResultStatus.ValidationError, (await crm.ConsultarCitasClienteAsync(conv.Id, contexto: ctxAdmin)).Status);
    }

    // =========================================================================
    // 4. Ciclo de vida: vinculación, desvinculación, cliente inactivo y cambio de teléfono (M-03, M-04, M-05)
    // =========================================================================

    [Fact]
    public async Task F2_07_VinculacionDesvinculacionAmbigua_SinCambiosSinAuditoria_YClienteDesactivado()
    {
        using var db = CrearContexto();
        var cli1 = new Cliente
        {
            NombreCompleto = "María Torres",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "71234567",
            Telefono = "955-111-222",
            Activo = true
        };
        var cli2MismoTel = new Cliente
        {
            NombreCompleto = "Pedro Torres",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "79876543",
            Telefono = "+51 955111222",
            Activo = true
        };
        var cliInactivo = new Cliente { NombreCompleto = "Cliente Inactivo", Telefono = "900000001", Activo = false };
        var conv = new ConversacionWhatsApp
        {
            TelefonoE164 = "+51955111222",
            ClienteId = null,
            AsociacionClienteAmbigua = true,
            Activo = true
        };
        db.Clientes.AddRange(cli1, cli2MismoTel, cliInactivo);
        db.ConversacionesWhatsApp.Add(conv);
        await db.SaveChangesAsync();

        var settingsMonitor = CrearSettingsMonitor();
        var auditoria = new AuditoriaEnMemoria();
        var identSvc = new WhatsAppClienteIdentificacionService(db, settingsMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
        var outboxSvc = new WhatsAppOutboxService(db, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), settingsMonitor, NullLogger<WhatsAppOutboxService>.Instance);
        var svc = new WhatsAppService(db, outboxSvc, identSvc, auditoria, settingsMonitor);
        var staffId = Guid.NewGuid();

        // 1. Búsqueda rechaza criterio vacío o < 3 caracteres (H-03.C)
        Assert.Equal(ServiceResultStatus.ValidationError, (await svc.BuscarClientesParaVincularAsync("")).Status);
        Assert.Equal(ServiceResultStatus.ValidationError, (await svc.BuscarClientesParaVincularAsync("ab")).Status);

        // Búsqueda SQL por teléfono normalizado retorna ambos activos paginados con documento y teléfono enmascarados
        var busq = await svc.BuscarClientesParaVincularAsync("+51 955111222", pagina: 1, tamanoPagina: 10);
        Assert.True(busq.IsSuccess);
        Assert.Equal(2, busq.Data!.TotalRegistros);
        Assert.All(busq.Data.Items, c =>
        {
            Assert.StartsWith("****", c.DocumentoEnmascarado);
            Assert.Contains("*", c.TelefonoEnmascarado);
        });

        // 2. Desvincular cuando ya no hay cliente vinculado -> NO genera evento de auditoría (M-04)
        var desvSinCambio = await svc.DesvincularClienteAsync(conv.Id, staffId);
        Assert.True(desvSinCambio.IsSuccess);
        Assert.Empty(auditoria.Eventos);

        // 3. Rechaza asociación a cliente inactivo o inexistente
        Assert.Equal(ServiceResultStatus.ValidationError, (await svc.VincularClienteAsync(conv.Id, new WhatsAppVincularClienteRequest(cliInactivo.Id), staffId)).Status);
        Assert.Equal(ServiceResultStatus.ValidationError, (await svc.VincularClienteAsync(conv.Id, new WhatsAppVincularClienteRequest(Guid.NewGuid()), staffId)).Status);

        // 4. Vincular manualmente a cli1 limpia ambigüedad y enmascara documento si incluirDocumentoCompleto=false
        var vinc = await svc.VincularClienteAsync(conv.Id, new WhatsAppVincularClienteRequest(cli1.Id), staffId, incluirDocumentoCompleto: false);
        Assert.True(vinc.IsSuccess);
        Assert.Equal(cli1.Id, vinc.Data!.ClienteId);
        Assert.Equal("****4567", vinc.Data.ClienteDocumento);
        Assert.False(vinc.Data.AsociacionClienteAmbigua);
        Assert.Single(auditoria.Eventos, e => e.Accion == "WHATSAPP_VINCULAR_CLIENTE");

        // Re-identificar mientras está vinculado a cliente activo devuelve Conflict
        Assert.Equal(ServiceResultStatus.Conflict, (await svc.ReintentarIdentificacionClienteAsync(conv.Id, staffId)).Status);

        // 5. Desvincular conversación originalmente ambigua recalcula AsociacionClienteAmbigua = true (M-04)
        var desv = await svc.DesvincularClienteAsync(conv.Id, staffId);
        Assert.True(desv.IsSuccess);
        Assert.Null(desv.Data!.ClienteId);
        Assert.True(desv.Data.AsociacionClienteAmbigua);
        Assert.Single(auditoria.Eventos, e => e.Accion == "WHATSAPP_DESVINCULAR_CLIENTE");

        // 6. Volver a vincular a cli1 y simular cambio de teléfono del cliente (número reciclado/cambiado) -> M-03
        await svc.VincularClienteAsync(conv.Id, new WhatsAppVincularClienteRequest(cli1.Id), staffId);
        cli1.Telefono = "999000111";
        await db.SaveChangesAsync();

        var vincCambioTel = await svc.ObtenerVinculacionClienteAsync(conv.Id, incluirDocumentoCompleto: false);
        Assert.True(vincCambioTel.IsSuccess);
        Assert.True(vincCambioTel.Data!.EstaVinculada);
        Assert.Equal(false, vincCambioTel.Data.TelefonoCoincideConCliente);
        Assert.True(vincCambioTel.Data.RequiereRevisionVinculacion);

        // 7. Desactivar cli1 después de la vinculación -> M-03:
        //    - ObtenerVinculacion reporta ClienteVinculadoInactivo=true y EstaVinculada=false
        //    - ObtenerConversacion oculta nombre y documento del cliente inactivo
        //    - ReintentarIdentificacion reemplaza el vínculo inactivo y vincula unívocamente a cli2MismoTel
        cli1.Activo = false;
        await db.SaveChangesAsync();

        var vincInactivo = await svc.ObtenerVinculacionClienteAsync(conv.Id);
        Assert.False(vincInactivo.Data!.EstaVinculada);
        Assert.True(vincInactivo.Data.ClienteVinculadoInactivo);
        Assert.True(vincInactivo.Data.RequiereRevisionVinculacion);

        var detalleInactivo = await svc.ObtenerConversacionAsync(conv.Id, incluirDocumentoCompleto: true);
        Assert.Null(detalleInactivo.Data!.ClienteId);
        Assert.Null(detalleInactivo.Data.ClienteNombre);
        Assert.Null(detalleInactivo.Data.ClienteDocumento);

        var reident = await svc.ReintentarIdentificacionClienteAsync(conv.Id, staffId);
        Assert.True(reident.IsSuccess);
        Assert.True(reident.Data!.VinculadaAutomaticamente);
        Assert.Equal(cli2MismoTel.Id, reident.Data.ClienteId);
        Assert.Contains(auditoria.Eventos, e => e.Accion == "WHATSAPP_REIDENTIFICAR_CLIENTE");
    }

    // =========================================================================
    // 5. Aislamiento cruzado y validación consistente ordenServicioId + numeroOrden (B-02)
    // =========================================================================

    [Fact]
    public async Task F2_08_AislamientoCruzadoYCombinacionOrdenIdNumeroOrden()
    {
        using var db = CrearContexto();
        var clienteA = new Cliente { NombreCompleto = "Cliente A", Telefono = "987111111", Activo = true };
        var clienteB = new Cliente { NombreCompleto = "Cliente B", Telefono = "987222222", Activo = true };
        var vehA = new Vehiculo { Cliente = clienteA, Marca = "Yamaha", Modelo = "MT-03", Placa = "1111-AA", Activo = true };
        var vehB = new Vehiculo { Cliente = clienteB, Marca = "Yamaha", Modelo = "R3", Placa = "9999-BB", Activo = true };
        var ordenA = new OrdenServicio { Cliente = clienteA, Vehiculo = vehA, NumeroOrden = "OS-000100", Estado = EstadoOrdenServicio.EnProceso, Total = 300m, Activo = true };
        var ordenB = new OrdenServicio { Cliente = clienteB, Vehiculo = vehB, NumeroOrden = "OS-000777", Estado = EstadoOrdenServicio.EnProceso, Diagnostico = "Confidencial B", Total = 950m, Activo = true };
        var convA = new ConversacionWhatsApp { TelefonoE164 = "+51987111111", Cliente = clienteA, Activo = true };

        db.Clientes.AddRange(clienteA, clienteB);
        db.Vehiculos.AddRange(vehA, vehB);
        db.OrdenesServicio.AddRange(ordenA, ordenB);
        db.ConversacionesWhatsApp.Add(convA);
        await db.SaveChangesAsync();

        var crm = CrearCrmService(db);
        var ctxAdmin = WhatsAppContextoConsultaCrm.Administrativo();

        // Conversación de A intenta consultar la orden de B por GUID y por NumeroOrden
        Assert.Equal(ServiceResultStatus.NotFound, (await crm.ConsultarEstadoOrdenServicioAsync(convA.Id, ordenServicioId: ordenB.Id, contexto: ctxAdmin)).Status);
        Assert.Equal(ServiceResultStatus.NotFound, (await crm.ConsultarEstadoOrdenServicioAsync(convA.Id, numeroOrden: "OS-000777", contexto: ctxAdmin)).Status);

        // Si envía ordenServicioId y numeroOrden juntos y coinciden -> OK
        var ambosCoinciden = await crm.ConsultarEstadoOrdenServicioAsync(convA.Id, ordenServicioId: ordenA.Id, numeroOrden: "os-000100", contexto: ctxAdmin);
        Assert.True(ambosCoinciden.IsSuccess);

        // Si envía ordenServicioId de A junto con numeroOrden diferente -> ValidationError (no se ignora silenciosamente)
        var ambosInconsistentes = await crm.ConsultarEstadoOrdenServicioAsync(convA.Id, ordenServicioId: ordenA.Id, numeroOrden: "OS-000777", contexto: ctxAdmin);
        Assert.Equal(ServiceResultStatus.ValidationError, ambosInconsistentes.Status);
    }

    // =========================================================================
    // 6. Consultas CRM administrativas (vehículos, órdenes, estados reales y citas)
    // =========================================================================

    [Fact]
    public async Task F2_09_ConsultasCrm_MultiplesVehiculosOrdenesEstadosRealesYCitas()
    {
        using var db = CrearContexto();
        var cliente = new Cliente
        {
            NombreCompleto = "Luis Quispe",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "40123456",
            Telefono = "987654321",
            Email = "luis@example.com",
            Activo = true
        };
        var moto1 = new Vehiculo
        {
            Cliente = cliente,
            TipoUnidad = TipoUnidad.Motocicleta,
            Marca = "Yamaha",
            Modelo = "MT-03",
            Anio = 2025,
            Placa = "1234-AB",
            NumeroSerieVIN = "MH3RH25RXTK004019",
            TipoMedidor = TipoMedidor.Kilometraje,
            Kilometraje = 8500,
            Activo = true
        };
        var motorOm = new Vehiculo
        {
            Cliente = cliente,
            TipoUnidad = TipoUnidad.MotorFueraDeBorda,
            Marca = "Yamaha",
            Modelo = "F115BETX",
            TipoMedidor = TipoMedidor.Horas,
            HorasUso = 120m,
            NumeroSerieVIN = "AB1234", // 6 caracteres -> debe enmascararse
            Activo = true
        };
        var motoInactiva = new Vehiculo { Cliente = cliente, Marca = "Yamaha", Modelo = "Vendida", Activo = false };

        var ordenActiva = new OrdenServicio
        {
            Cliente = cliente,
            Vehiculo = moto1,
            NumeroOrden = "OS-000101",
            Estado = EstadoOrdenServicio.Lista,
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            TiposAtencion = [TipoAtencion.MantenimientoPreventivo, TipoAtencion.MantenimientoCorrectivo],
            FechaIngreso = new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc),
            FechaEstimadaEntrega = new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc),
            Diagnostico = "Cambio de aceite y pastillas",
            Total = 320m,
            Activo = true,
            Detalles =
            [
                new DetalleServicio { TipoItem = TipoItemServicio.ManoDeObra, Descripcion = "Mantenimiento 10k", Cantidad = 1, Activo = true },
                new DetalleServicio { TipoItem = TipoItemServicio.Repuesto, Descripcion = "Aceite Yamalube", Cantidad = 2, Activo = true }
            ]
        };
        var ordenEntregada = new OrdenServicio
        {
            Cliente = cliente,
            Vehiculo = moto1,
            NumeroOrden = "OS-000090",
            Estado = EstadoOrdenServicio.Entregada,
            FechaIngreso = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc),
            FechaSalida = new DateTime(2026, 10, 11, 18, 0, 0, DateTimeKind.Utc),
            Solucion = "Cambio de kit de arrastre",
            Total = 450m,
            Activo = true
        };
        var citaConfirmada = new Cita
        {
            Cliente = cliente,
            Vehiculo = moto1,
            NumeroCita = "CIT-000010",
            FechaHoraProgramada = new DateTime(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc),
            DuracionMinutos = 60,
            Motivo = "Revisión de frenos",
            Estado = EstadoCita.Confirmada,
            Activo = true
        };
        var citaCancelada = new Cita
        {
            Cliente = cliente,
            NumeroCita = "CIT-000005",
            FechaHoraProgramada = new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc),
            Motivo = "Cita cancelada",
            Estado = EstadoCita.Cancelada,
            Activo = true
        };

        var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Cliente = cliente, Activo = true };

        db.Clientes.Add(cliente);
        db.Vehiculos.AddRange(moto1, motorOm, motoInactiva);
        db.OrdenesServicio.AddRange(ordenActiva, ordenEntregada);
        db.Citas.AddRange(citaConfirmada, citaCancelada);
        db.ConversacionesWhatsApp.Add(conv);
        await db.SaveChangesAsync();

        var crm = CrearCrmService(db);
        var ctxAdminMask = WhatsAppContextoConsultaCrm.Administrativo(permitirDocumentoCompleto: false);

        var perfil = await crm.ConsultarPerfilClienteAsync(conv.Id, ctxAdminMask);
        Assert.True(perfil.IsSuccess);
        Assert.Equal("****3456", perfil.Data!.DocumentoVisible);
        Assert.Equal(2, perfil.Data.CantidadVehiculosActivos);
        Assert.Equal(1, perfil.Data.CantidadOrdenesActivas);
        Assert.Equal(1, perfil.Data.CantidadCitasActivas);

        var vehs = await crm.ConsultarVehiculosClienteAsync(conv.Id, ctxAdminMask);
        Assert.Equal(2, vehs.Data!.Count);
        Assert.Contains(vehs.Data, v => v.Placa == "1234-AB" && v.SerieIdentificacionParcial == "*************4019");
        Assert.Contains(vehs.Data, v => v.TipoUnidad == TipoUnidad.MotorFueraDeBorda && v.SerieIdentificacionParcial == "****34");

        var todasOrdenes = await crm.ConsultarOrdenesServicioClienteAsync(conv.Id, incluirFinalizadasYCanceladas: true, contexto: ctxAdminMask);
        Assert.Equal(2, todasOrdenes.Data!.Count);

        var soloActivas = await crm.ConsultarOrdenesServicioClienteAsync(conv.Id, incluirFinalizadasYCanceladas: false, contexto: ctxAdminMask);
        Assert.Single(soloActivas.Data!);
        Assert.Equal("OS-000101", soloActivas.Data![0].NumeroOrden);

        var citasActivas = await crm.ConsultarCitasClienteAsync(conv.Id, soloActivas: true, contexto: ctxAdminMask);
        Assert.Single(citasActivas.Data!);
        Assert.Equal("CIT-000010", citasActivas.Data![0].NumeroCita);
    }

    // =========================================================================
    // 7. Catálogo y cálculo de precios oficial con IGV, USD, cero y filtros (H-04, B-01, B-04)
    // =========================================================================

    [Fact]
    public async Task F2_10_CatalogoProductosYServicios_IgvUsdPrecioCeroInactivosYStockSeparado()
    {
        // Verificación estructural: el contrato de catálogo jamás expone Costo
        Assert.DoesNotContain(
            typeof(WhatsAppProductoCatalogoResponse).GetProperties(),
            p => p.Name.Contains("Costo", StringComparison.OrdinalIgnoreCase));

        using var db = CrearContexto();
        var config = new ConfiguracionEmpresa
        {
            RazonSocial = "Team Benavides S.R.L.",
            Ruc = "20455687765",
            PorcentajeIgv = 18m,
            TipoCambioVigente = 3.72m
        };
        db.ConfiguracionesEmpresa.Add(config);

        var catActiva = new CategoriaProducto { Nombre = "Aceites y Filtros", Activo = true };
        var catInactiva = new CategoriaProducto { Nombre = "Descontinuados", Activo = false };
        db.CategoriasProducto.AddRange(catActiva, catInactiva);

        db.Productos.AddRange(
            new Producto { Categoria = catActiva, Codigo = "PEN-CON-IGV", Nombre = "Aceite Con IGV", PrecioVenta = 59m, MonedaPrecio = MonedaPrecio.PEN, PrecioIncluyeIgv = true, Costo = 30m, StockActual = 10, Activo = true },
            new Producto { Categoria = catActiva, Codigo = "PEN-SIN-IGV", Nombre = "Bujía Sin IGV", PrecioVenta = 100m, MonedaPrecio = MonedaPrecio.PEN, PrecioIncluyeIgv = false, Costo = 50m, StockActual = 5, Activo = true },
            new Producto { Categoria = catActiva, Codigo = "PEN-CERO", Nombre = "Producto Sin Precio", PrecioVenta = 0m, MonedaPrecio = MonedaPrecio.PEN, PrecioIncluyeIgv = true, Costo = 15m, StockActual = 4, Activo = true },
            new Producto { Categoria = catActiva, Codigo = "USD-CON-IGV", Nombre = "Filtro USD Con IGV", PrecioVenta = 20m, MonedaPrecio = MonedaPrecio.USD, PrecioIncluyeIgv = true, Costo = 40m, StockActual = 3, Activo = true },
            new Producto { Categoria = catActiva, Codigo = "USD-SIN-IGV", Nombre = "Kit USD Sin IGV", PrecioVenta = 20m, MonedaPrecio = MonedaPrecio.USD, PrecioIncluyeIgv = false, Costo = 40m, StockActual = 2, Activo = true },
            new Producto { Categoria = catActiva, Codigo = "PROD-INACT", Nombre = "Producto Inactivo", PrecioVenta = 80m, MonedaPrecio = MonedaPrecio.PEN, PrecioIncluyeIgv = true, Costo = 20m, StockActual = 8, Activo = false },
            new Producto { Categoria = catInactiva, Codigo = "CAT-INACT", Nombre = "Producto Categoría Inactiva", PrecioVenta = 80m, MonedaPrecio = MonedaPrecio.PEN, PrecioIncluyeIgv = true, Costo = 20m, StockActual = 8, Activo = true });

        db.Servicios.AddRange(
            new Servicio { Nombre = "Serv Gravado Sin IGV", PrecioSugerido = 100m, PrecioIncluyeIgv = false, TipoAfectacionIgv = TipoAfectacionIgv.Gravado, Activo = true },
            new Servicio { Nombre = "Serv Gravado Con IGV", PrecioSugerido = 118m, PrecioIncluyeIgv = true, TipoAfectacionIgv = TipoAfectacionIgv.Gravado, Activo = true },
            new Servicio { Nombre = "Serv Exonerado", PrecioSugerido = 100m, PrecioIncluyeIgv = false, TipoAfectacionIgv = TipoAfectacionIgv.Exonerado, Activo = true },
            new Servicio { Nombre = "Serv Inafecto", PrecioSugerido = 100m, PrecioIncluyeIgv = false, TipoAfectacionIgv = TipoAfectacionIgv.Inafecto, Activo = true },
            new Servicio { Nombre = "Serv Precio Cero", PrecioSugerido = 0m, PrecioIncluyeIgv = false, TipoAfectacionIgv = TipoAfectacionIgv.Gravado, Activo = true },
            new Servicio { Nombre = "Serv Inactivo", PrecioSugerido = 90m, PrecioIncluyeIgv = true, TipoAfectacionIgv = TipoAfectacionIgv.Gravado, Activo = false });

        await db.SaveChangesAsync();

        var crm = CrearCrmService(db);

        // 1. Con tipo de cambio vigente en modo AdministrativoInterno
        var catAdmin = await crm.ConsultarProductosYServiciosAsync(modo: WhatsAppModoConsultaCrm.AdministrativoInterno);
        Assert.Equal(5, catAdmin.Productos.Count); // Excluye PROD-INACT y CAT-INACT (B-04)
        Assert.DoesNotContain(catAdmin.Productos, p => p.Codigo is "PROD-INACT" or "CAT-INACT");

        var penConIgv = catAdmin.Productos.Single(p => p.Codigo == "PEN-CON-IGV");
        Assert.True(penConIgv.PrecioDisponible);
        Assert.Equal(50m, penConIgv.PrecioBaseSinIgvSoles);
        Assert.Equal(59m, penConIgv.PrecioReferencialConIgvSoles);
        Assert.Equal(10, penConIgv.StockDisponible);

        var penSinIgv = catAdmin.Productos.Single(p => p.Codigo == "PEN-SIN-IGV");
        Assert.True(penSinIgv.PrecioDisponible);
        Assert.Equal(100m, penSinIgv.PrecioBaseSinIgvSoles);
        Assert.Equal(118m, penSinIgv.PrecioReferencialConIgvSoles); // Nunca muestra 100 como precio final con IGV

        var penCero = catAdmin.Productos.Single(p => p.Codigo == "PEN-CERO");
        Assert.False(penCero.PrecioDisponible);
        Assert.Null(penCero.PrecioBaseSinIgvSoles);
        Assert.Null(penCero.PrecioReferencialConIgvSoles);

        // USD: 20 * 3.72 = 74.4 -> Math.Ceiling = 75
        var usdConIgv = catAdmin.Productos.Single(p => p.Codigo == "USD-CON-IGV");
        Assert.True(usdConIgv.PrecioDisponible);
        Assert.Equal(75m, usdConIgv.PrecioReferencialConIgvSoles);

        var usdSinIgv = catAdmin.Productos.Single(p => p.Codigo == "USD-SIN-IGV");
        Assert.True(usdSinIgv.PrecioDisponible);
        Assert.Equal(75m, usdSinIgv.PrecioBaseSinIgvSoles);
        Assert.Equal(88.50m, usdSinIgv.PrecioReferencialConIgvSoles);

        // Servicios: excluye inactivo, respeta afectación y precio cero
        Assert.Equal(5, catAdmin.Servicios.Count);
        Assert.DoesNotContain(catAdmin.Servicios, s => s.Nombre == "Serv Inactivo");
        Assert.Equal(118m, catAdmin.Servicios.Single(s => s.Nombre == "Serv Gravado Sin IGV").PrecioReferencialConIgv);
        Assert.Equal(118m, catAdmin.Servicios.Single(s => s.Nombre == "Serv Gravado Con IGV").PrecioReferencialConIgv);
        Assert.Equal(100m, catAdmin.Servicios.Single(s => s.Nombre == "Serv Exonerado").PrecioReferencialConIgv);
        Assert.Equal(100m, catAdmin.Servicios.Single(s => s.Nombre == "Serv Inafecto").PrecioReferencialConIgv);
        var servCero = catAdmin.Servicios.Single(s => s.Nombre == "Serv Precio Cero");
        Assert.False(servCero.PrecioDisponible);
        Assert.Null(servCero.PrecioSugeridoSoles);
        Assert.Null(servCero.PrecioReferencialConIgv);

        // 2. Modo ConversacionalCliente oculta stock numérico exacto (B-01)
        var catConv = await crm.ConsultarProductosYServiciosAsync(modo: WhatsAppModoConsultaCrm.ConversacionalCliente);
        Assert.All(catConv.Productos, p => Assert.Null(p.StockDisponible));

        // 3. Sin tipo de cambio vigente -> productos USD pasan a PrecioDisponible = false
        config.TipoCambioVigente = null;
        await db.SaveChangesAsync();

        var catSinTc = await crm.ConsultarProductosYServiciosAsync(modo: WhatsAppModoConsultaCrm.AdministrativoInterno);
        Assert.False(catSinTc.Productos.Single(p => p.Codigo == "USD-CON-IGV").PrecioDisponible);
        Assert.Null(catSinTc.Productos.Single(p => p.Codigo == "USD-CON-IGV").PrecioReferencialConIgvSoles);
        Assert.False(catSinTc.Productos.Single(p => p.Codigo == "USD-SIN-IGV").PrecioDisponible);
    }

    // =========================================================================
    // 8. Seguridad, permisos granulares, roles personalizados y revocación (H-03, M-06)
    // =========================================================================

    [Fact]
    public async Task F2_11_EndpointsFase2_PermisosGranulares_RolesPersonalizados_DocumentoEnmascaradoYRevocacion()
    {
        var (app, dbName, _) = await CrearServidorPruebaAsync();
        await using (app)
        {
            var tecnicoId = Guid.NewGuid();
            var clienteUserId = Guid.NewGuid();
            var soloWaVerId = Guid.NewGuid();
            var soloWaGestId = Guid.NewGuid();
            var asesorCustomId = Guid.NewGuid();
            Guid convId;
            Guid ordenId;
            Guid rolAsesorCustomId;
            Guid permClientesVerId;

            using (var db = CrearContexto(dbName))
            {
                var rolTec = await db.Roles.SingleAsync(r => r.Nombre == RolesDefinidos.Tecnico);
                var rolCli = await db.Roles.SingleAsync(r => r.Nombre == RolesDefinidos.Cliente);

                var perms = await db.Permisos.ToDictionaryAsync(p => p.Codigo, p => p.Id);
                permClientesVerId = perms[PermisosDefinidos.ClientesVer];

                // Roles personalizados de staff (M-06)
                var rolSoloWaVer = new Rol { Nombre = "MonitorWhatsApp", Descripcion = "Solo whatsapp.ver", Activo = true };
                var rolSoloWaGest = new Rol { Nombre = "OperadorWhatsAppSinClientes", Descripcion = "whatsapp.ver + whatsapp.gestionar sin clientes.ver", Activo = true };
                var rolAsesorCustom = new Rol { Nombre = "AsesorWhatsAppIntegral", Descripcion = "Rol custom completo", Activo = true };
                db.Roles.AddRange(rolSoloWaVer, rolSoloWaGest, rolAsesorCustom);
                await db.SaveChangesAsync();
                rolAsesorCustomId = rolAsesorCustom.Id;

                db.RolPermisos.AddRange(
                    new RolPermiso { RolId = rolSoloWaVer.Id, PermisoId = perms[PermisosDefinidos.WhatsAppVer] },

                    new RolPermiso { RolId = rolSoloWaGest.Id, PermisoId = perms[PermisosDefinidos.WhatsAppVer] },
                    new RolPermiso { RolId = rolSoloWaGest.Id, PermisoId = perms[PermisosDefinidos.WhatsAppGestionar] },

                    new RolPermiso { RolId = rolAsesorCustom.Id, PermisoId = perms[PermisosDefinidos.WhatsAppVer] },
                    new RolPermiso { RolId = rolAsesorCustom.Id, PermisoId = perms[PermisosDefinidos.WhatsAppGestionar] },
                    new RolPermiso { RolId = rolAsesorCustom.Id, PermisoId = perms[PermisosDefinidos.ClientesVer] },
                    new RolPermiso { RolId = rolAsesorCustom.Id, PermisoId = perms[PermisosDefinidos.UnidadesVer] },
                    new RolPermiso { RolId = rolAsesorCustom.Id, PermisoId = perms[PermisosDefinidos.OrdenesVerTodas] },
                    new RolPermiso { RolId = rolAsesorCustom.Id, PermisoId = perms[PermisosDefinidos.CitasVer] },
                    new RolPermiso { RolId = rolAsesorCustom.Id, PermisoId = perms[PermisosDefinidos.InventarioVer] },
                    new RolPermiso { RolId = rolAsesorCustom.Id, PermisoId = perms[PermisosDefinidos.ServiciosVer] });

                db.Usuarios.AddRange(
                    new Usuario { Id = tecnicoId, NombreCompleto = "Técnico", Email = "t@t.local", Activo = true },
                    new Usuario { Id = clienteUserId, NombreCompleto = "Cliente", Email = "c@t.local", Activo = true },
                    new Usuario { Id = soloWaVerId, NombreCompleto = "Monitor WA", Email = "m@t.local", Activo = true },
                    new Usuario { Id = soloWaGestId, NombreCompleto = "Operador WA", Email = "o@t.local", Activo = true },
                    new Usuario { Id = asesorCustomId, NombreCompleto = "Asesor Custom", Email = "a@t.local", Activo = true });

                db.UsuarioRoles.AddRange(
                    new UsuarioRol { UsuarioId = tecnicoId, RolId = rolTec.Id },
                    new UsuarioRol { UsuarioId = clienteUserId, RolId = rolCli.Id },
                    new UsuarioRol { UsuarioId = soloWaVerId, RolId = rolSoloWaVer.Id },
                    new UsuarioRol { UsuarioId = soloWaGestId, RolId = rolSoloWaGest.Id },
                    new UsuarioRol { UsuarioId = asesorCustomId, RolId = rolAsesorCustom.Id });

                var clienteVinculado = new Cliente
                {
                    UsuarioId = clienteUserId,
                    NombreCompleto = "Cliente Vinculado",
                    TipoDocumento = TipoDocumentoCliente.DNI,
                    NumeroDocumento = "45678901",
                    Telefono = "987654321",
                    Activo = true
                };
                var veh = new Vehiculo { Cliente = clienteVinculado, Marca = "Yamaha", Modelo = "R15", Placa = "8888-XX", Activo = true };
                var orden = new OrdenServicio { Cliente = clienteVinculado, Vehiculo = veh, NumeroOrden = "OS-000888", Estado = EstadoOrdenServicio.EnProceso, Total = 200m, Activo = true };
                var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Cliente = clienteVinculado, Activo = true };

                db.Clientes.Add(clienteVinculado);
                db.Vehiculos.Add(veh);
                db.OrdenesServicio.Add(orden);
                db.ConversacionesWhatsApp.Add(conv);
                await db.SaveChangesAsync();

                convId = conv.Id;
                ordenId = orden.Id;
            }

            using var client = app.GetTestClient();

            HttpRequestMessage CrearReq(HttpMethod method, string url, Guid? uid, string? role = null, string? stalePerm = null, object? body = null)
            {
                var req = new HttpRequestMessage(method, url);
                if (uid.HasValue) req.Headers.Add(TestAuthHandler.HeaderUserId, uid.Value.ToString());
                if (!string.IsNullOrWhiteSpace(role)) req.Headers.Add(TestAuthHandler.HeaderRole, role);
                if (!string.IsNullOrWhiteSpace(stalePerm)) req.Headers.Add(TestAuthHandler.HeaderStalePermission, stalePerm);
                if (body is not null) req.Content = JsonContent.Create(body);
                return req;
            }

            // 1. Anónimo -> 401
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/whatsapp/admin/conversaciones/{convId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/whatsapp/admin/conversaciones/{convId}/crm/perfil")).StatusCode);

            // 2. Técnico y Cliente portal -> 403
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/vinculacion", tecnicoId, RolesDefinidos.Tecnico))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/vehiculos", clienteUserId, RolesDefinidos.Cliente))).StatusCode);

            // 3. Usuario con SOLO whatsapp.ver (sin clientes.ver ni módulos CRM ni whatsapp.gestionar):
            //    - GET /conversaciones/{id}?incluirDocumento=true -> 200 pero documento SIEMPRE enmascarado (H-03.A)
            var respConvMask = await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}?incluirDocumento=true", soloWaVerId, "MonitorWhatsApp", stalePerm: PermisosDefinidos.ClientesVer));
            Assert.Equal(HttpStatusCode.OK, respConvMask.StatusCode);
            var convDetalle = await respConvMask.Content.ReadFromJsonAsync<WhatsAppConversacionDetalleResponse>();
            Assert.Equal("****8901", convDetalle!.ClienteDocumento);

            //    - GET /vinculacion?incluirDocumento=true -> 200 pero documento y teléfono enmascarados
            var respVincMask = await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/vinculacion?incluirDocumento=true", soloWaVerId, "MonitorWhatsApp"));
            Assert.Equal(HttpStatusCode.OK, respVincMask.StatusCode);
            var vincMask = await respVincMask.Content.ReadFromJsonAsync<WhatsAppVinculacionClienteResponse>();
            Assert.Equal("****8901", vincMask!.Cliente!.DocumentoVisible);
            Assert.Contains("*", vincMask.Cliente.Telefono);

            //    - Todos los endpoints CRM granulares rechazados con 403 por faltar el permiso del módulo (H-03.B)
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/perfil", soloWaVerId, "MonitorWhatsApp"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/vehiculos", soloWaVerId, "MonitorWhatsApp"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/ordenes", soloWaVerId, "MonitorWhatsApp"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/ordenes/{ordenId}", soloWaVerId, "MonitorWhatsApp"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/citas", soloWaVerId, "MonitorWhatsApp"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, "/api/whatsapp/admin/crm/catalogo", soloWaVerId, "MonitorWhatsApp"))).StatusCode);

            //    - Acciones de gestión rechazadas con 403 sin whatsapp.gestionar (H-03.D)
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Delete, $"/api/whatsapp/admin/conversaciones/{convId}/vincular-cliente", soloWaVerId, "MonitorWhatsApp"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Post, $"/api/whatsapp/admin/conversaciones/{convId}/identificar-cliente", soloWaVerId, "MonitorWhatsApp"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, "/api/whatsapp/admin/clientes/buscar?busqueda=Cliente", soloWaVerId, "MonitorWhatsApp"))).StatusCode);

            // 4. Usuario con whatsapp.gestionar SIN clientes.ver:
            //    - GET /clientes/buscar rechazado con 403 (H-03.C)
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, "/api/whatsapp/admin/clientes/buscar?busqueda=Cliente", soloWaGestId, "OperadorWhatsAppSinClientes"))).StatusCode);
            //    - Mutación (PUT /modo) responde 200 con ClienteDocumento enmascarado (H-03.A)
            var respModo = await client.SendAsync(CrearReq(HttpMethod.Put, $"/api/whatsapp/admin/conversaciones/{convId}/modo", soloWaGestId, "OperadorWhatsAppSinClientes", body: new WhatsAppCambiarModoRequest(ModoAtencionWhatsApp.Humano)));
            Assert.Equal(HttpStatusCode.OK, respModo.StatusCode);
            var modoDto = await respModo.Content.ReadFromJsonAsync<WhatsAppConversacionDetalleResponse>();
            Assert.Equal("****8901", modoDto!.ClienteDocumento);

            // 5. Rol personalizado autorizado con todos los permisos requeridos (M-06)
            var respCustomPerfil = await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/perfil?incluirDocumento=true", asesorCustomId, "AsesorWhatsAppIntegral"));
            Assert.Equal(HttpStatusCode.OK, respCustomPerfil.StatusCode);
            var perfilCustom = await respCustomPerfil.Content.ReadFromJsonAsync<WhatsAppPerfilClienteResponse>();
            Assert.Equal("45678901", perfilCustom!.DocumentoVisible);

            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/vehiculos", asesorCustomId, "AsesorWhatsAppIntegral"))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/ordenes/{ordenId}", asesorCustomId, "AsesorWhatsAppIntegral"))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/citas", asesorCustomId, "AsesorWhatsAppIntegral"))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(CrearReq(HttpMethod.Get, "/api/whatsapp/admin/crm/catalogo", asesorCustomId, "AsesorWhatsAppIntegral"))).StatusCode);

            // Búsqueda vacía rechazada con 400; válida responde 200 paginada
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(CrearReq(HttpMethod.Get, "/api/whatsapp/admin/clientes/buscar?busqueda=", asesorCustomId, "AsesorWhatsAppIntegral"))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(CrearReq(HttpMethod.Get, "/api/whatsapp/admin/clientes/buscar?busqueda=Cliente", asesorCustomId, "AsesorWhatsAppIntegral"))).StatusCode);

            // 6. Revocación en BD surte efecto inmediato sin depender de claims obsoletos (M-06)
            using (var db = CrearContexto(dbName))
            {
                var rp = await db.RolPermisos.SingleAsync(x => x.RolId == rolAsesorCustomId && x.PermisoId == permClientesVerId);
                db.RolPermisos.Remove(rp);
                await db.SaveChangesAsync();
            }

            // Incluso enviando claim obsoleto "permission: clientes.ver", queda denegado en BD inmediatamente
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}/crm/perfil", asesorCustomId, "AsesorWhatsAppIntegral", stalePerm: PermisosDefinidos.ClientesVer))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CrearReq(HttpMethod.Get, "/api/whatsapp/admin/clientes/buscar?busqueda=Cliente", asesorCustomId, "AsesorWhatsAppIntegral", stalePerm: PermisosDefinidos.ClientesVer))).StatusCode);

            var respConvPostRevocacion = await client.SendAsync(CrearReq(HttpMethod.Get, $"/api/whatsapp/admin/conversaciones/{convId}?incluirDocumento=true", asesorCustomId, "AsesorWhatsAppIntegral", stalePerm: PermisosDefinidos.ClientesVer));
            Assert.Equal(HttpStatusCode.OK, respConvPostRevocacion.StatusCode);
            var convPostRev = await respConvPostRevocacion.Content.ReadFromJsonAsync<WhatsAppConversacionDetalleResponse>();
            Assert.Equal("****8901", convPostRev!.ClienteDocumento);
        }
    }
}
