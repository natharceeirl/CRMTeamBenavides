using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Fotos;

public record FotoOrdenServicioResponse(
    Guid Id,
    Guid OrdenServicioId,
    string NombreArchivoOriginal,
    string UrlRelativa,
    string ContentType,
    long TamanioBytes,
    EtapaFotoOrdenServicio Etapa,
    string EtapaNombre,
    Guid UsuarioId,
    string? UsuarioNombre,
    string? Observacion,
    DateTime FechaCreacion);

public record SubirFotoMetadataRequest(
    EtapaFotoOrdenServicio Etapa = EtapaFotoOrdenServicio.Ingreso,
    string? Observacion = null);
