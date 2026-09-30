using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Fotos;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface IFotoOrdenServicioService
{
    Task<ServiceResult<FotoOrdenServicioResponse>> SubirFotoAsync(
        Guid ordenServicioId,
        Stream stream,
        string nombreOriginal,
        string contentType,
        long tamanioBytes,
        EtapaFotoOrdenServicio etapa,
        string? observacion,
        Guid usuarioId,
        UserIsolationContext isolation,
        CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<FotoOrdenServicioResponse>>> GetFotosByOrdenIdAsync(
        Guid ordenServicioId,
        UserIsolationContext isolation,
        CancellationToken ct = default);

    Task<ServiceResult<FotoOrdenServicioResponse>> GetFotoByIdAsync(
        Guid ordenServicioId,
        Guid fotoId,
        UserIsolationContext isolation,
        CancellationToken ct = default);

    Task<ServiceResult<(Stream Stream, string ContentType, string NombreOriginal)>> GetArchivoFotoAsync(
        Guid ordenServicioId,
        Guid fotoId,
        UserIsolationContext isolation,
        CancellationToken ct = default);

    Task<ServiceResult<bool>> EliminarFotoAsync(
        Guid ordenServicioId,
        Guid fotoId,
        Guid usuarioId,
        UserIsolationContext isolation,
        CancellationToken ct = default);
}
