using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace CRMTeamBenavides.Api.Services.Notificaciones;

/// <summary>Sección «Notificaciones» de la configuración. Las credenciales van en user-secrets o variables de entorno.</summary>
public sealed class NotificacionesOptions
{
    public const string Seccion = "Notificaciones";

    /// <summary>«Registro» (desarrollo: solo se anota en el log) o «Firebase».</summary>
    public string Proveedor { get; set; } = "Registro";

    /// <summary>Cada cuánto el proceso en segundo plano revisa la cola.</summary>
    public int IntervaloSegundos { get; set; } = 10;

    /// <summary>Un aviso agrupable espera este tiempo sin cambios antes de salir, para juntar los que llegan seguidos.</summary>
    public int EsperaAgrupacionSegundos { get; set; } = 60;

    /// <summary>Ni agrupando espera más que esto.</summary>
    public int EsperaMaximaMinutos { get; set; } = 5;

    public int MaximoIntentos { get; set; } = 5;

    public FirebaseOptions Firebase { get; set; } = new();

    public sealed class FirebaseOptions
    {
        /// <summary>Ruta al JSON de la cuenta de servicio (desarrollo, en user-secrets).</summary>
        public string? CredencialesRuta { get; set; }

        /// <summary>El JSON completo de la cuenta de servicio (servidor, en una variable de entorno).</summary>
        public string? CredencialesJson { get; set; }
    }
}

public enum ResultadoPush
{
    Enviado,
    /// <summary>La app se desinstaló o el token venció: ese teléfono se deja de usar.</summary>
    TokenInvalido,
    /// <summary>Falla de red o de Firebase: se reintenta más tarde.</summary>
    ErrorTemporal
}

public sealed record MensajePush(string Token, string Titulo, string Cuerpo, IReadOnlyDictionary<string, string> Datos);

public sealed record RespuestaPush(ResultadoPush Resultado, string? Error = null);

/// <summary>Quien entrega el aviso al teléfono. Cambiar de proveedor no toca el resto del sistema.</summary>
public interface IProveedorPush
{
    Task<RespuestaPush> EnviarAsync(MensajePush mensaje, CancellationToken ct);
}

/// <summary>Modo de desarrollo: no sale nada del servidor, el aviso queda en el log y en la bandeja.</summary>
public sealed class PushSoloRegistro : IProveedorPush
{
    private readonly ILogger<PushSoloRegistro> _logger;

    public PushSoloRegistro(ILogger<PushSoloRegistro> logger)
    {
        _logger = logger;
    }

    public Task<RespuestaPush> EnviarAsync(MensajePush mensaje, CancellationToken ct)
    {
        _logger.LogInformation("Notificación (modo registro) a …{Token}: {Titulo} — {Cuerpo}",
            mensaje.Token.Length > 8 ? mensaje.Token[^8..] : mensaje.Token, mensaje.Titulo, mensaje.Cuerpo);
        return Task.FromResult(new RespuestaPush(ResultadoPush.Enviado));
    }
}

/// <summary>Firebase Cloud Messaging: Android directo e iPhone a través de la clave de Apple cargada en Firebase.</summary>
public sealed class PushFirebase : IProveedorPush
{
    /// <summary>El canal de Android que crea la app; ahí aparecen los avisos.</summary>
    public const string CanalAndroid = "avisos";

    private readonly FirebaseMessaging _mensajeria;

    public PushFirebase(GoogleCredential credencial)
    {
        var app = FirebaseApp.GetInstance("crm-team-benavides")
            ?? FirebaseApp.Create(new AppOptions { Credential = credencial }, "crm-team-benavides");
        _mensajeria = FirebaseMessaging.GetMessaging(app);
    }

    public async Task<RespuestaPush> EnviarAsync(MensajePush mensaje, CancellationToken ct)
    {
        // FirebaseAdmin 3.7 marca Token como obsoleto en favor del FID (identificador de
        // instalación), pero lo sigue enviando, y el token es lo que hoy entrega el plugin
        // de Flutter. Revisar al conectar Firebase si conviene pasar a FID.
#pragma warning disable CS0618
        var envio = new Message
        {
            Token = mensaje.Token,
#pragma warning restore CS0618
            Notification = new Notification { Title = mensaje.Titulo, Body = mensaje.Cuerpo },
            Data = mensaje.Datos,
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification { ChannelId = CanalAndroid }
            },
            Apns = new ApnsConfig { Aps = new Aps { Sound = "default" } }
        };

        try
        {
            await _mensajeria.SendAsync(envio, ct);
            return new RespuestaPush(ResultadoPush.Enviado);
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument or MessagingErrorCode.SenderIdMismatch)
        {
            return new RespuestaPush(ResultadoPush.TokenInvalido, ex.Message);
        }
        catch (FirebaseMessagingException ex)
        {
            return new RespuestaPush(ResultadoPush.ErrorTemporal, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return new RespuestaPush(ResultadoPush.ErrorTemporal, ex.Message);
        }
    }

    /// <summary>
    /// Firebase si está configurado y sus credenciales cargan; si no, el modo
    /// registro con una advertencia. Una falla de Firebase no detiene la API.
    /// </summary>
    public static IProveedorPush Crear(NotificacionesOptions opciones, ILoggerFactory registros)
    {
        var logger = registros.CreateLogger<PushFirebase>();
        if (!string.Equals(opciones.Proveedor, "Firebase", StringComparison.OrdinalIgnoreCase))
        {
            return new PushSoloRegistro(registros.CreateLogger<PushSoloRegistro>());
        }

        try
        {
            GoogleCredential credencial;
            if (!string.IsNullOrWhiteSpace(opciones.Firebase.CredencialesJson))
            {
                credencial = CredentialFactory.FromJson<ServiceAccountCredential>(opciones.Firebase.CredencialesJson).ToGoogleCredential();
            }
            else if (!string.IsNullOrWhiteSpace(opciones.Firebase.CredencialesRuta) && File.Exists(opciones.Firebase.CredencialesRuta))
            {
                credencial = CredentialFactory.FromFile<ServiceAccountCredential>(opciones.Firebase.CredencialesRuta).ToGoogleCredential();
            }
            else
            {
                logger.LogWarning("Notificaciones: se pidió Firebase pero faltan sus credenciales. Se usa el modo registro.");
                return new PushSoloRegistro(registros.CreateLogger<PushSoloRegistro>());
            }

            return new PushFirebase(credencial);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notificaciones: no se pudieron cargar las credenciales de Firebase. Se usa el modo registro.");
            return new PushSoloRegistro(registros.CreateLogger<PushSoloRegistro>());
        }
    }
}
