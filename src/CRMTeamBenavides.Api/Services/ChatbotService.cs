using System.Text;
using CRMTeamBenavides.Api.Features.Chatbot;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class ChatbotService : IChatbotService
{
    private readonly ApplicationDbContext _context;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "la", "el", "los", "las", "un", "una", "unos", "unas", "en", "para", "por",
        "con", "que", "y", "o", "a", "mi", "tu", "su", "al", "del", "como", "es", "son",
        "taller", "benavides", "hola", "buenos", "dias", "tardes", "noches", "porfavor", "favor",
        "hacen", "tienen", "ofrecen", "quiero", "necesito", "buenas", "pueden", "servicio", "servicios"
    };

    public ChatbotService(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================================
    // ENDPOINTS PÚBLICOS
    // =========================================================================

    public async Task<List<FaqResponse>> GetFaqsPublicasAsync(string? categoria, string? busqueda)
    {
        var query = _context.FaqItems.Where(f => f.Activo);

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            var catLower = categoria.Trim().ToLower();
            query = query.Where(f => f.Categoria.ToLower() == catLower);
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim().ToLower();
            query = query.Where(f =>
                f.Pregunta.ToLower().Contains(term) ||
                (f.PalabrasClave != null && f.PalabrasClave.ToLower().Contains(term)));
        }

        return await query
            .OrderBy(f => f.Orden)
            .ThenBy(f => f.Pregunta)
            .Select(f => MapToFaqResponse(f))
            .ToListAsync();
    }

    public async Task<ServiceResult<ConsultaChatbotResponse>> ConsultarAsync(
        ConsultaChatbotRequest request,
        Guid? usuarioAutenticadoId)
    {
        if (string.IsNullOrWhiteSpace(request.Mensaje))
        {
            return ServiceResult<ConsultaChatbotResponse>.Invalid("El mensaje de consulta es obligatorio.");
        }

        var faqsActivas = await _context.FaqItems
            .Where(f => f.Activo)
            .ToListAsync();

        var tokens = ExtraerTokens(request.Mensaje);
        var normUserMsg = NormalizarTexto(request.Mensaje);

        FaqItem? bestMatch = null;
        int maxScore = 0;

        foreach (var faq in faqsActivas)
        {
            var normPregunta = NormalizarTexto(faq.Pregunta);
            var normKeywords = NormalizarTexto(faq.PalabrasClave ?? string.Empty);
            var keywords = normKeywords.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var preguntaWords = normPregunta.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            int score = 0;

            // Coincidencia de frase exacta o contenida
            if (!string.IsNullOrWhiteSpace(normUserMsg) &&
                (normPregunta.Contains(normUserMsg) || (normUserMsg.Length >= 8 && normPregunta.Length >= 8 && normUserMsg.Contains(normPregunta))))
            {
                score += 50;
            }

            // Coincidencia de palabras clave y términos individuales
            foreach (var token in tokens)
            {
                if (keywords.Any(k => k == token))
                {
                    score += 20;
                }
                else if (keywords.Any(k => (k.Length >= 5 && token.Length >= 5) && (k.StartsWith(token) || token.StartsWith(k))))
                {
                    score += 15;
                }

                if (preguntaWords.Any(w => w == token))
                {
                    score += 15;
                }
            }

            if (score > maxScore)
            {
                maxScore = score;
                bestMatch = faq;
            }
        }

        // Determinar si hay coincidencia suficiente (umbral = 20)
        bool resuelto = bestMatch != null && maxScore >= 20;

        Guid? clienteId = null;
        if (usuarioAutenticadoId.HasValue)
        {
            clienteId = await ObtenerClienteIdDeUsuarioAsync(usuarioAutenticadoId.Value);
        }

        var consulta = new ConsultaChatbot
        {
            Canal                 = string.IsNullOrWhiteSpace(request.Canal) ? "Web" : request.Canal.Trim(),
            ClienteId             = clienteId,
            NombreContacto        = request.NombreContacto?.Trim(),
            TelefonoContacto      = request.TelefonoContacto?.Trim(),
            MensajeConsulta       = request.Mensaje.Trim(),
            FaqItemId             = resuelto ? bestMatch!.Id : null,
            RequiereAtencionAgente = !resuelto,
            EstadoAtencion        = resuelto ? EstadoAtencionConsulta.Resuelto : EstadoAtencionConsulta.Pendiente,
            FechaDerivacion       = !resuelto ? DateTime.UtcNow : null,
            FechaResolucion       = resuelto ? DateTime.UtcNow : null,
            FechaCreacion         = DateTime.UtcNow,
            Activo                = true
        };

        if (resuelto)
        {
            bestMatch!.VecesConsultada++;
            bestMatch.FechaModificacion = DateTime.UtcNow;
        }

        _context.ConsultasChatbot.Add(consulta);
        await _context.SaveChangesAsync();

        if (resuelto)
        {
            return ServiceResult<ConsultaChatbotResponse>.Success(new ConsultaChatbotResponse(
                ResueltoPorFaq:   true,
                Faq:              MapToFaqResponse(bestMatch!),
                Sugerencias:      new List<FaqResponse>(),
                RequiereAgente:   false,
                ConsultaId:       consulta.Id,
                MensajeRespuesta: bestMatch!.Respuesta));
        }

        // Sugerir las FAQs más consultadas o de mayor prioridad
        var sugerencias = faqsActivas
            .OrderByDescending(f => f.VecesConsultada)
            .ThenBy(f => f.Orden)
            .Take(3)
            .Select(f => MapToFaqResponse(f))
            .ToList();

        return ServiceResult<ConsultaChatbotResponse>.Success(new ConsultaChatbotResponse(
            ResueltoPorFaq:   false,
            Faq:              null,
            Sugerencias:      sugerencias,
            RequiereAgente:   true,
            ConsultaId:       consulta.Id,
            MensajeRespuesta: "No encontré una respuesta específica para tu consulta. Puedes revisar nuestras preguntas frecuentes sugeridas o solicitar la atención de un asesor del taller."));
    }

    public async Task<ServiceResult<SolicitudAgenteResponse>> SolicitarAgenteAsync(
        SolicitarAgenteRequest request,
        Guid? usuarioAutenticadoId)
    {
        if (string.IsNullOrWhiteSpace(request.TelefonoContacto))
        {
            return ServiceResult<SolicitudAgenteResponse>.Invalid("El teléfono de contacto es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return ServiceResult<SolicitudAgenteResponse>.Invalid("El motivo o descripción de la consulta es obligatorio.");
        }

        Guid? clienteId = null;
        if (usuarioAutenticadoId.HasValue)
        {
            clienteId = await ObtenerClienteIdDeUsuarioAsync(usuarioAutenticadoId.Value);
        }

        ConsultaChatbot? consulta = null;

        if (request.ConsultaId.HasValue)
        {
            consulta = await _context.ConsultasChatbot
                .FirstOrDefaultAsync(c => c.Id == request.ConsultaId.Value && c.Activo);
        }

        if (consulta is not null)
        {
            // Actualizar consulta existente
            if (!string.IsNullOrWhiteSpace(request.NombreContacto))
            {
                consulta.NombreContacto = request.NombreContacto.Trim();
            }
            consulta.TelefonoContacto       = request.TelefonoContacto.Trim();
            consulta.RequiereAtencionAgente = true;
            consulta.EstadoAtencion         = EstadoAtencionConsulta.Pendiente;
            consulta.FechaDerivacion        = DateTime.UtcNow;
            consulta.FechaModificacion      = DateTime.UtcNow;

            if (clienteId.HasValue && !consulta.ClienteId.HasValue)
            {
                consulta.ClienteId = clienteId;
            }

            if (!string.IsNullOrWhiteSpace(request.Motivo) && !consulta.MensajeConsulta.Contains(request.Motivo))
            {
                consulta.MensajeConsulta = $"{consulta.MensajeConsulta} | Motivo: {request.Motivo.Trim()}";
            }
        }
        else
        {
            // Crear nueva solicitud de derivación
            consulta = new ConsultaChatbot
            {
                Canal                  = string.IsNullOrWhiteSpace(request.Canal) ? "Web" : request.Canal.Trim(),
                ClienteId              = clienteId,
                NombreContacto         = request.NombreContacto?.Trim(),
                TelefonoContacto       = request.TelefonoContacto.Trim(),
                MensajeConsulta        = request.Motivo.Trim(),
                RequiereAtencionAgente = true,
                EstadoAtencion         = EstadoAtencionConsulta.Pendiente,
                FechaDerivacion        = DateTime.UtcNow,
                FechaCreacion          = DateTime.UtcNow,
                Activo                 = true
            };
            _context.ConsultasChatbot.Add(consulta);
        }

        await _context.SaveChangesAsync();

        return ServiceResult<SolicitudAgenteResponse>.Success(new SolicitudAgenteResponse(
            ConsultaId: consulta.Id,
            Estado:     consulta.EstadoAtencion.ToString(),
            Mensaje:    "Tu solicitud ha sido registrada con éxito. Un asesor del taller se pondrá en contacto contigo a la brevedad."));
    }

    // =========================================================================
    // ENDPOINTS ADMINISTRATIVOS — FAQs
    // =========================================================================

    public async Task<List<FaqResponse>> GetFaqsAdminAsync(string? categoria, bool? soloActivos)
    {
        var query = _context.FaqItems.AsQueryable();

        if (soloActivos.HasValue && soloActivos.Value)
        {
            query = query.Where(f => f.Activo);
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            var catLower = categoria.Trim().ToLower();
            query = query.Where(f => f.Categoria.ToLower() == catLower);
        }

        return await query
            .OrderBy(f => f.Orden)
            .ThenBy(f => f.Pregunta)
            .Select(f => MapToFaqResponse(f))
            .ToListAsync();
    }

    public async Task<ServiceResult<FaqResponse>> CreateFaqAsync(CreateFaqRequest request, Guid? usuarioId)
    {
        if (string.IsNullOrWhiteSpace(request.Categoria))
        {
            return ServiceResult<FaqResponse>.Invalid("La categoría es obligatoria.");
        }
        if (string.IsNullOrWhiteSpace(request.Pregunta))
        {
            return ServiceResult<FaqResponse>.Invalid("La pregunta es obligatoria.");
        }
        if (string.IsNullOrWhiteSpace(request.Respuesta))
        {
            return ServiceResult<FaqResponse>.Invalid("La respuesta es obligatoria.");
        }

        var faq = new FaqItem
        {
            Categoria       = request.Categoria.Trim(),
            Pregunta        = request.Pregunta.Trim(),
            Respuesta       = request.Respuesta.Trim(),
            PalabrasClave   = request.PalabrasClave?.Trim(),
            Orden           = request.Orden,
            VecesConsultada = 0,
            CreadoPorId     = usuarioId,
            FechaCreacion   = DateTime.UtcNow,
            Activo          = true
        };

        _context.FaqItems.Add(faq);
        await _context.SaveChangesAsync();

        return ServiceResult<FaqResponse>.Success(MapToFaqResponse(faq));
    }

    public async Task<ServiceResult<FaqResponse>> UpdateFaqAsync(Guid id, UpdateFaqRequest request, Guid? usuarioId)
    {
        var faq = await _context.FaqItems.FirstOrDefaultAsync(f => f.Id == id);
        if (faq is null)
        {
            return ServiceResult<FaqResponse>.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Categoria))
        {
            return ServiceResult<FaqResponse>.Invalid("La categoría es obligatoria.");
        }
        if (string.IsNullOrWhiteSpace(request.Pregunta))
        {
            return ServiceResult<FaqResponse>.Invalid("La pregunta es obligatoria.");
        }
        if (string.IsNullOrWhiteSpace(request.Respuesta))
        {
            return ServiceResult<FaqResponse>.Invalid("La respuesta es obligatoria.");
        }

        faq.Categoria         = request.Categoria.Trim();
        faq.Pregunta          = request.Pregunta.Trim();
        faq.Respuesta         = request.Respuesta.Trim();
        faq.PalabrasClave     = request.PalabrasClave?.Trim();
        faq.Orden             = request.Orden;
        faq.Activo            = request.Activo;
        faq.ModificadoPorId   = usuarioId;
        faq.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<FaqResponse>.Success(MapToFaqResponse(faq));
    }

    public async Task<ServiceResult<bool>> DeleteFaqAsync(Guid id, Guid? usuarioId)
    {
        var faq = await _context.FaqItems.FirstOrDefaultAsync(f => f.Id == id && f.Activo);
        if (faq is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        faq.Activo            = false;
        faq.ModificadoPorId   = usuarioId;
        faq.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ServiceResult<bool>.Success(true);
    }

    // =========================================================================
    // ENDPOINTS ADMINISTRATIVOS — BANDEJA DE CONSULTAS
    // =========================================================================

    public async Task<List<ConsultaBandejaResponse>> GetConsultasAdminAsync(
        bool? requiereAtencion,
        EstadoAtencionConsulta? estado,
        string? canal,
        DateTime? fechaDesde,
        DateTime? fechaHasta)
    {
        var query = _context.ConsultasChatbot
            .Include(c => c.Cliente)
            .Include(c => c.FaqItem)
            .Include(c => c.AgenteAsignado)
            .Where(c => c.Activo);

        if (requiereAtencion.HasValue)
        {
            query = query.Where(c => c.RequiereAtencionAgente == requiereAtencion.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(c => c.EstadoAtencion == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(canal))
        {
            var canalLower = canal.Trim().ToLower();
            query = query.Where(c => c.Canal.ToLower() == canalLower);
        }

        if (fechaDesde.HasValue)
        {
            var desdeUtc = DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc);
            query = query.Where(c => c.FechaCreacion >= desdeUtc);
        }

        if (fechaHasta.HasValue)
        {
            var hastaExclusivo = DateTime.SpecifyKind(fechaHasta.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(c => c.FechaCreacion < hastaExclusivo);
        }

        return await query
            .OrderByDescending(c => c.FechaCreacion)
            .Select(c => new ConsultaBandejaResponse(
                c.Id,
                c.FechaCreacion,
                c.Canal,
                c.ClienteId,
                c.Cliente != null ? c.Cliente.NombreCompleto : null,
                c.NombreContacto,
                c.TelefonoContacto,
                c.MensajeConsulta,
                c.FaqItemId,
                c.FaqItem != null ? c.FaqItem.Pregunta : null,
                c.RequiereAtencionAgente,
                c.EstadoAtencion.ToString(),
                (int)c.EstadoAtencion,
                c.AgenteAsignadoId,
                c.AgenteAsignado != null ? c.AgenteAsignado.NombreCompleto : null,
                c.NotasAgente,
                c.FechaDerivacion,
                c.FechaResolucion))
            .ToListAsync();
    }

    public async Task<ServiceResult<ConsultaBandejaResponse>> AsignarAgenteAsync(
        Guid id,
        AsignarAgenteRequest request,
        Guid? usuarioId)
    {
        var consulta = await _context.ConsultasChatbot
            .Include(c => c.Cliente)
            .Include(c => c.FaqItem)
            .Include(c => c.AgenteAsignado)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo);

        if (consulta is null)
        {
            return ServiceResult<ConsultaBandejaResponse>.NotFound();
        }

        var agente = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Id == request.AgenteId && u.Activo);

        if (agente is null)
        {
            return ServiceResult<ConsultaBandejaResponse>.Invalid("El usuario agente especificado no existe o está inactivo.");
        }

        consulta.AgenteAsignadoId   = agente.Id;
        consulta.AgenteAsignado     = agente;
        consulta.EstadoAtencion     = EstadoAtencionConsulta.EnAtencion;
        consulta.ModificadoPorId    = usuarioId;
        consulta.FechaModificacion  = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<ConsultaBandejaResponse>.Success(MapToBandejaResponse(consulta));
    }

    public async Task<ServiceResult<ConsultaBandejaResponse>> ResolverConsultaAsync(
        Guid id,
        ResolverConsultaRequest request,
        Guid? usuarioId)
    {
        var consulta = await _context.ConsultasChatbot
            .Include(c => c.Cliente)
            .Include(c => c.FaqItem)
            .Include(c => c.AgenteAsignado)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo);

        if (consulta is null)
        {
            return ServiceResult<ConsultaBandejaResponse>.NotFound();
        }

        if (request.Estado != EstadoAtencionConsulta.Resuelto &&
            request.Estado != EstadoAtencionConsulta.Descartado)
        {
            return ServiceResult<ConsultaBandejaResponse>.Invalid("El estado de cierre debe ser Resuelto o Descartado.");
        }

        consulta.EstadoAtencion     = request.Estado;
        consulta.NotasAgente        = request.NotasAgente?.Trim();
        consulta.FechaResolucion    = DateTime.UtcNow;
        consulta.ModificadoPorId    = usuarioId;
        consulta.FechaModificacion  = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<ConsultaBandejaResponse>.Success(MapToBandejaResponse(consulta));
    }

    // =========================================================================
    // HELPER DE AUTORIZACIÓN POR ROL
    // =========================================================================

    public async Task<bool> TieneRolAsync(Guid usuarioId, params string[] rolesPermitidos)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo);
        if (usuario is null) return false;

        var rolesUsuario = await _context.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId && ur.Rol.Activo)
            .Select(ur => ur.Rol.Nombre)
            .ToListAsync();

        // El usuario de desarrollo o administrador siempre tiene acceso
        if (string.Equals(usuario.Email, "developer@crm.local", StringComparison.OrdinalIgnoreCase) ||
            rolesUsuario.Any(r => r.Equals("Administrador", StringComparison.OrdinalIgnoreCase) ||
                                  r.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Verificar roles permitidos
        foreach (var rolPermitido in rolesPermitidos)
        {
            if (rolesUsuario.Any(r => r.Equals(rolPermitido, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================================
    // MÉTODOS PRIVADOS AUXILIARES
    // =========================================================================

    private static FaqResponse MapToFaqResponse(FaqItem f) => new(
        f.Id,
        f.Categoria,
        f.Pregunta,
        f.Respuesta,
        f.PalabrasClave,
        f.Orden,
        f.VecesConsultada,
        f.Activo);

    private static ConsultaBandejaResponse MapToBandejaResponse(ConsultaChatbot c) => new(
        c.Id,
        c.FechaCreacion,
        c.Canal,
        c.ClienteId,
        c.Cliente != null ? c.Cliente.NombreCompleto : null,
        c.NombreContacto,
        c.TelefonoContacto,
        c.MensajeConsulta,
        c.FaqItemId,
        c.FaqItem != null ? c.FaqItem.Pregunta : null,
        c.RequiereAtencionAgente,
        c.EstadoAtencion.ToString(),
        (int)c.EstadoAtencion,
        c.AgenteAsignadoId,
        c.AgenteAsignado != null ? c.AgenteAsignado.NombreCompleto : null,
        c.NotasAgente,
        c.FechaDerivacion,
        c.FechaResolucion);

    private async Task<Guid?> ObtenerClienteIdDeUsuarioAsync(Guid usuarioId)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo);
        if (usuario is null || string.IsNullOrWhiteSpace(usuario.Email)) return null;

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Email == usuario.Email && c.Activo);

        return cliente?.Id;
    }

    private static List<string> ExtraerTokens(string texto)
    {
        var normalizado = NormalizarTexto(texto);
        var palabras = normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var tokens = new List<string>();
        foreach (var p in palabras)
        {
            if (p.Length >= 3 && !StopWords.Contains(p))
            {
                tokens.Add(p);
            }
        }
        return tokens;
    }

    private static string NormalizarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var sb = new StringBuilder();
        var lowered = texto.Trim().ToLowerInvariant();

        foreach (var c in lowered)
        {
            switch (c)
            {
                case 'á': case 'à': case 'ä': sb.Append('a'); break;
                case 'é': case 'è': case 'ë': sb.Append('e'); break;
                case 'í': case 'ì': case 'ï': sb.Append('i'); break;
                case 'ó': case 'ò': case 'ö': sb.Append('o'); break;
                case 'ú': case 'ù': case 'ü': sb.Append('u'); break;
                case 'ñ': sb.Append('n'); break;
                case '¿': case '?': case '¡': case '!': case ',': case '.': case ';':
                case ':': case '-': case '—': case '_': case '/': case '(': case ')':
                    sb.Append(' ');
                    break;
                default:
                    if (!char.IsPunctuation(c)) sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
