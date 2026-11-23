using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CRMTeamBenavides.Api.Services.WhatsApp.IA;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntencionWhatsApp
{
    Saludo = 0,
    ConsultaGeneralTaller = 1,
    PreguntaFrecuente = 2,
    ConsultaCatalogoProducto = 3,
    ConsultaCatalogoServicio = 4,
    ConsultaOrdenServicioPersonal = 5,
    ConsultaVehiculoPersonal = 6,
    ConsultaCitaPersonal = 7,
    ConsultaDatosPersonales = 8,
    SolicitudAgenteHumano = 9,
    IntentoManipulacionPrompt = 10,
    FueraDeAlcanceOAmbigua = 11
}

public sealed record ResultadoClasificacionIntencion(
    IntencionWhatsApp Intencion,
    bool RequiereIdentidadVerificada,
    bool EsIntentoPromptInjection,
    bool RequiereEscalamientoHumano,
    string? TerminoBusquedaExtraido,
    string MotivoClasificacion);

/// <summary>
/// Clasifica la intención de un mensaje entrante de WhatsApp y detecta intentos de
/// prompt injection o solicitudes que requieren datos personales / derivación humana.
/// </summary>
public interface IWhatsAppIntentClassifier
{
    ResultadoClasificacionIntencion Clasificar(string? textoMensaje);
}

public sealed partial class WhatsAppIntentClassifier : IWhatsAppIntentClassifier
{
    private static readonly string[] PatronesPromptInjection =
    [
        "ignora las instrucciones",
        "ignora tus instrucciones",
        "ignora todas las instrucciones",
        "olvida las instrucciones",
        "olvida tus reglas",
        "ignore previous instructions",
        "ignore all instructions",
        "disregard previous",
        "system prompt",
        "prompt del sistema",
        "modo administrador",
        "modo admin",
        "administrativointerno",
        "actua como administrador",
        "soy el administrador",
        "soy gerencia",
        "soy tecnico",
        "dame acceso admin",
        "cambia tus permisos",
        "salta la verificacion",
        "omite la verificacion",
        "bypass",
        "override",
        "jailbreak",
        "select * from",
        "drop table",
        "ejecuta sql",
        "consulta sql",
        "dame el costo",
        "dame los costos",
        "precio de costo",
        "margen de ganancia",
        "datos de otro cliente",
        "orden de otro cliente",
        "todos los clientes",
        "lista de clientes",
        "access token",
        "api key",
        "app secret"
    ];

    private static readonly string[] FrasesEscalamientoHumano =
    [
        "hablar con un asesor",
        "hablar con asesor",
        "hablar con una persona",
        "hablar con alguien",
        "asesor humano",
        "agente humano",
        "atencion humana",
        "persona real",
        "quiero un asesor",
        "necesito un asesor",
        "comunicame con",
        "derivame",
        "pasame con",
        "reclamo",
        "queja",
        "libro de reclamaciones",
        "emergencia",
        "urgente"
    ];

    private static readonly string[] PalabrasOrdenPersonal =
    [
        "mi orden",
        "mis ordenes",
        "estado de orden",
        "estado de mi orden",
        "orden de servicio",
        "como va mi moto",
        "como va mi vehiculo",
        "como va mi unidad",
        "ya esta lista mi moto",
        "ya esta listo mi",
        "cuando entregan mi",
        "cuando recojo mi",
        "mi reparacion",
        "mi mantenimiento en taller",
        "mi presupuesto"
    ];

    private static readonly string[] PalabrasVehiculoPersonal =
    [
        "mi vehiculo",
        "mis vehiculos",
        "mi moto registrada",
        "mis motos",
        "mi placa",
        "mi kilometraje",
        "mi motor fuera de borda",
        "mis unidades"
    ];

    private static readonly string[] PalabrasCitaPersonal =
    [
        "mi cita",
        "mis citas",
        "cita programada",
        "tengo cita",
        "hora de mi cita",
        "confirmar mi cita",
        "reprogramar mi cita",
        "cancelar mi cita"
    ];

    private static readonly string[] PalabrasDatosPersonales =
    [
        "mi perfil",
        "mis datos",
        "mi dni",
        "mi ruc",
        "mi historial",
        "mi factura",
        "mi boleta",
        "cuanto debo",
        "mi saldo",
        "mi telefono registrado"
    ];

    private static readonly string[] PalabrasCatalogoServicio =
    [
        "mantenimiento",
        "afinamiento",
        "cambio de aceite",
        "diagnostico",
        "reparacion",
        "revision tecnica",
        "instalacion",
        "servicio de",
        "servicios del taller",
        "que servicios",
        "tarifa",
        "mano de obra",
        "planchado",
        "frenos"
    ];

    private static readonly string[] PalabrasCatalogoProducto =
    [
        "precio",
        "precios",
        "cuesta",
        "cotizar",
        "cotizacion",
        "repuesto",
        "repuestos",
        "aceite",
        "yamalube",
        "filtro",
        "bujia",
        "pastilla",
        "pastillas",
        "llanta",
        "llantas",
        "bateria",
        "cadena",
        "kit de arrastre",
        "casco",
        "producto",
        "productos",
        "catalogo",
        "stock",
        "disponible",
        "tienen en venta",
        "venden"
    ];

    private static readonly string[] PalabrasInfoTaller =
    [
        "horario",
        "horarios",
        "hora atienden",
        "hora abren",
        "hora cierran",
        "direccion",
        "ubicacion",
        "donde quedan",
        "donde estan",
        "como llegar",
        "telefono",
        "correo",
        "email",
        "contacto",
        "garantia",
        "metodos de pago",
        "medios de pago",
        "aceptan tarjeta",
        "aceptan yape",
        "aceptan plin",
        "yamaha",
        "sabado",
        "domingo"
    ];

    private static readonly HashSet<string> SaludosPuros = new(StringComparer.Ordinal)
    {
        "hola", "holi", "buenas", "buenos dias", "buenas tardes", "buenas noches",
        "saludos", "hey", "que tal", "buen dia"
    };

    private static readonly HashSet<string> StopWordsBusqueda = new(StringComparer.Ordinal)
    {
        "hola", "buenos", "buenas", "dias", "tardes", "noches", "quiero", "necesito",
        "saber", "precio", "precios", "cuanto", "cuesta", "cuestan", "tiene", "tienen", "venden",
        "disponible", "disponibilidad", "stock", "catalogo", "producto", "productos",
        "repuesto", "repuestos", "servicio", "servicios", "taller", "para", "sobre",
        "cotizar", "cotizacion", "favor", "por", "con", "sin", "del", "las", "los",
        "una", "uno", "unos", "unas", "que", "hay", "ofrecen", "hacen", "tarifa", "tarifas",
        "cual", "cuales", "como", "donde"
    };

    [GeneratedRegex(@"\bos[\s\-]?\d{3,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex PatronNumeroOrdenRegex();

    [GeneratedRegex(@"\bcit[\s\-]?\d{3,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex PatronNumeroCitaRegex();

    public ResultadoClasificacionIntencion Clasificar(string? textoMensaje)
    {
        if (string.IsNullOrWhiteSpace(textoMensaje))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.FueraDeAlcanceOAmbigua,
                RequiereIdentidadVerificada: false,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: false,
                TerminoBusquedaExtraido: null,
                MotivoClasificacion: "MENSAJE_SIN_TEXTO");
        }

        var norm = NormalizarTexto(textoMensaje);
        if (string.IsNullOrWhiteSpace(norm))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.FueraDeAlcanceOAmbigua,
                RequiereIdentidadVerificada: false,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: false,
                TerminoBusquedaExtraido: null,
                MotivoClasificacion: "MENSAJE_SOLO_SIMBOLOS");
        }

        // 1. Primera barrera: detección de prompt injection / intento de evadir políticas o extraer datos internos.
        if (PatronesPromptInjection.Any(p => norm.Contains(p, StringComparison.Ordinal)))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.IntentoManipulacionPrompt,
                RequiereIdentidadVerificada: false,
                EsIntentoPromptInjection: true,
                RequiereEscalamientoHumano: false,
                TerminoBusquedaExtraido: null,
                MotivoClasificacion: "PROMPT_INJECTION_DETECTADO");
        }

        // 2. Solicitud explícita de atención humana o reclamo -> escalamiento directo.
        if (FrasesEscalamientoHumano.Any(f => norm.Contains(f, StringComparison.Ordinal)))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.SolicitudAgenteHumano,
                RequiereIdentidadVerificada: false,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: true,
                TerminoBusquedaExtraido: null,
                MotivoClasificacion: "SOLICITUD_HUMANO");
        }

        // 3. Solicitudes de información personal (SIEMPRE exigen verificación real de identidad).
        if (PatronNumeroOrdenRegex().IsMatch(norm)
            || PalabrasOrdenPersonal.Any(p => norm.Contains(p, StringComparison.Ordinal)))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.ConsultaOrdenServicioPersonal,
                RequiereIdentidadVerificada: true,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: true,
                TerminoBusquedaExtraido: null,
                MotivoClasificacion: "CONSULTA_ORDEN_PERSONAL");
        }

        if (PatronNumeroCitaRegex().IsMatch(norm)
            || PalabrasCitaPersonal.Any(p => norm.Contains(p, StringComparison.Ordinal)))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.ConsultaCitaPersonal,
                RequiereIdentidadVerificada: true,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: true,
                TerminoBusquedaExtraido: null,
                MotivoClasificacion: "CONSULTA_CITA_PERSONAL");
        }

        if (PalabrasVehiculoPersonal.Any(p => norm.Contains(p, StringComparison.Ordinal)))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.ConsultaVehiculoPersonal,
                RequiereIdentidadVerificada: true,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: true,
                TerminoBusquedaExtraido: null,
                MotivoClasificacion: "CONSULTA_VEHICULO_PERSONAL");
        }

        if (PalabrasDatosPersonales.Any(p => norm.Contains(p, StringComparison.Ordinal)))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.ConsultaDatosPersonales,
                RequiereIdentidadVerificada: true,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: true,
                TerminoBusquedaExtraido: null,
                MotivoClasificacion: "CONSULTA_DATOS_PERSONALES");
        }

        // 4. Saludo puro sin consulta adicional.
        if (SaludosPuros.Contains(norm))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.Saludo,
                RequiereIdentidadVerificada: false,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: false,
                TerminoBusquedaExtraido: null,
                MotivoClasificacion: "SALUDO");
        }

        // 5. Consultas de catálogo público: productos/repuestos vs servicios de taller.
        var coincideProducto = PalabrasCatalogoProducto.Any(p => norm.Contains(p, StringComparison.Ordinal));
        var coincideServicio = PalabrasCatalogoServicio.Any(p => norm.Contains(p, StringComparison.Ordinal));

        if (coincideServicio && !coincideProducto)
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.ConsultaCatalogoServicio,
                RequiereIdentidadVerificada: false,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: false,
                TerminoBusquedaExtraido: ExtraerTerminoBusqueda(norm),
                MotivoClasificacion: "CATALOGO_SERVICIOS");
        }

        if (coincideProducto)
        {
            // Si pide precio de un servicio explícito (ej. "precio de mantenimiento"), clasificar como servicio.
            if (coincideServicio && (norm.Contains("mantenimiento", StringComparison.Ordinal)
                || norm.Contains("afinamiento", StringComparison.Ordinal)
                || norm.Contains("diagnostico", StringComparison.Ordinal)
                || norm.Contains("mano de obra", StringComparison.Ordinal)))
            {
                return new ResultadoClasificacionIntencion(
                    IntencionWhatsApp.ConsultaCatalogoServicio,
                    RequiereIdentidadVerificada: false,
                    EsIntentoPromptInjection: false,
                    RequiereEscalamientoHumano: false,
                    TerminoBusquedaExtraido: ExtraerTerminoBusqueda(norm),
                    MotivoClasificacion: "CATALOGO_SERVICIOS_PRECIO");
            }

            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.ConsultaCatalogoProducto,
                RequiereIdentidadVerificada: false,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: false,
                TerminoBusquedaExtraido: ExtraerTerminoBusqueda(norm),
                MotivoClasificacion: "CATALOGO_PRODUCTOS");
        }

        // 6. Información general del taller y preguntas frecuentes.
        if (PalabrasInfoTaller.Any(p => norm.Contains(p, StringComparison.Ordinal)))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.ConsultaGeneralTaller,
                RequiereIdentidadVerificada: false,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: false,
                TerminoBusquedaExtraido: ExtraerTerminoBusqueda(norm),
                MotivoClasificacion: "INFO_GENERAL_TALLER");
        }

        // 7. Mensajes con contenido suficiente se intentan contrastar primero contra FAQs autorizadas.
        var termino = ExtraerTerminoBusqueda(norm);
        if (!string.IsNullOrWhiteSpace(termino))
        {
            return new ResultadoClasificacionIntencion(
                IntencionWhatsApp.PreguntaFrecuente,
                RequiereIdentidadVerificada: false,
                EsIntentoPromptInjection: false,
                RequiereEscalamientoHumano: false,
                TerminoBusquedaExtraido: termino,
                MotivoClasificacion: "CONSULTA_FAQ_ABIERTA");
        }

        return new ResultadoClasificacionIntencion(
            IntencionWhatsApp.FueraDeAlcanceOAmbigua,
            RequiereIdentidadVerificada: false,
            EsIntentoPromptInjection: false,
            RequiereEscalamientoHumano: false,
            TerminoBusquedaExtraido: null,
            MotivoClasificacion: "CONSULTA_AMBIGUA");
    }

    private static string? ExtraerTerminoBusqueda(string textoNormalizado)
    {
        var palabras = textoNormalizado
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !StopWordsBusqueda.Contains(w))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();

        return palabras.Count > 0 ? string.Join(" ", palabras) : null;
    }

    private static string NormalizarTexto(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        bool espacioPrevio = true;

        foreach (var c in texto.Trim().ToLowerInvariant())
        {
            char mapped = c switch
            {
                'á' or 'à' or 'ä' => 'a',
                'é' or 'è' or 'ë' => 'e',
                'í' or 'ì' or 'ï' => 'i',
                'ó' or 'ò' or 'ö' => 'o',
                'ú' or 'ù' or 'ü' => 'u',
                'ñ' => 'n',
                _ when char.IsLetterOrDigit(c) || c == '-' => c,
                _ => ' '
            };

            if (mapped == ' ')
            {
                if (!espacioPrevio)
                {
                    sb.Append(' ');
                    espacioPrevio = true;
                }
            }
            else
            {
                sb.Append(mapped);
                espacioPrevio = false;
            }
        }

        return sb.ToString().Trim();
    }
}
