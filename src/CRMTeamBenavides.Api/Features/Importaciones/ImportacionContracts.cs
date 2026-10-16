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
    int RegistrosCreados,
    int RegistrosOmitidos = 0
);

public record ResultadoImportacionExcelDto(
    bool Exitoso,
    bool SoloValidacion,
    string NombreArchivo,
    DateTime FechaProceso,
    Guid? CategoriaId,
    ResumenHojaImportacionDto Clientes,
    ResumenHojaImportacionDto Catalogo,
    IReadOnlyList<ErrorFilaImportacionDto> Errores,
    ResumenHojaImportacionDto? Unidades = null,
    ResumenHojaImportacionDto? Repuestos = null,
    string ModoDuplicados = "ErrorSiExiste"
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
    int TotalErrores,
    int UnidadesProcesadas = 0,
    int RepuestosProcesados = 0,
    string? ModoDuplicados = null
);
