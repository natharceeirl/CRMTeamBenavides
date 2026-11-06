namespace CRMTeamBenavides.Api.Features.Motos;

/// <summary>Los datos de una moto al ingresar por una compra o por el inventario inicial.</summary>
public record UnidadMotoRequest(
    string NumeroSerieVIN,
    string? NumeroMotor = null,
    int? Anio = null,
    string? Color = null,
    string? Dua = null,
    string? ItemDua = null);

/// <summary>Ingreso de una moto sin compra: el inventario que ya estaba en la tienda. Costo en soles, sin IGV.</summary>
public record IngresarMotoRequest(
    UnidadMotoRequest Moto,
    decimal Costo);

public record MotoResponse(
    Guid Id,
    Guid ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    string NumeroSerieVIN,
    string? NumeroMotor,
    int? Anio,
    string? Color,
    string? Dua,
    string? ItemDua,
    decimal CostoUnitario,
    string Estado,
    int EstadoId,
    DateTime FechaIngreso,
    Guid? CompraId = null,
    string? NumeroCompra = null,
    Guid? VentaId = null,
    DateTime? FechaVenta = null,
    Guid? VehiculoId = null,
    string? ClienteNombre = null);
