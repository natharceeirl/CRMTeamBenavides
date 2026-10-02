using System.Text.Json;
using System.Text.Json.Serialization;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly ApplicationDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AuditoriaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task RegistrarEventoAsync(
        Guid? usuarioId,
        string accion,
        string entidad,
        string entidadId,
        object? detalle = null,
        CancellationToken ct = default)
    {
        string? detalleJson = null;
        if (detalle is not null)
        {
            if (detalle is string s)
            {
                detalleJson = s;
            }
            else
            {
                try
                {
                    detalleJson = JsonSerializer.Serialize(detalle, JsonOptions);
                }
                catch
                {
                    detalleJson = detalle.ToString();
                }
            }
        }

        var evento = new EventoAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            Detalle = detalleJson
        };

        _context.EventosAuditoria.Add(evento);
        await _context.SaveChangesAsync(ct);
    }
}
