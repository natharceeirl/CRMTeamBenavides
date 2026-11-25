using System.Net;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Api.Services.WhatsApp.IA;
using Npgsql;

namespace CRMTeamBenavides.Api.Configuration;

/// <summary>
/// Comprobaciones de seguridad de configuración para impedir el arranque en entornos
/// no locales (Staging / Production) con marcadores placeholder, secretos débiles o
/// parámetros de producción reutilizados accidentalmente en QA (Staging).
/// Jamás incluye valores de secretos ni cadenas de conexión en los mensajes de excepción.
/// </summary>
public static class ValidacionConfiguracionEntorno
{
    public const int LongitudMinimaJwtSecret = 48;
    public const int LongitudMinimaDbPassword = 32;
    public const int MinimoCaracteresDistintosSecreto = 6;

    public const string BaseDatosProduccion = "crm_teambenavides";
    public const string UsuarioBaseDatosProduccion = "crm";
    public const string SubredDockerProduccion = "172.30.0.0/24";

    public const string BaseDatosQaEsperada = "crm_teambenavides_qa";
    public const string UsuarioBaseDatosQaEsperado = "crm_qa";
    public const string SubredDockerQaEsperada = "172.31.0.0/24";

    // Marcadores explícitos de plantilla. No incluye subcadenas cortas ambiguas como "XXXX"
    // para evitar falsos positivos en claves aleatorias legítimas (QA-FIN-LOW-02).
    private static readonly string[] MarcadoresPlaceholder =
    [
        "REEMPLAZAR",
        "CAMBIAR_",
        "CAMBIAR-",
        "POR_CAMBIAR",
        "PLACEHOLDER",
        "CHANGEME",
        "CHANGE_ME",
        "TODO_",
        "TU_CLAVE",
        "TU_SECRETO",
        "TU_DOMINIO",
        "EJEMPLO_SECRETO",
        "VALOR_FICTICIO",
        "INSERTAR_AQUI"
    ];

    private static readonly string[] ClavesInsegurasConocidas =
    [
        "password",
        "123456",
        "12345678",
        "12345678901234567890123456789012",
        "123456789012345678901234567890123456789012345678",
        "postgres",
        "admin",
        "secret",
        "cambiar",
        "xxxx",
        "xxxxxxxx",
        "crm_teambenavides",
        "crm_teambenavides_qa"
    ];

    public static bool ContieneMarcadorPlaceholder(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var texto = valor.Trim();
        foreach (var marcador in MarcadoresPlaceholder)
        {
            if (texto.Contains(marcador, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var insegura in ClavesInsegurasConocidas)
        {
            if (string.Equals(texto, insegura, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool EsSecretoRepetitivoODebil(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return true;
        }

        var limpio = valor.Trim();
        if (limpio.StartsWith("1234567890", StringComparison.Ordinal) ||
            limpio.StartsWith("abcdefghij", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var distintos = limpio
            .Select(char.ToLowerInvariant)
            .Distinct()
            .Count();

        return distintos < MinimoCaracteresDistintosSecreto;
    }

    public static bool EsSecretoValido(string? valor, int longitudMinima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var limpio = valor.Trim();
        if (limpio.Length < longitudMinima)
        {
            return false;
        }

        if (ContieneMarcadorPlaceholder(limpio))
        {
            return false;
        }

        return !EsSecretoRepetitivoODebil(limpio);
    }

    /// <summary>
    /// Valida la configuración crítica antes de levantar la aplicación.
    /// En <c>Staging</c> (QA) y <c>Production</c> exige secretos reales sin placeholders ni patrones débiles.
    /// En <c>Staging</c> además exige la base y usuario exactos de QA, proxy confiable válido y
    /// que WhatsApp, IA y Yamaha permanezcan deshabilitados.
    /// </summary>
    public static void ValidarAlArrancar(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        if (environment.IsDevelopment())
        {
            return;
        }

        // 1. Validar JwtSettings:SecretKey (mínimo 48 caracteres y entropía suficiente)
        var jwtSecret = configuration["JwtSettings:SecretKey"];
        if (!EsSecretoValido(jwtSecret, LongitudMinimaJwtSecret))
        {
            throw new InvalidOperationException(
                $"Configuración rechazada en entorno {environment.EnvironmentName}: " +
                $"JwtSettings:SecretKey es obligatorio, debe tener al menos {LongitudMinimaJwtSecret} caracteres, " +
                "entropía suficiente (no repetitivo) y no contener marcadores placeholder.");
        }

        // 2. Validar cadena de conexión a PostgreSQL (sin exponer jamás su contenido en el error)
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) || ContieneMarcadorPlaceholder(connectionString))
        {
            throw new InvalidOperationException(
                $"Configuración rechazada en entorno {environment.EnvironmentName}: " +
                "ConnectionStrings:DefaultConnection es obligatoria y no puede contener marcadores placeholder.");
        }

        NpgsqlConnectionStringBuilder connBuilder;
        try
        {
            connBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception)
        {
            throw new InvalidOperationException(
                $"Configuración rechazada en entorno {environment.EnvironmentName}: " +
                "ConnectionStrings:DefaultConnection no tiene un formato válido de PostgreSQL.");
        }

        if (!EsSecretoValido(connBuilder.Password, LongitudMinimaDbPassword))
        {
            throw new InvalidOperationException(
                $"Configuración rechazada en entorno {environment.EnvironmentName}: " +
                $"La contraseña de PostgreSQL en DefaultConnection debe tener al menos {LongitudMinimaDbPassword} caracteres, " +
                "entropía suficiente (no repetitiva) y no ser un placeholder ni un valor inseguro.");
        }

        // 3. Reglas estrictas de aislamiento en Staging (QA) — QA-FIN-MED-03
        if (environment.IsStaging())
        {
            var dbName = connBuilder.Database?.Trim() ?? string.Empty;
            if (!string.Equals(dbName, BaseDatosQaEsperada, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Violación de aislamiento QA (Staging): la base de datos configurada " +
                    $"debe ser exactamente «{BaseDatosQaEsperada}» (se rechazó un nombre distinto o de producción).");
            }

            var dbUser = connBuilder.Username?.Trim() ?? string.Empty;
            if (!string.Equals(dbUser, UsuarioBaseDatosQaEsperado, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Violación de aislamiento QA (Staging): el usuario de PostgreSQL " +
                    $"debe ser exactamente «{UsuarioBaseDatosQaEsperado}» (se rechazó «{UsuarioBaseDatosProduccion}», «postgres» u otro).");
            }

            var redesConfiables = (configuration.GetSection(ProxyInverso.SeccionRedesConfiables).Get<string[]>() ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .ToList();

            if (redesConfiables.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Configuración rechazada en entorno Staging: {ProxyInverso.SeccionRedesConfiables} " +
                    $"es obligatorio y debe incluir la subred interna de QA ({SubredDockerQaEsperada}).");
            }

            foreach (var red in redesConfiables)
            {
                if (!IPNetwork.TryParse(red, out _))
                {
                    throw new InvalidOperationException(
                        $"Configuración rechazada en entorno Staging: {ProxyInverso.SeccionRedesConfiables} " +
                        "contiene una red que no está en formato CIDR válido.");
                }

                if (string.Equals(red, SubredDockerProduccion, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Violación de aislamiento QA (Staging): {ProxyInverso.SeccionRedesConfiables} contiene la subred de producción " +
                        $"({SubredDockerProduccion}). QA debe usar exclusivamente su subred ({SubredDockerQaEsperada}).");
                }
            }

            // En Staging, las integraciones externas deben permanecer deshabilitadas y en Mock
            var whatsAppEnabled = configuration.GetValue<bool>("WhatsApp:Enabled");
            var whatsAppProvider = configuration["WhatsApp:Provider"]?.Trim() ?? WhatsAppSettings.ProviderMock;
            if (whatsAppEnabled || !string.Equals(whatsAppProvider, WhatsAppSettings.ProviderMock, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Violación de seguridad QA (Staging): WhatsApp debe permanecer deshabilitado (WhatsApp:Enabled=false) " +
                    "y con proveedor Mock (WhatsApp:Provider=Mock).");
            }

            var whatsAppAiEnabled = configuration.GetValue<bool>("WhatsAppAi:Enabled");
            var whatsAppAiProvider = configuration["WhatsAppAi:Provider"]?.Trim() ?? WhatsAppAiSettings.ProviderMock;
            if (whatsAppAiEnabled || !string.Equals(whatsAppAiProvider, WhatsAppAiSettings.ProviderMock, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Violación de seguridad QA (Staging): WhatsAppAi debe permanecer deshabilitada (WhatsAppAi:Enabled=false) " +
                    "y con proveedor Mock (WhatsAppAi:Provider=Mock).");
            }

            var yamahaEnabled = configuration.GetValue<bool>("YamahaApi:Enabled");
            if (yamahaEnabled)
            {
                throw new InvalidOperationException(
                    "Violación de seguridad QA (Staging): YamahaApi debe permanecer deshabilitado (YamahaApi:Enabled=false) en QA.");
            }
        }

        // 4. Validar que no existan placeholders en integraciones externas si están presentes
        ValidarSinPlaceholdersEnOpcionales(configuration, environment.EnvironmentName,
            "WhatsApp:AccessToken",
            "WhatsApp:AppSecret",
            "WhatsApp:WebhookVerifyToken",
            "WhatsApp:PhoneNumberId",
            "WhatsApp:BusinessAccountId",
            "WhatsAppAi:ApiKey",
            "WhatsAppAi:ApiBaseUrl",
            "YamahaApi:FunctionKey",
            "YamahaApi:VentasFunctionKey",
            "YamahaApi:StockFunctionKey",
            "YamahaApi:ServicioTecnicoFunctionKey",
            "Decolecta:Token");
    }

    private static void ValidarSinPlaceholdersEnOpcionales(
        IConfiguration configuration,
        string environmentName,
        params string[] claves)
    {
        foreach (var clave in claves)
        {
            var valor = configuration[clave];
            if (!string.IsNullOrWhiteSpace(valor) && ContieneMarcadorPlaceholder(valor))
            {
                throw new InvalidOperationException(
                    $"Configuración rechazada en entorno {environmentName}: " +
                    $"la clave «{clave}» contiene un marcador placeholder no permitido.");
            }
        }
    }
}
