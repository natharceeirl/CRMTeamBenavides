namespace CRMTeamBenavides.Api.Features.Importaciones;

public record ErrorFilaImportacionDto(
    string Hoja,
    int Fila,
    string Columna,
    string? ValorLeido,
    string Mensaje
);

public record ResumenHojaImportacionDto(
    int TotalFilasLeidas,
    int FilasValidas,
    int FilasConError,
    int RegistrosCreados
);

public record ResultadoImportacionExcelDto(
    bool Exitoso,
    bool SoloValidacion,
    string NombreArchivo,
    DateTime FechaProceso,
    Guid? CategoriaId,
    ResumenHojaImportacionDto Clientes,
    ResumenHojaImportacionDto Catalogo,
    IReadOnlyList<ErrorFilaImportacionDto> Errores
);

public record HistorialImportacionItemDto(
    Guid Id,
    DateTime Fecha,
    Guid? UsuarioId,
    string? UsuarioNombre,
    string Accion,
    string NombreArchivo,
    bool Exitoso,
    bool SoloValidacion,
    Guid? CategoriaId,
    int ClientesProcesados,
    int CatalogoProcesados,
    int TotalErrores
);
