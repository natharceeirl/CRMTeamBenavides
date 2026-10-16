using CRMTeamBenavides.Api.Features.Importaciones;

namespace CRMTeamBenavides.Api.Services.Importacion;

public interface IImportadorExcelService
{
    byte[] GenerarPlantillaExcel();

    Task<ResultadoImportacionExcelDto> ValidarArchivoAsync(
        Stream archivoStream,
        string nombreArchivo,
        Guid? categoriaId,
        Guid? usuarioId,
        string modoDuplicados = "ErrorSiExiste",
        CancellationToken ct = default);

    Task<ResultadoImportacionExcelDto> EjecutarImportacionAsync(
        Stream archivoStream,
        string nombreArchivo,
        Guid? categoriaId,
        Guid? usuarioId,
        string modoDuplicados = "ErrorSiExiste",
        CancellationToken ct = default);

    Task<List<HistorialImportacionItemDto>> ObtenerHistorialAsync(
        int limite = 50,
        CancellationToken ct = default);
}
