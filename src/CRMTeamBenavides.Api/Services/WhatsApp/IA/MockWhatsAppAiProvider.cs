using System.Globalization;
using System.Text;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp.IA;

/// <summary>
/// Proveedor de IA determinista local para pruebas y desarrollo de Fase 3.
/// No realiza llamadas externas ni inventa información: responde estrictamente
/// a partir de <see cref="WhatsAppAiSolicitud.ContextoAutorizado"/>.
/// </summary>
public sealed class MockWhatsAppAiProvider : IWhatsAppAiProvider
{
    private readonly IOptionsMonitor<WhatsAppAiSettings> _settingsMonitor;

    public MockWhatsAppAiProvider(IOptionsMonitor<WhatsAppAiSettings> settingsMonitor)
    {
        _settingsMonitor = settingsMonitor;
    }

    public string NombreProveedor => WhatsAppAiSettings.ProviderMock;

    public Task<WhatsAppAiRespuesta> GenerarRespuestaAsync(
        WhatsAppAiSolicitud solicitud,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var settings = _settingsMonitor.CurrentValue;
        var modelo = string.IsNullOrWhiteSpace(settings.Model) ? "mock-deterministic-v1" : settings.Model.Trim();
        var ctx = solicitud.ContextoAutorizado;
        var clasif = solicitud.Clasificacion;
        var nombreTaller = ctx.InformacionTaller?.NombreTaller ?? "Team Benavides";

        string texto;
        bool escalar = clasif.RequiereEscalamientoHumano;

        if (clasif.EsIntentoPromptInjection || clasif.Intencion == IntencionWhatsApp.IntentoManipulacionPrompt)
        {
            texto = $"Solo puedo ayudarte con información general autorizada de {nombreTaller}, catálogo público de productos y servicios, o comunicarte con un asesor del taller. No tengo acceso a configuración interna, costos ni datos de otros clientes.";
            escalar = false;
        }
        else if (clasif.RequiereIdentidadVerificada || ctx.ConsultaPersonalBloqueadaPorFaltaVerificacion)
        {
            // Regla obligatoria: jamás exponer órdenes, citas, vehículos ni datos personales sin verificación aprobada.
            texto = $"Por seguridad y protección de tus datos personales, no puedo consultar órdenes de servicio, vehículos, citas ni información privada de forma automática por este chat sin una verificación de identidad aprobada. Te derivo con un asesor de {nombreTaller} para ayudarte personalmente.";
            escalar = true;
        }
        else if (clasif.Intencion == IntencionWhatsApp.SolicitudAgenteHumano)
        {
            texto = $"Con gusto te comunico con un asesor humano de {nombreTaller}. Un miembro de nuestro equipo continuará tu atención por este mismo chat en breve.";
            escalar = true;
        }
        else if (clasif.Intencion == IntencionWhatsApp.Saludo)
        {
            texto = $"¡Hola! Bienvenido a {nombreTaller}. Puedo ayudarte con información general del taller, preguntas frecuentes, catálogo referencial de repuestos y servicios, o comunicarte con un asesor humano. ¿En qué te ayudo hoy?";
            escalar = false;
        }
        else if (clasif.Intencion == IntencionWhatsApp.ConsultaCatalogoProducto)
        {
            (texto, escalar) = ConstruirRespuestaProductos(nombreTaller, ctx.ProductosPublicos);
        }
        else if (clasif.Intencion == IntencionWhatsApp.ConsultaCatalogoServicio)
        {
            (texto, escalar) = ConstruirRespuestaServicios(nombreTaller, ctx.ServiciosPublicos);
        }
        else if (clasif.Intencion is IntencionWhatsApp.PreguntaFrecuente or IntencionWhatsApp.ConsultaGeneralTaller)
        {
            (texto, escalar) = ConstruirRespuestaGeneralYFaq(nombreTaller, clasif.Intencion, ctx);
        }
        else
        {
            texto = $"No pude identificar con precisión tu consulta. Puedo orientarte sobre servicios, repuestos o información general de {nombreTaller}. Si prefieres atención personalizada, responde indicando que deseas hablar con un asesor.";
            escalar = false;
        }

        var maxChars = Math.Clamp(solicitud.MaxOutputChars, 40, 4096);
        var maxCharsPorTokens = Math.Clamp(solicitud.MaxOutputTokens, 20, 1024) * 4;
        var limiteEfectivo = Math.Min(maxChars, maxCharsPorTokens);
        if (texto.Length > limiteEfectivo)
        {
            texto = texto[..(limiteEfectivo - 3)].TrimEnd() + "...";
        }

        var tokensEntrada = EstimarTokens(solicitud.MensajeUsuarioNoConfiable);
        var tokensSalida = EstimarTokens(texto);

        return Task.FromResult(new WhatsAppAiRespuesta(
            Exitoso: true,
            TextoRespuesta: texto,
            SugiereEscalamientoHumano: escalar,
            Proveedor: NombreProveedor,
            Modelo: modelo,
            TokensEstimadosEntrada: tokensEntrada,
            TokensEstimadosSalida: tokensSalida));
    }

    private static (string Texto, bool Escalar) ConstruirRespuestaProductos(
        string nombreTaller,
        IReadOnlyList<WhatsAppProductoCatalogoResponse> productos)
    {
        if (productos.Count == 0)
        {
            return (
                $"No encontré repuestos o productos coincidentes en nuestro catálogo público en este momento. Si deseas una cotización exacta o verificar compatibilidad, puedo comunicarte con un asesor de {nombreTaller}.",
                true);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Encontré estas opciones referenciales en el catálogo de {nombreTaller}:");

        foreach (var p in productos.Take(5))
        {
            var disponibilidad = p.Disponible ? "Disponible" : "Sin stock por el momento";
            var precioTexto = p.PrecioDisponible && p.PrecioReferencialConIgvSoles.HasValue
                ? $"S/ {p.PrecioReferencialConIgvSoles.Value.ToString("0.00", CultureInfo.InvariantCulture)} (incl. IGV)"
                : "Precio a confirmar con un asesor";

            var marca = string.IsNullOrWhiteSpace(p.Marca) ? string.Empty : $" ({p.Marca})";
            sb.AppendLine($"- {p.Nombre}{marca} [{p.Codigo}]: {precioTexto} — {disponibilidad}.");
        }

        sb.Append("Los precios son referenciales. Si deseas cotizar o separar un repuesto, indícame para comunicarte con un asesor.");
        return (sb.ToString(), false);
    }

    private static (string Texto, bool Escalar) ConstruirRespuestaServicios(
        string nombreTaller,
        IReadOnlyList<WhatsAppServicioCatalogoResponse> servicios)
    {
        if (servicios.Count == 0)
        {
            return (
                $"No encontré un servicio exacto con ese detalle en nuestro catálogo público. Te puedo derivar con un asesor de {nombreTaller} para brindarte una cotización precisa según tu unidad.",
                true);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Estos son servicios referenciales disponibles en {nombreTaller}:");

        foreach (var s in servicios.Take(5))
        {
            string precioTexto;
            if (s.PrecioDisponible && s.PrecioReferencialConIgv.HasValue)
            {
                var notaIgv = s.TipoAfectacionIgv == TipoAfectacionIgv.Gravado ? " (incl. IGV)" : $" ({s.TipoAfectacionIgv})";
                precioTexto = $"S/ {s.PrecioReferencialConIgv.Value.ToString("0.00", CultureInfo.InvariantCulture)}{notaIgv}";
            }
            else
            {
                precioTexto = "Tarifa a confirmar con un asesor";
            }

            sb.AppendLine($"- {s.Nombre}: {precioTexto}.");
        }

        sb.Append("Si deseas agendar o solicitar un presupuesto exacto, indícame y te comunico con un asesor.");
        return (sb.ToString(), false);
    }

    private static (string Texto, bool Escalar) ConstruirRespuestaGeneralYFaq(
        string nombreTaller,
        IntencionWhatsApp intencion,
        WhatsAppAiContextoAutorizado ctx)
    {
        if (ctx.FaqsCoincidentes.Count > 0)
        {
            var principal = ctx.FaqsCoincidentes[0];
            var respuesta = principal.Respuesta.Trim();
            if (ctx.FaqsCoincidentes.Count > 1)
            {
                respuesta += " Si necesitas más detalles, puedes preguntarme o solicitar atención con un asesor.";
            }

            return (respuesta, false);
        }

        // Solo entregar los datos públicos de contacto del taller si la intención era explícitamente información general del taller.
        // Para preguntas abiertas sin FAQ coincidente, no inventar ni sustituir la respuesta: derivar a un asesor humano.
        if (intencion == IntencionWhatsApp.ConsultaGeneralTaller)
        {
            var info = ctx.InformacionTaller;
            var partes = new List<string>();
            if (!string.IsNullOrWhiteSpace(info?.Direccion))
            {
                partes.Add($"Dirección: {info.Direccion}");
            }
            if (!string.IsNullOrWhiteSpace(info?.TelefonoContacto))
            {
                partes.Add($"Teléfono: {info.TelefonoContacto}");
            }
            if (!string.IsNullOrWhiteSpace(info?.EmailContacto))
            {
                partes.Add($"Correo: {info.EmailContacto}");
            }

            if (partes.Count > 0)
            {
                return (
                    $"Información de contacto de {nombreTaller}: {string.Join(" | ", partes)}. Para consultas específicas que no estén en nuestras preguntas frecuentes, puedo comunicarte con un asesor.",
                    false);
            }
        }

        return (
            $"No encontré información autorizada exacta para tu consulta en este momento. Te derivo con un asesor de {nombreTaller} para ayudarte correctamente.",
            true);
    }

    private static int EstimarTokens(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? 0 : Math.Max(1, (texto.Length + 3) / 4);
}

/// <summary>
/// Proveedor de seguridad para configuraciones externas no activadas en Fase 3.
/// Garantiza que jamás se ejecuten llamadas HTTP a proveedores LLM externos mientras no exista
/// aprobación y configuración completa.
/// </summary>
public sealed class DisabledExternalWhatsAppAiProvider : IWhatsAppAiProvider
{
    public const string NombreProveedorDeshabilitado = "DisabledExternal";

    public string NombreProveedor => NombreProveedorDeshabilitado;

    public Task<WhatsAppAiRespuesta> GenerarRespuestaAsync(
        WhatsAppAiSolicitud solicitud,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(new WhatsAppAiRespuesta(
            Exitoso: false,
            TextoRespuesta: null,
            SugiereEscalamientoHumano: true,
            Proveedor: NombreProveedor,
            Modelo: "disabled",
            TokensEstimadosEntrada: 0,
            TokensEstimadosSalida: 0,
            CodigoError: "PROVIDER_DISABLED",
            ErrorSanitizado: "El proveedor externo de IA permanece deshabilitado por configuración en Fase 3."));
    }
}
