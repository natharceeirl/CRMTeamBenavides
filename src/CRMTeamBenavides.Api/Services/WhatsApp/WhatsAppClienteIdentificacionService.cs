using System.Text.Json.Serialization;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoIdentificacionClienteWhatsApp
{
    TelefonoInvalido = 0,
    NoEncontrado = 1,
    IdentificadoUnivoco = 2,
    Ambiguo = 3
}

public sealed record ResultadoIdentificacionClienteWhatsApp(
    EstadoIdentificacionClienteWhatsApp Estado,
    Guid? ClienteId,
    string? TelefonoE164Normalizado,
    int TotalCandidatos,
    string Motivo)
{
    public bool EsVinculableAutomaticamente =>
        Estado == EstadoIdentificacionClienteWhatsApp.IdentificadoUnivoco && ClienteId.HasValue;

    public bool EsAmbiguo =>
        Estado == EstadoIdentificacionClienteWhatsApp.Ambiguo;
}

/// <summary>
/// Servicio dedicado para identificar clientes activos del CRM a partir de un teléfono de WhatsApp.
/// Reglas estrictas:
/// - Normaliza a E.164 considerando el contexto peruano y prefijos internacionales.
/// - Solo acepta igualdad E.164 exacta (jamás coincidencias parciales por subcadena).
/// - Nunca crea ni fusiona clientes automáticamente.
/// - Ante múltiples coincidencias devuelve <see cref="EstadoIdentificacionClienteWhatsApp.Ambiguo"/> sin vincular.
/// </summary>
public interface IWhatsAppClienteIdentificacionService
{
    Task<ResultadoIdentificacionClienteWhatsApp> IdentificarPorTelefonoAsync(
        string? telefonoOriginal,
        CancellationToken ct = default);
}

public sealed class WhatsAppClienteIdentificacionService : IWhatsAppClienteIdentificacionService
{
    private readonly ApplicationDbContext _context;
    private readonly IOptionsMonitor<WhatsAppSettings> _settingsMonitor;
    private readonly ILogger<WhatsAppClienteIdentificacionService> _logger;

    public WhatsAppClienteIdentificacionService(
        ApplicationDbContext context,
        IOptionsMonitor<WhatsAppSettings> settingsMonitor,
        ILogger<WhatsAppClienteIdentificacionService> logger)
    {
        _context = context;
        _settingsMonitor = settingsMonitor;
        _logger = logger;
    }

    public async Task<ResultadoIdentificacionClienteWhatsApp> IdentificarPorTelefonoAsync(
        string? telefonoOriginal,
        CancellationToken ct = default)
    {
        var codigoPais = _settingsMonitor.CurrentValue.DefaultCountryCode;
        var norm = WhatsAppPhoneNormalizer.NormalizarE164(telefonoOriginal, codigoPais);

        if (!norm.EsValido || string.IsNullOrEmpty(norm.TelefonoE164))
        {
            var resInvalido = new ResultadoIdentificacionClienteWhatsApp(
                EstadoIdentificacionClienteWhatsApp.TelefonoInvalido,
                ClienteId: null,
                TelefonoE164Normalizado: null,
                TotalCandidatos: 0,
                Motivo: norm.Error ?? "El teléfono proporcionado no tiene formato E.164 válido.");

            _logger.LogInformation(
                "WhatsApp identificación cliente: Estado={Estado}, Candidatos=0, Motivo={Motivo}",
                resInvalido.Estado,
                resInvalido.Motivo);

            return resInvalido;
        }

        var telefonoObjetivo = norm.TelefonoE164;
        var waIdObjetivo = norm.WhatsAppId!;
        // Pre-filtro en SQL para evitar cargar todo el padrón de clientes en memoria:
        // para Perú (+51), cualquier formato válido (987654321, 0987654321, +51..., 054...)
        // contiene los dígitos locales sin el código de país '51' tras limpiar puntuación común.
        var digitosPrefiltroSql = waIdObjetivo.StartsWith("51", StringComparison.Ordinal) && waIdObjetivo.Length > 2
            ? waIdObjetivo[2..]
            : waIdObjetivo;

        // Proyección mínima y filtrado previo en SQL; la igualdad E.164 exacta se verifica luego sobre cada segmento.
        var clientesCandidatos = await _context.Clientes
            .AsNoTracking()
            .Where(c => c.Activo && c.Telefono != null && c.Telefono != "")
            .Where(c => c.Telefono!
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace(".", "")
                .Replace("+", "")
                .Replace("\t", "")
                .Contains(digitosPrefiltroSql))
            .Select(c => new { c.Id, c.Telefono })
            .ToListAsync(ct);

        var coincidencias = new List<Guid>();
        foreach (var candidato in clientesCandidatos)
        {
            var telefonosCliente = WhatsAppPhoneNormalizer.ExtraerTelefonosNormalizadosE164(
                candidato.Telefono,
                codigoPais);

            if (telefonosCliente.Contains(telefonoObjetivo, StringComparer.Ordinal))
            {
                coincidencias.Add(candidato.Id);
            }
        }

        ResultadoIdentificacionClienteWhatsApp resultado;
        if (coincidencias.Count == 0)
        {
            resultado = new ResultadoIdentificacionClienteWhatsApp(
                EstadoIdentificacionClienteWhatsApp.NoEncontrado,
                ClienteId: null,
                TelefonoE164Normalizado: telefonoObjetivo,
                TotalCandidatos: 0,
                Motivo: "No se encontró ningún cliente activo con el número telefónico normalizado.");
        }
        else if (coincidencias.Count == 1)
        {
            resultado = new ResultadoIdentificacionClienteWhatsApp(
                EstadoIdentificacionClienteWhatsApp.IdentificadoUnivoco,
                ClienteId: coincidencias[0],
                TelefonoE164Normalizado: telefonoObjetivo,
                TotalCandidatos: 1,
                Motivo: "Se identificó un único cliente activo coincidente de forma inequívoca.");
        }
        else
        {
            resultado = new ResultadoIdentificacionClienteWhatsApp(
                EstadoIdentificacionClienteWhatsApp.Ambiguo,
                ClienteId: null,
                TelefonoE164Normalizado: telefonoObjetivo,
                TotalCandidatos: coincidencias.Count,
                Motivo: $"Coincidencia ambigua: {coincidencias.Count} clientes activos comparten el mismo teléfono normalizado. Requiere vinculación manual.");
        }

        // Log estrictamente sin PII (sin teléfono, nombre ni documento).
        _logger.LogInformation(
            "WhatsApp identificación cliente: Estado={Estado}, Candidatos={Candidatos}, Vinculable={Vinculable}",
            resultado.Estado,
            resultado.TotalCandidatos,
            resultado.EsVinculableAutomaticamente);

        return resultado;
    }
}
