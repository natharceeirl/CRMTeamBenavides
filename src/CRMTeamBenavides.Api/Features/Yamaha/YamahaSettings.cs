namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Configuración tipada para el conector Yamaha API.
/// Los valores reales deben proveerse en User Secrets o variables de entorno —
/// nunca en appsettings.json (que se versiona en Git).
/// </summary>
public class YamahaSettings
{
    public const string SectionName = "YamahaApi";

    /// <summary>URL base de la API de Yamaha, sin barra final. Ej: "https://api.yamaha.example.com/v1"</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Tiempo de espera por petición, en segundos. Valor por defecto: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Nombre del encabezado HTTP de autenticación.
    /// El esquema exacto (API Key, Bearer token, Basic, u otro) debe confirmarse
    /// con la documentación oficial de Yamaha antes de completar este valor.
    /// Ejemplo: "X-Api-Key", "Authorization", etc.
    /// </summary>
    public string AuthHeaderName { get; set; } = string.Empty;

    /// <summary>
    /// Valor del encabezado HTTP de autenticación.
    /// El formato exacto depende del esquema que use Yamaha (token, credencial, etc.).
    /// DEBE proveerse únicamente en User Secrets o variables de entorno.
    /// </summary>
    public string AuthHeaderValue { get; set; } = string.Empty;
}
