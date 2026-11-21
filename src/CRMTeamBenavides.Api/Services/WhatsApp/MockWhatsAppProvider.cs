using System.Collections.Concurrent;
using CRMTeamBenavides.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

/// <summary>
/// Implementación simulada local de <see cref="IWhatsAppProvider"/> para desarrollo y pruebas
/// sin llamadas externas ni envío real de mensajes por WhatsApp.
/// </summary>
public sealed class MockWhatsAppProvider : IWhatsAppProvider
{
    private readonly ILogger<MockWhatsAppProvider> _logger;
    private readonly ConcurrentQueue<WhatsAppSendTextRequest> _enviosSimulados = new();

    public MockWhatsAppProvider(ILogger<MockWhatsAppProvider> logger)
    {
        _logger = logger;
    }

    public string NombreProveedor => WhatsAppSettings.ProviderMock;

    /// <summary>Mensajes despachados en memoria durante la vida de esta instancia (solo diagnóstico/pruebas).</summary>
    public IReadOnlyCollection<WhatsAppSendTextRequest> EnviosSimulados => _enviosSimulados.ToArray();

    public Task<WhatsAppSendResult> EnviarTextoAsync(
        WhatsAppSendTextRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var norm = WhatsAppPhoneNormalizer.NormalizarE164(request.DestinatarioE164);
        if (!norm.EsValido)
        {
            return Task.FromResult(new WhatsAppSendResult(
                Exitoso: false,
                ExternalMessageId: null,
                CodigoHttp: 400,
                Error: norm.Error,
                Clasificacion: TipoResultadoEnvioWhatsApp.ErrorDefinitivo,
                Proveedor: NombreProveedor));
        }

        if (string.IsNullOrWhiteSpace(request.Texto))
        {
            return Task.FromResult(new WhatsAppSendResult(
                Exitoso: false,
                ExternalMessageId: null,
                CodigoHttp: 400,
                Error: "El contenido de texto del mensaje es obligatorio.",
                Clasificacion: TipoResultadoEnvioWhatsApp.ErrorDefinitivo,
                Proveedor: NombreProveedor));
        }

        var wamidSimulado = $"wamid.mock.{Guid.NewGuid():N}";
        _enviosSimulados.Enqueue(request);

        // No registrar el texto completo del mensaje en logs para proteger información sensible.
        _logger.LogInformation(
            "WhatsApp [Mock] mensaje simulado despachado. IdempotencyKey={IdempotencyKey}, ExternalId={ExternalId}, LongitudTexto={Longitud}",
            request.IdempotencyKey,
            wamidSimulado,
            request.Texto.Length);

        return Task.FromResult(new WhatsAppSendResult(
            Exitoso: true,
            ExternalMessageId: wamidSimulado,
            CodigoHttp: 200,
            Error: null,
            Clasificacion: TipoResultadoEnvioWhatsApp.Exitoso,
            Proveedor: NombreProveedor));
    }
}
