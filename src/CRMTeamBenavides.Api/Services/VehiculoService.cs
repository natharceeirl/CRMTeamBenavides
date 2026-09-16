using CRMTeamBenavides.Api.Features.Vehiculos;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class VehiculoService : IVehiculoService
{
    private readonly ApplicationDbContext _context;

    public VehiculoService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<VehiculoResponse>> GetAllAsync(Guid? clienteId)
    {
        var query = _context.Vehiculos
            .Include(v => v.Cliente)
            .Where(v => v.Activo);

        if (clienteId is not null)
        {
            query = query.Where(v => v.ClienteId == clienteId);
        }

        return await query
            .OrderBy(v => v.Placa)
            .Select(v => MapToResponse(v))
            .ToListAsync();
    }

    public async Task<ServiceResult<VehiculoResponse>> GetByIdAsync(Guid id)
    {
        var vehiculo = await _context.Vehiculos
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        return vehiculo is null
            ? ServiceResult<VehiculoResponse>.NotFound()
            : ServiceResult<VehiculoResponse>.Success(MapToResponse(vehiculo));
    }

    public async Task<ServiceResult<VehiculoResponse>> CreateAsync(CreateVehiculoRequest request)
    {
        var validationError = ValidateBasicFields(request.Placa, request.Marca, request.Modelo);
        if (validationError is not null)
        {
            return ServiceResult<VehiculoResponse>.Invalid(validationError);
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == request.ClienteId);

        if (cliente is null)
        {
            return ServiceResult<VehiculoResponse>.Invalid("El cliente indicado no existe.");
        }

        if (!cliente.Activo)
        {
            return ServiceResult<VehiculoResponse>.Invalid("No se puede asignar un vehículo a un cliente inactivo.");
        }

        var placaDuplicada = await _context.Vehiculos
            .AnyAsync(v => v.Placa == request.Placa && v.Activo);

        if (placaDuplicada)
        {
            return ServiceResult<VehiculoResponse>.Invalid("Ya existe un vehículo activo con esa placa.");
        }

        var vehiculo = new Vehiculo
        {
            ClienteId = request.ClienteId,
            Placa = request.Placa.Trim(),
            Marca = request.Marca.Trim(),
            Modelo = request.Modelo.Trim(),
            Anio = request.Anio,
            Kilometraje = request.Kilometraje,
            Color = request.Color,
            Observaciones = request.Observaciones
        };

        _context.Vehiculos.Add(vehiculo);
        await _context.SaveChangesAsync();

        vehiculo.Cliente = cliente;
        return ServiceResult<VehiculoResponse>.Success(MapToResponse(vehiculo));
    }

    public async Task<ServiceResult<VehiculoResponse>> UpdateAsync(Guid id, UpdateVehiculoRequest request)
    {
        var vehiculo = await _context.Vehiculos
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        if (vehiculo is null)
        {
            return ServiceResult<VehiculoResponse>.NotFound();
        }

        var validationError = ValidateBasicFields(request.Placa, request.Marca, request.Modelo);
        if (validationError is not null)
        {
            return ServiceResult<VehiculoResponse>.Invalid(validationError);
        }

        var cliente = vehiculo.ClienteId == request.ClienteId
            ? vehiculo.Cliente
            : await _context.Clientes.FirstOrDefaultAsync(c => c.Id == request.ClienteId);

        if (cliente is null)
        {
            return ServiceResult<VehiculoResponse>.Invalid("El cliente indicado no existe.");
        }

        if (!cliente.Activo)
        {
            return ServiceResult<VehiculoResponse>.Invalid("No se puede asignar un vehículo a un cliente inactivo.");
        }

        var placaDuplicada = await _context.Vehiculos
            .AnyAsync(v => v.Placa == request.Placa && v.Activo && v.Id != id);

        if (placaDuplicada)
        {
            return ServiceResult<VehiculoResponse>.Invalid("Ya existe otro vehículo activo con esa placa.");
        }

        vehiculo.ClienteId = request.ClienteId;
        vehiculo.Cliente = cliente;
        vehiculo.Placa = request.Placa.Trim();
        vehiculo.Marca = request.Marca.Trim();
        vehiculo.Modelo = request.Modelo.Trim();
        vehiculo.Anio = request.Anio;
        vehiculo.Kilometraje = request.Kilometraje;
        vehiculo.Color = request.Color;
        vehiculo.Observaciones = request.Observaciones;
        vehiculo.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<VehiculoResponse>.Success(MapToResponse(vehiculo));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var vehiculo = await _context.Vehiculos
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        if (vehiculo is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        vehiculo.Activo = false;
        vehiculo.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private static string? ValidateBasicFields(string placa, string marca, string modelo)
    {
        if (string.IsNullOrWhiteSpace(placa)) return "Placa es obligatoria.";
        if (string.IsNullOrWhiteSpace(marca)) return "Marca es obligatoria.";
        if (string.IsNullOrWhiteSpace(modelo)) return "Modelo es obligatorio.";
        return null;
    }

    private static VehiculoResponse MapToResponse(Vehiculo vehiculo) => new(
        vehiculo.Id,
        vehiculo.ClienteId,
        vehiculo.Cliente.NombreCompleto,
        vehiculo.Placa,
        vehiculo.Marca,
        vehiculo.Modelo,
        vehiculo.Anio,
        vehiculo.Kilometraje,
        vehiculo.Color,
        vehiculo.Observaciones,
        vehiculo.Activo);
}
