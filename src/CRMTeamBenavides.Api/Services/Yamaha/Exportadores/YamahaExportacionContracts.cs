namespace CRMTeamBenavides.Api.Services.Yamaha.Exportadores;

/// <summary>
/// Resultado tipado de una extracción y proyección desde PostgreSQL hacia contratos de Yamaha v3.
/// </summary>
/// <typeparam name="TDto">Tipo de DTO oficial de Yamaha v3 proyectado.</typeparam>
public sealed record YamahaExportacionResultado<TDto>(
    IReadOnlyList<TDto> RegistrosValidos,
    int TotalEntidadesLeidas,
    int TotalRegistrosValidos,
    int TotalRegistrosOmitidos,
    IReadOnlyList<string> ObservacionesYAdvertencias,
    DateTime? RangoDesdeUtc,
    DateTime? RangoHastaUtc
)
{
    public bool TieneRegistros => TotalRegistrosValidos > 0;
}
