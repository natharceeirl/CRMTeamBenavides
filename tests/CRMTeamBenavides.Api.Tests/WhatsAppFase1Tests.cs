using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Chatbot;
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
using Microsoft.AspNetCore.Http;
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
/// Suite automatizada obligatoria — Fase 1 Infraestructura WhatsApp Business Cloud API.
/// 100 % local y sintética: sin llamadas externas a Meta, sin credenciales reales ni acceso a BD compartidas.
/// </summary>
public class WhatsAppFase1Tests
{
    private const string TestVerifyToken = "verify-token-sintetico-local-123";
    private const string TestAppSecret = "app-secret-sintetico-hmac-456";

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

        public void EncolarEventoEnTransaccion(
            Guid? usuarioId,
            string accion,
            string entidad,
            string entidadId,
            object? detalle = null,
            DateTime? fechaUtc = null)
        {
            Eventos.Add((usuarioId, accion, entidad, entidadId, detalle));
        }
    }

    private sealed class SpyAutoResponder : IWhatsAppAutoResponder
    {
        public int VecesInvocado { get; private set; }
        public List<Guid> MensajesRecibidos { get; } = new();

        public Task ProcesarMensajeEntranteAsync(
            ConversacionWhatsApp conversacion,
            MensajeWhatsApp mensaje,
            CancellationToken ct = default)
        {
            VecesInvocado++;
            MensajesRecibidos.Add(mensaje.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWhatsAppProvider : IWhatsAppProvider
    {
        private readonly Queue<WhatsAppSendResult> _respuestas = new();
        private int _llamadas;

        public string NombreProveedor => "FakeTest";
        public int TotalLlamadas => _llamadas;
        public List<WhatsAppSendTextRequest> Peticiones { get; } = new();

        public void Encolar(WhatsAppSendResult resultado) => _respuestas.Enqueue(resultado);

        public async Task<WhatsAppSendResult> EnviarTextoAsync(WhatsAppSendTextRequest request, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _llamadas);
            lock (Peticiones)
            {
                Peticiones.Add(request);
            }

            await Task.Delay(10, ct);

            lock (_respuestas)
            {
                if (_respuestas.Count > 0)
                {
                    return _respuestas.Dequeue();
                }
            }

            return new WhatsAppSendResult(true, $"wamid.fake.{Guid.NewGuid():N}", 200, null, TipoResultadoEnvioWhatsApp.Exitoso, NombreProveedor);
        }
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respuestas = new();
        public List<HttpRequestMessage> Peticiones { get; } = new();
        public List<string> CuerposEnviados { get; } = new();

        public void Encolar(HttpStatusCode status, string body)
        {
            _respuestas.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones.Add(request);
            CuerposEnviados.Add(request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty);
            return _respuestas.Count > 0
                ? _respuestas.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"messaging_product\":\"whatsapp\",\"messages\":[{\"id\":\"wamid.meta.test.1\"}]}", Encoding.UTF8, "application/json")
                };
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; set; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestAuth";
        public const string HeaderUserId = "X-Test-UserId";
        public const string HeaderRole = "X-Test-Role";

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

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
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

    private static WhatsAppSettings CrearSettings(
        bool enabled = true,
        string provider = WhatsAppSettings.ProviderMock,
        int maxPayloadBytes = 262_144) => new()
        {
            Enabled = enabled,
            Provider = provider,
            WebhookVerifyToken = TestVerifyToken,
            AppSecret = TestAppSecret,
            PhoneNumberId = "100000000000001",
            BusinessAccountId = "200000000000002",
            AccessToken = "synthetic-access-token-secret-999",
            GraphApiVersion = "v21.0",
            GraphApiBaseUrl = "https://graph.facebook.invalid",
            DefaultCountryCode = "51",
            MaxWebhookPayloadBytes = maxPayloadBytes,
            MaxSendRetries = 3
        };

    private static async Task<(WebApplication App, string DbName, SpyAutoResponder AutoResponder, AuditoriaEnMemoria Auditoria)>
        CrearServidorPruebaAsync(
            WhatsAppSettings settings,
            IWhatsAppProvider? customProvider = null)
    {
        var dbName = Guid.NewGuid().ToString();
        var autoResponder = new SpyAutoResponder();
        var auditoria = new AuditoriaEnMemoria();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddDbContext<ApplicationDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName)
               .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        builder.Services.AddSingleton<IOptionsMonitor<WhatsAppSettings>>(new StaticOptionsMonitor<WhatsAppSettings>(settings));
        builder.Services.AddSingleton<IAuditoriaService>(auditoria);
        builder.Services.AddSingleton<IWhatsAppAutoResponder>(autoResponder);

        if (customProvider is not null)
        {
            builder.Services.AddSingleton(customProvider);
        }
        else
        {
            builder.Services.AddSingleton<IWhatsAppProvider>(new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance));
        }

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
        return (app, dbName, autoResponder, auditoria);
    }

    private static string CrearPayloadMensajeTexto(
        string wamid,
        string from = "51987654321",
        string texto = "Hola taller Team Benavides",
        string nombrePerfil = "Carlos Mendoza",
        string timestamp = "1763686800") =>
        $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [
            {
              "id": "200000000000002",
              "changes": [
                {
                  "field": "messages",
                  "value": {
                    "messaging_product": "whatsapp",
                    "metadata": { "display_phone_number": "51900000000", "phone_number_id": "100000000000001" },
                    "contacts": [ { "profile": { "name": "{{nombrePerfil}}" }, "wa_id": "{{from}}" } ],
                    "messages": [
                      {
                        "from": "{{from}}",
                        "id": "{{wamid}}",
                        "timestamp": "{{timestamp}}",
                        "type": "text",
                        "text": { "body": "{{texto}}" }
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
        """;

    private static HttpRequestMessage CrearPostWebhookFirmado(string jsonCrudo, string? secretParaFirmar = TestAppSecret)
    {
        var bytes = Encoding.UTF8.GetBytes(jsonCrudo);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/whatsapp/webhook")
        {
            Content = new ByteArrayContent(bytes)
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        if (secretParaFirmar is not null)
        {
            req.Headers.Add(
                WhatsAppWebhookSecurity.HeaderFirmaMeta,
                WhatsAppWebhookSecurity.CalcularFirmaHmacSha256(bytes, secretParaFirmar));
        }

        return req;
    }

    // =========================================================================
    // 1 y 2. Verificación GET válida e inválida del webhook
    // =========================================================================

    [Fact]
    public async Task T01_WebhookGet_VerificacionValida_DevuelveChallengeEnTextoPlano()
    {
        var (app, _, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();
            var url = $"/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token={TestVerifyToken}&hub.challenge=challenge-abc-987";

            var resp = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Equal("text/plain", resp.Content.Headers.ContentType?.MediaType);
            Assert.Equal("challenge-abc-987", await resp.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task T02_WebhookGet_VerificacionInvalida_RechazaCon403()
    {
        var (app, _, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();

            var tokenIncorrecto = await client.GetAsync("/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=token-falso&hub.challenge=12345");
            var modoIncorrecto = await client.GetAsync($"/api/whatsapp/webhook?hub.mode=unsubscribe&hub.verify_token={TestVerifyToken}&hub.challenge=12345");
            var sinChallenge = await client.GetAsync($"/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token={TestVerifyToken}");

            Assert.Equal(HttpStatusCode.Forbidden, tokenIncorrecto.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, modoIncorrecto.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, sinChallenge.StatusCode);
        }
    }

    // =========================================================================
    // 3, 4 y 5. Firma HMAC válida, firma inválida, payload malformado y límite tamaño
    // =========================================================================

    [Fact]
    public async Task T03_WebhookPost_FirmaHmacValidaSobreCuerpoCrudo_AceptaYPersiste()
    {
        var (app, _, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();
            // Espacios y saltos intencionales para comprobar que HMAC se valida sobre el cuerpo crudo original
            var payload = "   \n" + CrearPayloadMensajeTexto("wamid.hmac.ok.001") + "   \n";
            using var req = CrearPostWebhookFirmado(payload, TestAppSecret);

            var resp = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("\"mensajesProcesados\":1", body);
        }
    }

    [Fact]
    public async Task T04_WebhookPost_FirmaHmacInvalidaOAusente_RechazaCon401YSinPersistir()
    {
        var (app, dbName, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();
            var payload = CrearPayloadMensajeTexto("wamid.hmac.bad.001");

            using var reqSecretIncorrecto = CrearPostWebhookFirmado(payload, "otro-secreto-invalido");
            var resp1 = await client.SendAsync(reqSecretIncorrecto);

            using var reqSinFirma = CrearPostWebhookFirmado(payload, secretParaFirmar: null);
            var resp2 = await client.SendAsync(reqSinFirma);

            Assert.Equal(HttpStatusCode.Unauthorized, resp1.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, resp2.StatusCode);

            using var db = CrearContexto(dbName);
            Assert.Equal(0, await db.MensajesWhatsApp.CountAsync());
        }
    }

    [Fact]
    public async Task T05_WebhookPost_PayloadMalformadoYExcesoTamanio_Retorna400Y413()
    {
        var (app, _, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true, maxPayloadBytes: 512));
        await using (app)
        {
            using var client = app.GetTestClient();

            using var reqJsonRoto = CrearPostWebhookFirmado("{ \"object\": \"whatsapp_business_account\", roto }");
            var respJsonRoto = await client.SendAsync(reqJsonRoto);
            Assert.Equal(HttpStatusCode.BadRequest, respJsonRoto.StatusCode);

            using var reqSinObject = CrearPostWebhookFirmado("{\"entry\":[]}");
            var respSinObject = await client.SendAsync(reqSinObject);
            Assert.Equal(HttpStatusCode.BadRequest, respSinObject.StatusCode);

            var grande = "{\"object\":\"whatsapp_business_account\",\"pad\":\"" + new string('A', 700) + "\"}";
            using var reqGrande = CrearPostWebhookFirmado(grande);
            var respGrande = await client.SendAsync(reqGrande);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respGrande.StatusCode);
        }
    }

    // =========================================================================
    // 6, 7, 8 y 9. Texto, multimedia, estados (y fuera de orden) y evento desconocido
    // =========================================================================

    [Fact]
    public async Task T06_Webhook_MensajeTextoEntrante_CreaConversacionYMensajeConTrazabilidad()
    {
        var (app, dbName, autoResponder, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();
            var payload = CrearPayloadMensajeTexto("wamid.texto.001", "51987654321", "Necesito mantenimiento FZ25", "Luis Quispe");

            var resp = await client.SendAsync(CrearPostWebhookFirmado(payload));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var db = CrearContexto(dbName);
            var conv = await db.ConversacionesWhatsApp.SingleAsync();
            Assert.Equal("+51987654321", conv.TelefonoE164);
            Assert.Equal("51987654321", conv.WhatsAppId);
            Assert.Equal("Luis Quispe", conv.NombrePerfilWhatsApp);
            Assert.Equal(ModoAtencionWhatsApp.Bot, conv.ModoAtencion);

            var msg = await db.MensajesWhatsApp.SingleAsync();
            Assert.Equal(conv.Id, msg.ConversacionId);
            Assert.Equal("wamid.texto.001", msg.ExternalMessageId);
            Assert.Equal(DireccionMensajeWhatsApp.Entrante, msg.Direccion);
            Assert.Equal(TipoMensajeWhatsApp.Texto, msg.Tipo);
            Assert.Equal("Necesito mantenimiento FZ25", msg.ContenidoTexto);
            Assert.Equal(EstadoMensajeWhatsApp.Recibido, msg.Estado);
            Assert.Equal(1, autoResponder.VecesInvocado);
            Assert.Equal(1, await db.EventosWebhookWhatsApp.CountAsync());
        }
    }

    [Fact]
    public async Task T07_Webhook_MensajesMultimediaYUbicacionYTipoDesconocido_ReconoceTiposYMetadatos()
    {
        var (app, dbName, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();
            var payload = """
            {
              "object": "whatsapp_business_account",
              "entry": [{
                "id": "200000000000002",
                "changes": [{
                  "field": "messages",
                  "value": {
                    "messaging_product": "whatsapp",
                    "messages": [
                      {
                        "from": "51912345678",
                        "id": "wamid.img.1",
                        "timestamp": "1763686801",
                        "type": "image",
                        "image": { "id": "media-img-1", "mime_type": "image/jpeg", "sha256": "abc123", "caption": "Foto del tablero" }
                      },
                      {
                        "from": "51912345678",
                        "id": "wamid.doc.1",
                        "timestamp": "1763686802",
                        "type": "document",
                        "document": { "id": "media-doc-1", "mime_type": "application/pdf", "filename": "tarjeta.pdf" }
                      },
                      {
                        "from": "51912345678",
                        "id": "wamid.aud.1",
                        "timestamp": "1763686803",
                        "type": "audio",
                        "audio": { "id": "media-aud-1", "mime_type": "audio/ogg" }
                      },
                      {
                        "from": "51912345678",
                        "id": "wamid.vid.1",
                        "timestamp": "1763686804",
                        "type": "video",
                        "video": { "id": "media-vid-1", "mime_type": "video/mp4", "caption": "Ruido motor" }
                      },
                      {
                        "from": "51912345678",
                        "id": "wamid.loc.1",
                        "timestamp": "1763686805",
                        "type": "location",
                        "location": { "latitude": -16.409047, "longitude": -71.537451, "name": "Arequipa", "address": "Av. Ejercito" }
                      },
                      {
                        "from": "51912345678",
                        "id": "wamid.unk.1",
                        "timestamp": "1763686806",
                        "type": "order_future_type"
                      }
                    ]
                  }
                }]
              }]
            }
            """;

            var resp = await client.SendAsync(CrearPostWebhookFirmado(payload));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var db = CrearContexto(dbName);
            var mensajes = await db.MensajesWhatsApp.OrderBy(m => m.FechaEventoUtc).ToListAsync();
            Assert.Equal(6, mensajes.Count);

            var img = mensajes.Single(m => m.ExternalMessageId == "wamid.img.1");
            Assert.Equal(TipoMensajeWhatsApp.Imagen, img.Tipo);
            Assert.Equal("media-img-1", img.MediaId);
            Assert.Equal("image/jpeg", img.MediaMimeType);
            Assert.Equal("Foto del tablero", img.ContenidoTexto);

            var doc = mensajes.Single(m => m.ExternalMessageId == "wamid.doc.1");
            Assert.Equal(TipoMensajeWhatsApp.Documento, doc.Tipo);
            Assert.Equal("tarjeta.pdf", doc.MediaNombreArchivo);

            var aud = mensajes.Single(m => m.ExternalMessageId == "wamid.aud.1");
            Assert.Equal(TipoMensajeWhatsApp.Audio, aud.Tipo);
            Assert.Null(aud.ContenidoTexto);
            Assert.Equal("media-aud-1", aud.MediaId);

            var vid = mensajes.Single(m => m.ExternalMessageId == "wamid.vid.1");
            Assert.Equal(TipoMensajeWhatsApp.Video, vid.Tipo);
            Assert.Equal("Ruido motor", vid.ContenidoTexto);

            var loc = mensajes.Single(m => m.ExternalMessageId == "wamid.loc.1");
            Assert.Equal(TipoMensajeWhatsApp.Ubicacion, loc.Tipo);
            Assert.Contains("-16.409047", loc.MetadataTecnicaJson);

            var unk = mensajes.Single(m => m.ExternalMessageId == "wamid.unk.1");
            Assert.Equal(TipoMensajeWhatsApp.Desconocido, unk.Tipo);
            Assert.Contains("order_future_type", unk.MetadataTecnicaJson);
        }
    }

    [Fact]
    public async Task T08_Webhook_NotificacionEstado_ActualizaMensajeSinCrearEntranteYProtegeFueraDeOrden()
    {
        var (app, dbName, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using (var seed = CrearContexto(dbName))
            {
                var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Activo = true };
                seed.ConversacionesWhatsApp.Add(conv);
                seed.MensajesWhatsApp.Add(new MensajeWhatsApp
                {
                    ConversacionId = conv.Id,
                    ExternalMessageId = "wamid.saliente.001",
                    Direccion = DireccionMensajeWhatsApp.Saliente,
                    Tipo = TipoMensajeWhatsApp.Texto,
                    ContenidoTexto = "Tu moto está lista",
                    Estado = EstadoMensajeWhatsApp.Enviado,
                    FechaEventoUtc = DateTime.UtcNow,
                    FechaRecepcionUtc = DateTime.UtcNow,
                    Activo = true
                });
                await seed.SaveChangesAsync();
            }

            using var client = app.GetTestClient();

            // 1. Llega primero "read" (t=1763686900)
            var statusRead = """
            {
              "object": "whatsapp_business_account",
              "entry": [{
                "id": "200000000000002",
                "changes": [{
                  "field": "messages",
                  "value": {
                    "messaging_product": "whatsapp",
                    "statuses": [{ "id": "wamid.saliente.001", "status": "read", "timestamp": "1763686900", "recipient_id": "51987654321" }]
                  }
                }]
              }]
            }
            """;

            var respRead = await client.SendAsync(CrearPostWebhookFirmado(statusRead));
            Assert.Equal(HttpStatusCode.OK, respRead.StatusCode);

            // 2. Llega después un "delivered" retrasado (fuera de orden) -> NO debe degradar de Leido a Entregado
            var statusDeliveredTardio = statusRead.Replace("\"read\"", "\"delivered\"").Replace("1763686900", "1763686890");
            var respDelivered = await client.SendAsync(CrearPostWebhookFirmado(statusDeliveredTardio));
            Assert.Equal(HttpStatusCode.OK, respDelivered.StatusCode);

            using var db = CrearContexto(dbName);
            // Sigue habiendo exactamente 1 mensaje (jamás creó un mensaje entrante por notificación de estado)
            var msg = await db.MensajesWhatsApp.SingleAsync();
            Assert.Equal(EstadoMensajeWhatsApp.Leido, msg.Estado);
            Assert.Equal(DireccionMensajeWhatsApp.Saliente, msg.Direccion);
        }
    }

    [Fact]
    public async Task T09_Webhook_EventoDesconocido_Responde200SinErroresYRegistraTrazabilidad()
    {
        var (app, dbName, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();
            var payload = """
            {
              "object": "whatsapp_business_account",
              "entry": [{
                "id": "200000000000002",
                "changes": [{
                  "field": "account_alerts",
                  "value": { "event": "QUALITY_UPDATE" }
                }]
              }]
            }
            """;

            var resp = await client.SendAsync(CrearPostWebhookFirmado(payload));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var db = CrearContexto(dbName);
            Assert.Equal(0, await db.MensajesWhatsApp.CountAsync());
            var evento = await db.EventosWebhookWhatsApp.SingleAsync();
            Assert.Equal("unknown", evento.TipoEvento);
            Assert.True(evento.ProcesadoConExito);
        }
    }

    // =========================================================================
    // 10 y 11. Deduplicación de webhook repetido y eventos concurrentes
    // =========================================================================

    [Fact]
    public async Task T10_Webhook_RepetidoMismoWamid_NoGeneraMensajesDuplicados()
    {
        var (app, dbName, autoResponder, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();
            var payload = CrearPayloadMensajeTexto("wamid.duplicado.001");

            var resp1 = await client.SendAsync(CrearPostWebhookFirmado(payload));
            var resp2 = await client.SendAsync(CrearPostWebhookFirmado(payload));

            Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
            Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
            Assert.Contains("\"mensajesDuplicados\":1", await resp2.Content.ReadAsStringAsync());

            using var db = CrearContexto(dbName);
            Assert.Equal(1, await db.MensajesWhatsApp.CountAsync());
            Assert.Equal(1, autoResponder.VecesInvocado);
        }
    }

    [Fact]
    public async Task T11_Webhook_DosEventosConcurrentesMismoWamid_PersisteSoloUno()
    {
        var (app, dbName, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();
            var payload = CrearPayloadMensajeTexto("wamid.concurrente.001");

            var tarea1 = client.SendAsync(CrearPostWebhookFirmado(payload));
            var tarea2 = client.SendAsync(CrearPostWebhookFirmado(payload));
            var respuestas = await Task.WhenAll(tarea1, tarea2);

            Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

            using var db = CrearContexto(dbName);
            Assert.Equal(1, await db.MensajesWhatsApp.CountAsync(m => m.ExternalMessageId == "wamid.concurrente.001"));
            Assert.Equal(1, await db.ConversacionesWhatsApp.CountAsync());
        }
    }

    // =========================================================================
    // 12, 13 y 14. Normalización E.164, sin cliente y asociación ambigua
    // =========================================================================

    [Theory]
    [InlineData("987654321", "+51987654321", "51987654321", true)]
    [InlineData("+51 987-654-321", "+51987654321", "51987654321", true)]
    [InlineData("0051987654321", "+51987654321", "51987654321", true)]
    [InlineData("51987654321", "+51987654321", "51987654321", true)]
    [InlineData("123", null, null, false)]
    [InlineData("", null, null, false)]
    public void T12_NormalizacionTelefono_E164(string entrada, string? e164Esperado, string? waIdEsperado, bool validoEsperado)
    {
        var res = WhatsAppPhoneNormalizer.NormalizarE164(entrada);
        Assert.Equal(validoEsperado, res.EsValido);
        Assert.Equal(e164Esperado, res.TelefonoE164);
        Assert.Equal(waIdEsperado, res.WhatsAppId);
    }

    [Fact]
    public async Task T13_Webhook_NumeroSinClienteAsociado_NoCreaClienteYDejaClienteIdNulo()
    {
        var (app, dbName, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();
            var payload = CrearPayloadMensajeTexto("wamid.sincliente.001", "51999888777");

            var resp = await client.SendAsync(CrearPostWebhookFirmado(payload));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var db = CrearContexto(dbName);
            var conv = await db.ConversacionesWhatsApp.SingleAsync();
            Assert.Null(conv.ClienteId);
            Assert.False(conv.AsociacionClienteAmbigua);
            Assert.Equal(0, await db.Clientes.CountAsync());
        }
    }

    [Fact]
    public async Task T14_Webhook_NumeroConAsociacionAmbigua_NoVinculaNiFusionaClientes()
    {
        var (app, dbName, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using (var seed = CrearContexto(dbName))
            {
                seed.Clientes.AddRange(
                    new Cliente { NombreCompleto = "Cliente A", TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40111222", Telefono = "987-654-321", Activo = true },
                    new Cliente { NombreCompleto = "Cliente B", TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40333444", Telefono = "+51 987654321", Activo = true });
                await seed.SaveChangesAsync();
            }

            using var client = app.GetTestClient();
            var payload = CrearPayloadMensajeTexto("wamid.ambiguo.001", "51987654321");

            var resp = await client.SendAsync(CrearPostWebhookFirmado(payload));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var db = CrearContexto(dbName);
            var conv = await db.ConversacionesWhatsApp.SingleAsync();
            Assert.Null(conv.ClienteId);
            Assert.True(conv.AsociacionClienteAmbigua);
            Assert.Equal(2, await db.Clientes.CountAsync());
        }
    }

    // =========================================================================
    // 15, 16 y 17. Envío simulado, error de proveedor (Outbox + Meta) y prevención de duplicados
    // =========================================================================

    [Fact]
    public async Task T15_EnvioSimulado_MockProvider_DespachaYRegistraEstadoYAuditoria()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CrearContexto(dbName);
        var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Activo = true };
        db.ConversacionesWhatsApp.Add(conv);
        await db.SaveChangesAsync();

        var settingsMonitor = new StaticOptionsMonitor<WhatsAppSettings>(CrearSettings(enabled: true, provider: WhatsAppSettings.ProviderMock));
        var mockProvider = new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance);
        var outboxService = new WhatsAppOutboxService(db, mockProvider, settingsMonitor, NullLogger<WhatsAppOutboxService>.Instance);
        var auditoria = new AuditoriaEnMemoria();
        var identSvc = new WhatsAppClienteIdentificacionService(db, settingsMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
        var service = new WhatsAppService(db, outboxService, identSvc, auditoria, settingsMonitor);

        var usuarioId = Guid.NewGuid();
        var res = await service.EnviarMensajeManualAsync(
            conv.Id,
            new WhatsAppEnviarMensajeRequest("Hola, tu moto ya está lista para entrega.", "idem-sim-001"),
            usuarioId);

        Assert.True(res.IsSuccess);
        Assert.Equal(EstadoMensajeWhatsApp.Enviado, res.Data!.Estado);
        Assert.StartsWith("wamid.mock.", res.Data.ExternalMessageId);
        Assert.Single(mockProvider.EnviosSimulados);

        var outbox = await db.OutboxMensajesWhatsApp.SingleAsync();
        Assert.Equal(EstadoOutboxWhatsApp.Enviado, outbox.Estado);
        Assert.Equal(1, outbox.Intentos);

        // Auditoría registrada sin exponer el texto completo del mensaje
        var evento = Assert.Single(auditoria.Eventos);
        Assert.Equal("WHATSAPP_ENVIAR_MENSAJE", evento.Accion);
        Assert.DoesNotContain("tu moto ya está lista", JsonSerializer.Serialize(evento.Detalle));
    }

    [Fact]
    public async Task T16_ErrorProveedor_TransitorioReprogramaOutbox_YPermanenteMarcaFallidoSinExponerSecretos()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CrearContexto(dbName);
        var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Activo = true };
        db.ConversacionesWhatsApp.Add(conv);
        await db.SaveChangesAsync();

        var settings = CrearSettings(enabled: true, provider: WhatsAppSettings.ProviderMeta);
        var settingsMonitor = new StaticOptionsMonitor<WhatsAppSettings>(settings);
        var fakeProvider = new FakeWhatsAppProvider();
        fakeProvider.Encolar(new WhatsAppSendResult(false, null, 503, $"Servicio caído {settings.AccessToken}", TipoResultadoEnvioWhatsApp.ErrorReintentableSeguro, "Meta", RetryAfterSegundos: 15));
        fakeProvider.Encolar(new WhatsAppSendResult(false, null, 400, $"Plantilla requerida {settings.AccessToken}", TipoResultadoEnvioWhatsApp.ErrorDefinitivo, "Meta"));
        fakeProvider.Encolar(new WhatsAppSendResult(false, null, null, $"Timeout HTTP {settings.AccessToken}", TipoResultadoEnvioWhatsApp.ResultadoAmbiguo, "Meta"));

        var outboxService = new WhatsAppOutboxService(db, fakeProvider, settingsMonitor, NullLogger<WhatsAppOutboxService>.Instance);
        var identSvc = new WhatsAppClienteIdentificacionService(db, settingsMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
        var service = new WhatsAppService(db, outboxService, identSvc, new AuditoriaEnMemoria(), settingsMonitor);

        // 1. Error transitorio 503 -> queda Pendiente para reintento, sin secreto en ErrorDetalle
        var resTransitorio = await service.EnviarMensajeManualAsync(
            conv.Id,
            new WhatsAppEnviarMensajeRequest("Aviso transitorio", "idem-err-503"),
            Guid.NewGuid());

        Assert.True(resTransitorio.IsSuccess);
        Assert.Equal(EstadoMensajeWhatsApp.Pendiente, resTransitorio.Data!.Estado);
        Assert.DoesNotContain(settings.AccessToken, resTransitorio.Data.ErrorDetalle ?? string.Empty);
        var outbox1 = await db.OutboxMensajesWhatsApp.SingleAsync(o => o.IdempotencyKey == "idem-err-503");
        Assert.Equal(EstadoOutboxWhatsApp.Pendiente, outbox1.Estado);
        Assert.NotNull(outbox1.ProximoIntentoUtc);

        // 2. Error permanente 400 -> queda Fallido definitivo
        var resPermanente = await service.EnviarMensajeManualAsync(
            conv.Id,
            new WhatsAppEnviarMensajeRequest("Aviso permanente", "idem-err-400"),
            Guid.NewGuid());

        Assert.True(resPermanente.IsSuccess);
        Assert.Equal(EstadoMensajeWhatsApp.Fallido, resPermanente.Data!.Estado);
        Assert.DoesNotContain(settings.AccessToken, resPermanente.Data.ErrorDetalle ?? string.Empty);

        // 3. Resultado ambiguo (timeout) -> queda Indeterminado y NO se reintenta en ciclos posteriores del Outbox
        var resAmbiguo = await service.EnviarMensajeManualAsync(
            conv.Id,
            new WhatsAppEnviarMensajeRequest("Aviso timeout ambiguo", "idem-err-timeout"),
            Guid.NewGuid());

        Assert.True(resAmbiguo.IsSuccess);
        Assert.Equal(EstadoMensajeWhatsApp.Indeterminado, resAmbiguo.Data!.Estado);
        Assert.DoesNotContain(settings.AccessToken, resAmbiguo.Data.ErrorDetalle ?? string.Empty);

        var outboxAmbiguo = await db.OutboxMensajesWhatsApp.SingleAsync(o => o.IdempotencyKey == "idem-err-timeout");
        Assert.Equal(EstadoOutboxWhatsApp.Indeterminado, outboxAmbiguo.Estado);
        Assert.Null(outboxAmbiguo.ProximoIntentoUtc);
        Assert.Contains("ResultadoAmbiguo", outboxAmbiguo.HistorialIntentosJson ?? string.Empty);

        // El worker Outbox no debe reintentar el registro Indeterminado
        var llamadasAntes = fakeProvider.TotalLlamadas;
        var reprocesados = await outboxService.ProcesarLotePendienteAsync();
        Assert.Equal(0, reprocesados);
        Assert.Equal(llamadasAntes, fakeProvider.TotalLlamadas);

        // 4. Validar contrato HTTP de MetaWhatsAppProvider con HttpMessageHandler simulado (sin red real)
        var httpHandler = new FakeHttpMessageHandler();
        httpHandler.Encolar(HttpStatusCode.OK, "{\"messaging_product\":\"whatsapp\",\"messages\":[{\"id\":\"wamid.meta.real.77\"}]}");
        using var httpClient = new HttpClient(httpHandler);
        var metaProvider = new MetaWhatsAppProvider(httpClient, settingsMonitor, NullLogger<MetaWhatsAppProvider>.Instance);

        var metaResp = await metaProvider.EnviarTextoAsync(new WhatsAppSendTextRequest("+51987654321", "Prueba contrato Meta", "idem-meta-1"));
        Assert.True(metaResp.Exitoso);
        Assert.Equal("wamid.meta.real.77", metaResp.ExternalMessageId);
        Assert.Single(httpHandler.Peticiones);
        Assert.Equal("https://graph.facebook.invalid/v21.0/100000000000001/messages", httpHandler.Peticiones[0].RequestUri!.ToString());
        Assert.Equal("Bearer", httpHandler.Peticiones[0].Headers.Authorization?.Scheme);
        Assert.Contains("\"to\":\"51987654321\"", httpHandler.CuerposEnviados[0]);
    }

    [Fact]
    public async Task T17_PrevencionEnvioDuplicado_MismaIdempotencyKeyConcurrente_EnviaUnaSolaVez()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid convId;
        using (var seed = CrearContexto(dbName))
        {
            var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Activo = true };
            seed.ConversacionesWhatsApp.Add(conv);
            await seed.SaveChangesAsync();
            convId = conv.Id;
        }

        var fakeProvider = new FakeWhatsAppProvider();
        var settingsMonitor = new StaticOptionsMonitor<WhatsAppSettings>(CrearSettings(enabled: true));

        async Task<ServiceResult<WhatsAppMensajeResponse>> EnviarAsync()
        {
            using var dbLocal = CrearContexto(dbName);
            var outbox = new WhatsAppOutboxService(dbLocal, fakeProvider, settingsMonitor, NullLogger<WhatsAppOutboxService>.Instance);
            var identSvc = new WhatsAppClienteIdentificacionService(dbLocal, settingsMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
            var svc = new WhatsAppService(dbLocal, outbox, identSvc, new AuditoriaEnMemoria(), settingsMonitor);
            return await svc.EnviarMensajeManualAsync(convId, new WhatsAppEnviarMensajeRequest("Mensaje único", "idem-unico-999"), Guid.NewGuid());
        }

        var resultados = await Task.WhenAll(EnviarAsync(), EnviarAsync());

        Assert.All(resultados, r => Assert.True(r.IsSuccess));
        Assert.Equal(resultados[0].Data!.Id, resultados[1].Data!.Id);
        Assert.Equal(1, fakeProvider.TotalLlamadas);

        using var dbCheck = CrearContexto(dbName);
        Assert.Equal(1, await dbCheck.MensajesWhatsApp.CountAsync());
        Assert.Equal(1, await dbCheck.OutboxMensajesWhatsApp.CountAsync());
    }

    // =========================================================================
    // 18. Conversación en modo humano impide respuestas automáticas del bot
    // =========================================================================

    [Fact]
    public async Task T18_ConversacionEnModoHumano_ImpideInterferenciaDelBotAnteNuevoMensaje()
    {
        var (app, dbName, autoResponder, auditoria) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            using var client = app.GetTestClient();

            // 1. Primer mensaje llega en modo Bot -> AutoResponder SÍ se invoca (1 vez)
            var resp1 = await client.SendAsync(CrearPostWebhookFirmado(CrearPayloadMensajeTexto("wamid.modo.1")));
            Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
            Assert.Equal(1, autoResponder.VecesInvocado);

            // 2. Un trabajador cambia la conversación a modo Humano
            Guid convId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var svc = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
                convId = (await db.ConversacionesWhatsApp.SingleAsync()).Id;
                var cambio = await svc.CambiarModoAtencionAsync(convId, new WhatsAppCambiarModoRequest(ModoAtencionWhatsApp.Humano), Guid.NewGuid());
                Assert.True(cambio.IsSuccess);
                Assert.Equal(ModoAtencionWhatsApp.Humano, cambio.Data!.ModoAtencion);
            }

            // 3. Segundo mensaje del cliente -> persiste el mensaje, pero AutoResponder NO se vuelve a invocar
            var resp2 = await client.SendAsync(CrearPostWebhookFirmado(CrearPayloadMensajeTexto("wamid.modo.2", texto: "¿Me atiende un asesor?")));
            Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
            Assert.Equal(1, autoResponder.VecesInvocado);

            using var check = CrearContexto(dbName);
            Assert.Equal(2, await check.MensajesWhatsApp.CountAsync(m => m.ConversacionId == convId));
            Assert.Contains(auditoria.Eventos, e => e.Accion == "WHATSAPP_CAMBIAR_MODO");
        }
    }

    // =========================================================================
    // 19. Autorización: usuario sin permisos administrativos es rechazado
    // =========================================================================

    [Fact]
    public async Task T19_EndpointsAdmin_UsuarioSinPermisosOCliente_RechazaCon401Y403_YRecepcionAutorizadaAccede()
    {
        var (app, dbName, _, _) = await CrearServidorPruebaAsync(CrearSettings(enabled: true));
        await using (app)
        {
            var tecnicoId = Guid.NewGuid();
            var clienteUsuarioId = Guid.NewGuid();
            var recepcionId = Guid.NewGuid();

            using (var db = CrearContexto(dbName))
            {
                var rolTecnico = await db.Roles.SingleAsync(r => r.Nombre == RolesDefinidos.Tecnico);
                var rolCliente = await db.Roles.SingleAsync(r => r.Nombre == RolesDefinidos.Cliente);
                var rolRecepcion = await db.Roles.SingleAsync(r => r.Nombre == RolesDefinidos.Recepcion);

                db.Usuarios.AddRange(
                    new Usuario { Id = tecnicoId, NombreCompleto = "Técnico Sin Acceso", Email = "tec@test.local", Activo = true },
                    new Usuario { Id = clienteUsuarioId, NombreCompleto = "Cliente Portal", Email = "cli@test.local", Activo = true },
                    new Usuario { Id = recepcionId, NombreCompleto = "Recepción Autorizada", Email = "rec@test.local", Activo = true });

                db.UsuarioRoles.AddRange(
                    new UsuarioRol { UsuarioId = tecnicoId, RolId = rolTecnico.Id },
                    new UsuarioRol { UsuarioId = clienteUsuarioId, RolId = rolCliente.Id },
                    new UsuarioRol { UsuarioId = recepcionId, RolId = rolRecepcion.Id });

                db.Clientes.Add(new Cliente
                {
                    UsuarioId = clienteUsuarioId,
                    NombreCompleto = "Cliente Portal",
                    TipoDocumento = TipoDocumentoCliente.DNI,
                    NumeroDocumento = "44556677",
                    Activo = true
                });

                await db.SaveChangesAsync();
            }

            using var client = app.GetTestClient();

            // 1. Sin autenticación -> 401
            var anonimo = await client.GetAsync("/api/whatsapp/admin/conversaciones");
            Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);

            // 2. Técnico (no tiene whatsapp.ver) -> 403
            using var reqTec = new HttpRequestMessage(HttpMethod.Get, "/api/whatsapp/admin/conversaciones");
            reqTec.Headers.Add(TestAuthHandler.HeaderUserId, tecnicoId.ToString());
            reqTec.Headers.Add(TestAuthHandler.HeaderRole, RolesDefinidos.Tecnico);
            var respTec = await client.SendAsync(reqTec);
            Assert.Equal(HttpStatusCode.Forbidden, respTec.StatusCode);

            // 3. Cliente portal -> 403
            using var reqCli = new HttpRequestMessage(HttpMethod.Get, "/api/whatsapp/admin/conversaciones");
            reqCli.Headers.Add(TestAuthHandler.HeaderUserId, clienteUsuarioId.ToString());
            reqCli.Headers.Add(TestAuthHandler.HeaderRole, RolesDefinidos.Cliente);
            var respCli = await client.SendAsync(reqCli);
            Assert.Equal(HttpStatusCode.Forbidden, respCli.StatusCode);

            // 4. Recepción (tiene whatsapp.ver) -> 200
            using var reqRec = new HttpRequestMessage(HttpMethod.Get, "/api/whatsapp/admin/conversaciones");
            reqRec.Headers.Add(TestAuthHandler.HeaderUserId, recepcionId.ToString());
            reqRec.Headers.Add(TestAuthHandler.HeaderRole, RolesDefinidos.Recepcion);
            var respRec = await client.SendAsync(reqRec);
            Assert.Equal(HttpStatusCode.OK, respRec.StatusCode);
        }
    }

    // =========================================================================
    // 20. Integración deshabilitada (por defecto) bloquea webhooks y envíos
    // =========================================================================

    [Fact]
    public async Task T20_IntegracionDeshabilitada_BloqueaWebhookYEnviosSinLlamadasExternas()
    {
        var settingsDeshabilitados = CrearSettings(enabled: false, provider: WhatsAppSettings.ProviderMeta);
        var (app, dbName, _, _) = await CrearServidorPruebaAsync(settingsDeshabilitados);
        await using (app)
        {
            using var client = app.GetTestClient();

            // Webhook GET y POST devuelven 503
            var getResp = await client.GetAsync($"/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token={TestVerifyToken}&hub.challenge=12345");
            var postResp = await client.SendAsync(CrearPostWebhookFirmado(CrearPayloadMensajeTexto("wamid.disabled.1")));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, getResp.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, postResp.StatusCode);

            // Envío manual rechazado sin crear mensaje
            Guid convId;
            using (var seed = CrearContexto(dbName))
            {
                var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Activo = true };
                seed.ConversacionesWhatsApp.Add(conv);
                await seed.SaveChangesAsync();
                convId = conv.Id;
            }

            using (var scope = app.Services.CreateScope())
            {
                var svc = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
                var envio = await svc.EnviarMensajeManualAsync(convId, new WhatsAppEnviarMensajeRequest("No debe salir"), Guid.NewGuid());
                Assert.False(envio.IsSuccess);
                Assert.Equal(ServiceResultStatus.ValidationError, envio.Status);
            }

            // MetaWhatsAppProvider tampoco ejecuta llamadas HTTP si Enabled=false
            var httpHandler = new FakeHttpMessageHandler();
            using var httpClient = new HttpClient(httpHandler);
            var metaProvider = new MetaWhatsAppProvider(
                httpClient,
                new StaticOptionsMonitor<WhatsAppSettings>(settingsDeshabilitados),
                NullLogger<MetaWhatsAppProvider>.Instance);

            var metaResp = await metaProvider.EnviarTextoAsync(new WhatsAppSendTextRequest("+51987654321", "No debe enviarse", "idem-off"));
            Assert.False(metaResp.Exitoso);
            Assert.Empty(httpHandler.Peticiones);
        }
    }

    // =========================================================================
    // 21. Compatibilidad: Chatbot Web/Móvil existente sigue operativo e intacto
    // =========================================================================

    [Fact]
    public async Task T21_ChatbotWebMovilExistente_ContinuaOperandoDeFormaIndependiente()
    {
        using var db = CrearContexto();
        await FaqSeeder.SeedAsync(db);

        var chatbot = new ChatbotService(db);

        var faqs = await chatbot.GetFaqsPublicasAsync(categoria: null, busqueda: "horario");
        Assert.Single(faqs);

        var resSaludo = await chatbot.ConsultarAsync(
            new ConsultaChatbotRequest("Hola buenas tardes", null, null, "Web"),
            usuarioAutenticadoId: null);
        Assert.True(resSaludo.IsSuccess);
        Assert.Equal(3, resSaludo.Data!.Sugerencias.Count);

        var resAgente = await chatbot.SolicitarAgenteAsync(
            new SolicitarAgenteRequest(null, "Visitante Web", "987654321", "Consulta web", "Web"),
            usuarioAutenticadoId: null);

        Assert.True(resAgente.IsSuccess);
        Assert.Equal(1, await db.ConsultasChatbot.CountAsync(c => c.RequiereAtencionAgente));
        Assert.Equal(0, await db.ConversacionesWhatsApp.CountAsync());
    }

    // =========================================================================
    // 22 y 23. Recuperación de reserva expirada (Indeterminado) y configuración Meta incompleta
    // =========================================================================

    [Fact]
    public async Task T22_OutboxReservaExpiradaTrasCaida_MarcaIndeterminadoSinReenviarDuplicado()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CrearContexto(dbName);
        var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Activo = true };
        var msg = new MensajeWhatsApp
        {
            Conversacion = conv,
            IdempotencyKey = "idem-crash-1",
            Direccion = DireccionMensajeWhatsApp.Saliente,
            Tipo = TipoMensajeWhatsApp.Texto,
            ContenidoTexto = "Mensaje durante caída",
            Estado = EstadoMensajeWhatsApp.Pendiente,
            Activo = true
        };
        var outbox = new OutboxMensajeWhatsApp
        {
            MensajeWhatsApp = msg,
            ConversacionId = conv.Id,
            DestinatarioE164 = conv.TelefonoE164,
            ContenidoTexto = msg.ContenidoTexto,
            IdempotencyKey = "idem-crash-1",
            Estado = EstadoOutboxWhatsApp.Procesando,
            Intentos = 1,
            ReservadoHastaUtc = DateTime.UtcNow.AddMinutes(-5), // Reserva expirada por caída del proceso
            Activo = true
        };
        db.ConversacionesWhatsApp.Add(conv);
        db.MensajesWhatsApp.Add(msg);
        db.OutboxMensajesWhatsApp.Add(outbox);
        await db.SaveChangesAsync();

        var fakeProvider = new FakeWhatsAppProvider();
        var settingsMonitor = new StaticOptionsMonitor<WhatsAppSettings>(CrearSettings(enabled: true));
        var outboxService = new WhatsAppOutboxService(db, fakeProvider, settingsMonitor, NullLogger<WhatsAppOutboxService>.Instance);

        var despachados = await outboxService.ProcesarLotePendienteAsync();

        Assert.Equal(0, despachados);
        Assert.Equal(0, fakeProvider.TotalLlamadas); // Jamás reenvía ciegamente

        var outboxActualizado = await db.OutboxMensajesWhatsApp.SingleAsync(o => o.Id == outbox.Id);
        var msgActualizado = await db.MensajesWhatsApp.SingleAsync(m => m.Id == msg.Id);
        Assert.Equal(EstadoOutboxWhatsApp.Indeterminado, outboxActualizado.Estado);
        Assert.Equal(EstadoMensajeWhatsApp.Indeterminado, msgActualizado.Estado);
        Assert.Contains("Reserva expirada", outboxActualizado.HistorialIntentosJson ?? string.Empty);
    }

    [Fact]
    public async Task T23_ConfiguracionMetaIncompleta_BloqueaEnvioEnFailClosedYClasificaErroresHttp()
    {
        // Clasificación conservadora de códigos HTTP Meta
        Assert.Equal(TipoResultadoEnvioWhatsApp.ErrorReintentableSeguro, MetaWhatsAppProvider.ClasificarErrorHttp(HttpStatusCode.TooManyRequests));
        Assert.Equal(TipoResultadoEnvioWhatsApp.ErrorReintentableSeguro, MetaWhatsAppProvider.ClasificarErrorHttp(HttpStatusCode.ServiceUnavailable));
        Assert.Equal(TipoResultadoEnvioWhatsApp.ResultadoAmbiguo, MetaWhatsAppProvider.ClasificarErrorHttp(HttpStatusCode.GatewayTimeout));
        Assert.Equal(TipoResultadoEnvioWhatsApp.ResultadoAmbiguo, MetaWhatsAppProvider.ClasificarErrorHttp(HttpStatusCode.InternalServerError));
        Assert.Equal(TipoResultadoEnvioWhatsApp.ErrorDefinitivo, MetaWhatsAppProvider.ClasificarErrorHttp(HttpStatusCode.BadRequest));
        Assert.Equal(TipoResultadoEnvioWhatsApp.ErrorDefinitivo, MetaWhatsAppProvider.ClasificarErrorHttp(HttpStatusCode.Unauthorized));

        // Activación accidental con Provider=Meta pero sin AppSecret ni AccessToken -> rechazado antes de crear Outbox
        var dbName = Guid.NewGuid().ToString();
        using var db = CrearContexto(dbName);
        var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987654321", Activo = true };
        db.ConversacionesWhatsApp.Add(conv);
        await db.SaveChangesAsync();

        var incompleto = new WhatsAppSettings
        {
            Enabled = true,
            Provider = WhatsAppSettings.ProviderMeta,
            PhoneNumberId = "100000000000001",
            AccessToken = "",
            AppSecret = "",
            WebhookVerifyToken = ""
        };
        var monitor = new StaticOptionsMonitor<WhatsAppSettings>(incompleto);
        var fakeProvider = new FakeWhatsAppProvider();
        var outboxSvc = new WhatsAppOutboxService(db, fakeProvider, monitor, NullLogger<WhatsAppOutboxService>.Instance);
        var identSvc = new WhatsAppClienteIdentificacionService(db, monitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
        var service = new WhatsAppService(db, outboxSvc, identSvc, new AuditoriaEnMemoria(), monitor);

        var res = await service.EnviarMensajeManualAsync(conv.Id, new WhatsAppEnviarMensajeRequest("No debe salir"), Guid.NewGuid());
        Assert.False(res.IsSuccess);
        Assert.Equal(0, fakeProvider.TotalLlamadas);
        Assert.Equal(0, await db.MensajesWhatsApp.CountAsync());
        Assert.Equal(0, await db.OutboxMensajesWhatsApp.CountAsync());
    }
}
