using System.Text;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services.WhatsApp.IA;

public sealed record WhatsAppInfoTallerPublica(
    string NombreTaller,
    string? Direccion,
    string? TelefonoContacto,
    string? EmailContacto,
    IReadOnlyList<string> CategoriasFaqDisponibles);

public sealed record WhatsAppFaqAutorizada(
    Guid Id,
    string Categoria,
    string Pregunta,
    string Respuesta);

/// <summary>
/// Fachada de herramientas CRM de SOLO LECTURA exclusiva para la capa de IA de WhatsApp (Fase 3).
/// Frontera obligatoria de seguridad:
/// - Fuerza SIEMPRE el contexto <see cref="WhatsAppModoConsultaCrm.ConversacionalCliente"/>.
/// - Jamás expone ni acepta <see cref="WhatsAppContextoConsultaCrm"/> ni <see cref="WhatsAppModoConsultaCrm.AdministrativoInterno"/>.
/// - Jamás permite operaciones de escritura, costos, márgenes ni acceso a datos personales sin verificación real aprobada.
/// </summary>
public interface IWhatsAppAiCrmToolsFacade
{
    Task<WhatsAppInfoTallerPublica> ConsultarInformacionTallerAsync(CancellationToken ct = default);

    Task<IReadOnlyList<WhatsAppFaqAutorizada>> BuscarFaqsAutorizadasAsync(
        string consulta,
        int limite = 3,
        CancellationToken ct = default);

    Task<WhatsAppCatalogoConsultaResponse> ConsultarCatalogoPublicoAsync(
        string? busqueda,
        bool incluirProductos,
        bool incluirServicios,
        int limite = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Intenta consultar datos personales de perfil del cliente usando estrictamente el contexto conversacional fail-closed.
    /// Mientras no exista un mecanismo real de verificación aprobado, siempre devuelve <see cref="ServiceResultStatus.Forbidden"/>.
    /// </summary>
    Task<ServiceResult<WhatsAppPerfilClienteResponse>> ConsultarPerfilPersonalAsync(
        Guid conversacionId,
        CancellationToken ct = default);

    Task<ServiceResult<List<WhatsAppVehiculoClienteResponse>>> ConsultarVehiculosPersonalesAsync(
        Guid conversacionId,
        CancellationToken ct = default);

    Task<ServiceResult<List<WhatsAppOrdenServicioResumenResponse>>> ConsultarOrdenesPersonalesAsync(
        Guid conversacionId,
        bool incluirFinalizadasYCanceladas = false,
        int limite = 5,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppOrdenServicioDetalleResponse>> ConsultarEstadoOrdenPersonalAsync(
        Guid conversacionId,
        string? numeroOrden = null,
        CancellationToken ct = default);

    Task<ServiceResult<List<WhatsAppCitaClienteResponse>>> ConsultarCitasPersonalesAsync(
        Guid conversacionId,
        bool soloActivas = true,
        int limite = 5,
        CancellationToken ct = default);
}

public sealed class WhatsAppAiCrmToolsFacade : IWhatsAppAiCrmToolsFacade
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "la", "el", "los", "las", "un", "una", "unos", "unas", "en", "para", "por",
        "con", "que", "al", "del", "como", "son", "hola", "buenos", "buenas", "dias",
        "tardes", "noches", "favor", "quiero", "necesito", "pueden", "tienen", "hacen",
        "saber", "sobre", "taller", "benavides", "team", "cual", "cuales", "donde"
    };

    private readonly ApplicationDbContext _context;
    private readonly IWhatsAppCrmConsultaService _crmConsulta;

    public WhatsAppAiCrmToolsFacade(
        ApplicationDbContext context,
        IWhatsAppCrmConsultaService crmConsulta)
    {
        _context = context;
        _crmConsulta = crmConsulta;
    }

    public async Task<WhatsAppInfoTallerPublica> ConsultarInformacionTallerAsync(CancellationToken ct = default)
    {
        var config = await _context.ConfiguracionesEmpresa
            .AsNoTracking()
            .Select(c => new
            {
                c.NombreEmpresa,
                c.Direccion,
                c.Telefono,
                c.Email
            })
            .FirstOrDefaultAsync(ct);

        var categorias = await _context.FaqItems
            .AsNoTracking()
            .Where(f => f.Activo)
            .Select(f => f.Categoria)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        return new WhatsAppInfoTallerPublica(
            NombreTaller: string.IsNullOrWhiteSpace(config?.NombreEmpresa) ? "Team Benavides" : config.NombreEmpresa.Trim(),
            Direccion: config?.Direccion?.Trim(),
            TelefonoContacto: config?.Telefono?.Trim(),
            EmailContacto: config?.Email?.Trim(),
            CategoriasFaqDisponibles: categorias);
    }

    public async Task<IReadOnlyList<WhatsAppFaqAutorizada>> BuscarFaqsAutorizadasAsync(
        string consulta,
        int limite = 3,
        CancellationToken ct = default)
    {
        var tope = Math.Clamp(limite, 1, 5);
        var tokens = ExtraerTokens(consulta);
        if (tokens.Count == 0)
        {
            return Array.Empty<WhatsAppFaqAutorizada>();
        }

        var normConsulta = NormalizarTexto(consulta);
        var faqs = await _context.FaqItems
            .AsNoTracking()
            .Where(f => f.Activo)
            .OrderBy(f => f.Orden)
            .ThenBy(f => f.Pregunta)
            .Select(f => new
            {
                f.Id,
                f.Categoria,
                f.Pregunta,
                f.Respuesta,
                f.PalabrasClave,
                f.Orden
            })
            .ToListAsync(ct);

        var puntuadas = new List<(WhatsAppFaqAutorizada Item, int Puntaje, int Orden)>();
        foreach (var faq in faqs)
        {
            var normPregunta = NormalizarTexto(faq.Pregunta);
            var normKeywords = NormalizarTexto(faq.PalabrasClave ?? string.Empty);
            var normCategoria = NormalizarTexto(faq.Categoria);
            var palabrasKeyword = normKeywords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var palabrasPregunta = normPregunta.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            int puntaje = 0;
            if (!string.IsNullOrWhiteSpace(normConsulta)
                && normConsulta.Length >= 6
                && (normPregunta.Contains(normConsulta, StringComparison.Ordinal)
                    || normConsulta.Contains(normPregunta, StringComparison.Ordinal)))
            {
                puntaje += 50;
            }

            foreach (var token in tokens)
            {
                if (palabrasKeyword.Any(k => k.Equals(token, StringComparison.Ordinal)))
                {
                    puntaje += 20;
                }
                else if (palabrasKeyword.Any(k => EsPrefijoSimilar(k, token)))
                {
                    puntaje += 12;
                }

                if (palabrasPregunta.Any(w => w.Equals(token, StringComparison.Ordinal)))
                {
                    puntaje += 15;
                }
                else if (palabrasPregunta.Any(w => EsPrefijoSimilar(w, token)))
                {
                    puntaje += 8;
                }

                if (normCategoria.Contains(token, StringComparison.Ordinal))
                {
                    puntaje += 10;
                }
            }

            if (puntaje >= 15)
            {
                puntuadas.Add((
                    new WhatsAppFaqAutorizada(faq.Id, faq.Categoria, faq.Pregunta, faq.Respuesta),
                    puntaje,
                    faq.Orden));
            }
        }

        return puntuadas
            .OrderByDescending(x => x.Puntaje)
            .ThenBy(x => x.Orden)
            .Take(tope)
            .Select(x => x.Item)
            .ToList();
    }

    public Task<WhatsAppCatalogoConsultaResponse> ConsultarCatalogoPublicoAsync(
        string? busqueda,
        bool incluirProductos,
        bool incluirServicios,
        int limite = 5,
        CancellationToken ct = default)
    {
        var tope = Math.Clamp(limite, 1, 10);
        // Fuerza SIEMPRE ConversacionalCliente: sin Costo, sin stock numérico exacto y con precios oficiales con IGV.
        return _crmConsulta.ConsultarProductosYServiciosAsync(
            busqueda: busqueda,
            incluirProductos: incluirProductos,
            incluirServicios: incluirServicios,
            limite: tope,
            modo: WhatsAppModoConsultaCrm.ConversacionalCliente,
            ct: ct);
    }

    // Todas las consultas personales fuerzan SIEMPRE el contexto ConversacionalSinVerificar() (fail-closed).
    // La fachada jamás permite construir ni inyectar AdministrativoInterno.

    public Task<ServiceResult<WhatsAppPerfilClienteResponse>> ConsultarPerfilPersonalAsync(
        Guid conversacionId,
        CancellationToken ct = default) =>
        _crmConsulta.ConsultarPerfilClienteAsync(
            conversacionId,
            WhatsAppContextoConsultaCrm.ConversacionalSinVerificar(),
            ct);

    public Task<ServiceResult<List<WhatsAppVehiculoClienteResponse>>> ConsultarVehiculosPersonalesAsync(
        Guid conversacionId,
        CancellationToken ct = default) =>
        _crmConsulta.ConsultarVehiculosClienteAsync(
            conversacionId,
            WhatsAppContextoConsultaCrm.ConversacionalSinVerificar(),
            ct);

    public Task<ServiceResult<List<WhatsAppOrdenServicioResumenResponse>>> ConsultarOrdenesPersonalesAsync(
        Guid conversacionId,
        bool incluirFinalizadasYCanceladas = false,
        int limite = 5,
        CancellationToken ct = default) =>
        _crmConsulta.ConsultarOrdenesServicioClienteAsync(
            conversacionId,
            incluirFinalizadasYCanceladas,
            Math.Clamp(limite, 1, 10),
            WhatsAppContextoConsultaCrm.ConversacionalSinVerificar(),
            ct);

    public Task<ServiceResult<WhatsAppOrdenServicioDetalleResponse>> ConsultarEstadoOrdenPersonalAsync(
        Guid conversacionId,
        string? numeroOrden = null,
        CancellationToken ct = default) =>
        _crmConsulta.ConsultarEstadoOrdenServicioAsync(
            conversacionId,
            ordenServicioId: null,
            numeroOrden: numeroOrden,
            contexto: WhatsAppContextoConsultaCrm.ConversacionalSinVerificar(),
            ct: ct);

    public Task<ServiceResult<List<WhatsAppCitaClienteResponse>>> ConsultarCitasPersonalesAsync(
        Guid conversacionId,
        bool soloActivas = true,
        int limite = 5,
        CancellationToken ct = default) =>
        _crmConsulta.ConsultarCitasClienteAsync(
            conversacionId,
            soloActivas,
            Math.Clamp(limite, 1, 10),
            WhatsAppContextoConsultaCrm.ConversacionalSinVerificar(),
            ct);

    private static List<string> ExtraerTokens(string? texto)
    {
        var normalizado = NormalizarTexto(texto);
        var palabras = normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var resultado = new List<string>();
        foreach (var p in palabras)
        {
            if (p.Length >= 3 && !StopWords.Contains(p) && !resultado.Contains(p, StringComparer.Ordinal))
            {
                resultado.Add(p);
            }
        }

        return resultado;
    }

    private static bool EsPrefijoSimilar(string a, string b)
    {
        if (a.Length < 4 || b.Length < 4) return false;
        return a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal);
    }

    private static string NormalizarTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Trim().ToLowerInvariant())
        {
            switch (c)
            {
                case 'á' or 'à' or 'ä': sb.Append('a'); break;
                case 'é' or 'è' or 'ë': sb.Append('e'); break;
                case 'í' or 'ì' or 'ï': sb.Append('i'); break;
                case 'ó' or 'ò' or 'ö': sb.Append('o'); break;
                case 'ú' or 'ù' or 'ü': sb.Append('u'); break;
                case 'ñ': sb.Append('n'); break;
                default:
                    sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
                    break;
            }
        }

        return sb.ToString();
    }
}
