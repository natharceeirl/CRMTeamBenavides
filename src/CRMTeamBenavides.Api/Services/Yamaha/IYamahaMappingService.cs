using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Contexto inmutable de configuración del punto de venta para la integración con Yamaha v3.
/// Desacopla al mapeador de cualquier inyección de bases de datos o servicios I/O.
/// </summary>
public sealed record YamahaPuntoVentaContexto(
    string RazonSocial,
    string Ruc,
    string CodigoPuntoVenta,
    string? NombreComercialPuntoVenta = null,
    string TipoPuntoVenta = YamahaV3Catalogos.TipoPuntoVentaFijo,
    string CiudadSede = "Arequipa",
    string DepartamentoSede = "Arequipa",
    string DistritoSede = "Arequipa")
{
    public static YamahaPuntoVentaContexto Crear(
        ConfiguracionEmpresa? configuracionEmpresa,
        YamahaSettings? settings)
    {
        var razonSocial = configuracionEmpresa?.RazonSocial?.Trim();
        if (string.IsNullOrWhiteSpace(razonSocial))
        {
            razonSocial = configuracionEmpresa?.NombreEmpresa?.Trim() ?? "Team Benavides S.R.L.";
        }

        var ruc = configuracionEmpresa?.Ruc?.Trim() ?? string.Empty;
        var codigoPuntoVenta = settings?.CodigoPuntoVenta?.Trim() ?? string.Empty;
        var nombreComercial = settings?.NombreComercialPuntoVenta?.Trim();
        var tipoPuntoVenta = !string.IsNullOrWhiteSpace(settings?.TipoPuntoVenta)
            ? settings.TipoPuntoVenta.Trim()
            : YamahaV3Catalogos.TipoPuntoVentaFijo;

        return new YamahaPuntoVentaContexto(
            RazonSocial: razonSocial,
            Ruc: ruc,
            CodigoPuntoVenta: codigoPuntoVenta,
            NombreComercialPuntoVenta: nombreComercial,
            TipoPuntoVenta: tipoPuntoVenta);
    }
}

/// <summary>
/// Resultado del mapeo de un ítem individual hacia un DTO de Yamaha v3.
/// </summary>
public sealed record YamahaMapeoRegistroResultado<TDto>(
    bool EsValido,
    TDto? Dto,
    string IdentificadorOrigen,
    IReadOnlyList<string> Observaciones,
    IReadOnlyList<string> ErroresBloqueantes)
{
    public static YamahaMapeoRegistroResultado<TDto> Exitoso(
        TDto dto,
        string identificadorOrigen,
        IReadOnlyList<string>? observaciones = null) =>
        new(true, dto, identificadorOrigen, observaciones ?? Array.Empty<string>(), Array.Empty<string>());

    public static YamahaMapeoRegistroResultado<TDto> NoProyectable(
        string identificadorOrigen,
        IReadOnlyList<string> erroresBloqueantes,
        IReadOnlyList<string>? observaciones = null) =>
        new(false, default, identificadorOrigen, observaciones ?? Array.Empty<string>(), erroresBloqueantes);

    public static YamahaMapeoRegistroResultado<TDto> NoProyectable(
        string identificadorOrigen,
        string errorBloqueante,
        IReadOnlyList<string>? observaciones = null) =>
        new(false, default, identificadorOrigen, observaciones ?? Array.Empty<string>(), new[] { errorBloqueante });
}

/// <summary>
/// Resultado agrupado de la proyección de un lote hacia DTOs de Yamaha v3.
/// </summary>
public sealed record YamahaMapeoLoteResultado<TDto>(
    IReadOnlyList<TDto> DtosProyectados,
    IReadOnlyList<YamahaMapeoRegistroResultado<TDto>> Registros,
    int TotalProcesados,
    int TotalValidos,
    int TotalNoProyectables)
{
    public bool EsLoteCompletamenteValido => TotalNoProyectables == 0 && TotalValidos > 0;
}

/// <summary>
/// Servicio de proyección pura y en memoria desde las entidades del CRM Team Benavides
/// hacia los contratos oficiales de Yamaha Motor del Perú v3.
/// </summary>
public interface IYamahaMappingService
{
    /// <summary>
    /// Proyecta una línea de venta (<see cref="DetalleVenta"/>) hacia el DTO de Ventas de Yamaha v3.
    /// </summary>
    YamahaMapeoRegistroResultado<YamahaVentaV3Dto> MapearDetalleVenta(
        Venta venta,
        DetalleVenta detalle,
        YamahaPuntoVentaContexto contexto,
        Comprobante? comprobante = null,
        Cliente? cliente = null,
        Vehiculo? vehiculo = null);

    /// <summary>
    /// Proyecta todas las líneas activas de una venta hacia el contrato de Ventas de Yamaha v3.
    /// </summary>
    YamahaMapeoLoteResultado<YamahaVentaV3Dto> MapearVenta(
        Venta venta,
        YamahaPuntoVentaContexto contexto,
        Comprobante? comprobante = null,
        Cliente? cliente = null,
        Vehiculo? vehiculo = null);

    /// <summary>
    /// Proyecta un lote de ventas hacia el contrato de Ventas de Yamaha v3.
    /// </summary>
    YamahaMapeoLoteResultado<YamahaVentaV3Dto> MapearLoteVentas(
        IEnumerable<Venta> ventas,
        YamahaPuntoVentaContexto contexto);

    /// <summary>
    /// Proyecta un producto del catálogo hacia el DTO de Stock de Yamaha v3.
    /// </summary>
    YamahaMapeoRegistroResultado<YamahaStockV3Dto> MapearProductoStock(
        Producto producto,
        YamahaPuntoVentaContexto contexto);

    /// <summary>
    /// Proyecta un lote de productos hacia el contrato de Stock de Yamaha v3.
    /// </summary>
    YamahaMapeoLoteResultado<YamahaStockV3Dto> MapearLoteStock(
        IEnumerable<Producto> productos,
        YamahaPuntoVentaContexto contexto);

    /// <summary>
    /// Proyecta una línea de servicio (<see cref="DetalleServicio"/>) hacia el DTO de Servicio Técnico de Yamaha v3.
    /// Exige que la orden cuente con comprobante de facturación emitido (Sección 6.2).
    /// </summary>
    YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto> MapearDetalleServicio(
        OrdenServicio orden,
        DetalleServicio detalle,
        YamahaPuntoVentaContexto contexto,
        Comprobante comprobante,
        Cliente? cliente = null,
        Vehiculo? vehiculo = null);

    /// <summary>
    /// Proyecta una orden de servicio completa hacia el contrato de Servicio Técnico de Yamaha v3.
    /// Exige comprobante emitido (Sección 6.2).
    /// </summary>
    YamahaMapeoLoteResultado<YamahaServicioTecnicoV3Dto> MapearOrdenServicio(
        OrdenServicio orden,
        YamahaPuntoVentaContexto contexto,
        Comprobante? comprobante = null,
        Cliente? cliente = null,
        Vehiculo? vehiculo = null);

    /// <summary>
    /// Proyecta un lote de órdenes de servicio hacia el contrato de Servicio Técnico de Yamaha v3.
    /// </summary>
    YamahaMapeoLoteResultado<YamahaServicioTecnicoV3Dto> MapearLoteOrdenesServicio(
        IEnumerable<OrdenServicio> ordenes,
        YamahaPuntoVentaContexto contexto);
}
