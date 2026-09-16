namespace CRMTeamBenavides.Api.Features.Vehiculos;

public record VehiculoResponse(
    Guid Id,
    Guid ClienteId,
    string ClienteNombre,
    string Placa,
    string Marca,
    string Modelo,
    int? Anio,
    int? Kilometraje,
    string? Color,
    string? Observaciones,
    bool Activo);

public record CreateVehiculoRequest(
    Guid ClienteId,
    string Placa,
    string Marca,
    string Modelo,
    int? Anio,
    int? Kilometraje,
    string? Color,
    string? Observaciones);

public record UpdateVehiculoRequest(
    Guid ClienteId,
    string Placa,
    string Marca,
    string Modelo,
    int? Anio,
    int? Kilometraje,
    string? Color,
    string? Observaciones);
