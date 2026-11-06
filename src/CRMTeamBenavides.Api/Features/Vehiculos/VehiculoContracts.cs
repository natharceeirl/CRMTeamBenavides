using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Vehiculos;

public record VehiculoResponse(
    Guid Id,
    Guid ClienteId,
    string ClienteNombre,
    string? Placa,
    string Marca,
    string Modelo,
    int? Anio,
    int? Kilometraje,
    string? Color,
    string? Observaciones,
    bool Activo,
    string TipoUnidad = "Motocicleta",
    int TipoUnidadId = 0,
    string? NumeroSerieVIN = null,
    string? NumeroMotor = null,
    string TipoMedidor = "Kilometraje",
    int TipoMedidorId = 0,
    decimal? HorasUso = null,
    decimal? ValorEstimado = null,
    decimal? LecturaMedidorActual = null,
    string? Dua = null,
    string? ItemDua = null,
    string? NumeroTarjetaPropiedad = null,
    DateOnly? FechaInicioGarantia = null,
    // Vendida por el taller y todavía sin tarjeta de propiedad.
    bool TarjetaPendiente = false);

public record CreateVehiculoRequest(
    Guid ClienteId,
    string? Placa,
    string Marca,
    string Modelo,
    int? Anio,
    int? Kilometraje,
    string? Color,
    string? Observaciones,
    TipoUnidad TipoUnidad = TipoUnidad.Motocicleta,
    string? NumeroSerieVIN = null,
    string? NumeroMotor = null,
    TipoMedidor TipoMedidor = TipoMedidor.Kilometraje,
    decimal? HorasUso = null,
    decimal? ValorEstimado = null,
    decimal? LecturaMedidorActual = null,
    // De la moto vendida por el taller: la tarjeta y la placa llegan después de la venta.
    string? Dua = null,
    string? ItemDua = null,
    string? NumeroTarjetaPropiedad = null,
    DateOnly? FechaInicioGarantia = null);

public record UpdateVehiculoRequest(
    Guid ClienteId,
    string? Placa,
    string Marca,
    string Modelo,
    int? Anio,
    int? Kilometraje,
    string? Color,
    string? Observaciones,
    TipoUnidad TipoUnidad = TipoUnidad.Motocicleta,
    string? NumeroSerieVIN = null,
    string? NumeroMotor = null,
    TipoMedidor TipoMedidor = TipoMedidor.Kilometraje,
    decimal? HorasUso = null,
    decimal? ValorEstimado = null,
    decimal? LecturaMedidorActual = null,
    // De la moto vendida por el taller: la tarjeta y la placa llegan después de la venta.
    string? Dua = null,
    string? ItemDua = null,
    string? NumeroTarjetaPropiedad = null,
    DateOnly? FechaInicioGarantia = null);

public record AltaRapidaVehiculoRequest(
    Guid ClienteId,
    string? Placa,
    string Marca,
    string Modelo,
    int? Kilometraje = null,
    string? Color = null);
