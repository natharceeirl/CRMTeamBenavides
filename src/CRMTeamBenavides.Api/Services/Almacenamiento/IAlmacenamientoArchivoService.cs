namespace CRMTeamBenavides.Api.Services.Almacenamiento;

public interface IAlmacenamientoArchivoService
{
    Task<(string rutaRelativa, string nombreAlmacenado)> GuardarFotoAsync(
        Stream stream,
        string nombreOriginal,
        string contentType,
        string subcarpeta,
        CancellationToken ct = default);

    Task<(Stream stream, string contentType)?> ObtenerArchivoAsync(
        string rutaRelativa,
        CancellationToken ct = default);

    Task<bool> EliminarArchivoAsync(
        string rutaRelativa,
        CancellationToken ct = default);
}
