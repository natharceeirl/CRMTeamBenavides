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

    public async Task<List<VehiculoResponse>> GetAllAsync(Guid? clienteId, Guid? soloClienteId = null)
    {
        var query = _context.Vehiculos
            .AsNoTracking()
            .Include(v => v.Cliente)
            .Where(v => v.Activo);

        if (soloClienteId.HasValue)
        {
            query = query.Where(v => v.ClienteId == soloClienteId.Value);
        }
        else if (clienteId is not null)
        {
            query = query.Where(v => v.ClienteId == clienteId);
        }

        return await query
            .OrderBy(v => v.Placa)
            .Select(v => MapToResponse(v))
            .ToListAsync();
    }

    public async Task<ServiceResult<VehiculoResponse>> GetByIdAsync(Guid id, Guid? soloClienteId = null)
    {
        var vehiculo = await _context.Vehiculos
            .AsNoTracking()
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        if (vehiculo is null)
        {
            return ServiceResult<VehiculoResponse>.NotFound();
        }

        if (soloClienteId.HasValue && vehiculo.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<VehiculoResponse>.NotFound();
        }

        return ServiceResult<VehiculoResponse>.Success(MapToResponse(vehiculo));
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

        string? placaNormalizada = string.IsNullOrWhiteSpace(request.Placa) ? null : request.Placa.Trim().ToUpperInvariant();

        if (placaNormalizada is not null)
        {
            var placaDuplicada = await _context.Vehiculos
                .AnyAsync(v => v.Placa == placaNormalizada && v.Activo);

            if (placaDuplicada)
            {
                return ServiceResult<VehiculoResponse>.Invalid("Ya existe una unidad activa con esa placa.");
            }
        }

        decimal? lecturaActual = request.LecturaMedidorActual;
        int? kilometraje = request.Kilometraje;
        decimal? horasUso = request.HorasUso;

        if (lecturaActual.HasValue)
        {
            if (request.TipoMedidor == TipoMedidor.Kilometraje)
                kilometraje = (int)Math.Round(lecturaActual.Value);
            else
                horasUso = lecturaActual.Value;
        }
        else
        {
            lecturaActual = request.TipoMedidor == TipoMedidor.Kilometraje
                ? (kilometraje.HasValue ? (decimal?)kilometraje.Value : null)
                : horasUso;
        }

        var vehiculo = new Vehiculo
        {
            ClienteId            = request.ClienteId,
            Placa                = placaNormalizada,
            TipoUnidad           = request.TipoUnidad,
            Marca                = request.Marca.Trim(),
            Modelo               = request.Modelo.Trim(),
            Anio                 = request.Anio,
            NumeroSerieVIN       = string.IsNullOrWhiteSpace(request.NumeroSerieVIN) ? null : request.NumeroSerieVIN.Trim().ToUpperInvariant(),
            NumeroMotor          = string.IsNullOrWhiteSpace(request.NumeroMotor) ? null : request.NumeroMotor.Trim().ToUpperInvariant(),
            TipoMedidor          = request.TipoMedidor,
            Kilometraje          = kilometraje,
            HorasUso             = horasUso,
            LecturaMedidorActual = lecturaActual,
            ValorEstimado        = request.ValorEstimado,
            Color                = request.Color?.Trim(),
            Observaciones        = request.Observaciones?.Trim(),
            FechaCreacion        = DateTime.UtcNow,
            Activo               = true
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

        string? placaNormalizada = string.IsNullOrWhiteSpace(request.Placa) ? null : request.Placa.Trim().ToUpperInvariant();

        if (placaNormalizada is not null)
        {
            var placaDuplicada = await _context.Vehiculos
                .AnyAsync(v => v.Placa == placaNormalizada && v.Activo && v.Id != id);

            if (placaDuplicada)
            {
                return ServiceResult<VehiculoResponse>.Invalid("Ya existe otra unidad activa con esa placa.");
            }
        }

        decimal? lecturaActual = request.LecturaMedidorActual;
        int? kilometraje = request.Kilometraje;
        decimal? horasUso = request.HorasUso;

        if (lecturaActual.HasValue)
        {
            if (request.TipoMedidor == TipoMedidor.Kilometraje)
                kilometraje = (int)Math.Round(lecturaActual.Value);
            else
                horasUso = lecturaActual.Value;
        }
        else
        {
            lecturaActual = request.TipoMedidor == TipoMedidor.Kilometraje
                ? (kilometraje.HasValue ? (decimal?)kilometraje.Value : null)
                : horasUso;
        }

        vehiculo.ClienteId            = request.ClienteId;
        vehiculo.Cliente              = cliente;
        vehiculo.Placa                = placaNormalizada;
        vehiculo.TipoUnidad           = request.TipoUnidad;
        vehiculo.Marca                = request.Marca.Trim();
        vehiculo.Modelo               = request.Modelo.Trim();
        vehiculo.Anio                 = request.Anio;
        vehiculo.NumeroSerieVIN       = string.IsNullOrWhiteSpace(request.NumeroSerieVIN) ? null : request.NumeroSerieVIN.Trim().ToUpperInvariant();
        vehiculo.NumeroMotor          = string.IsNullOrWhiteSpace(request.NumeroMotor) ? null : request.NumeroMotor.Trim().ToUpperInvariant();
        vehiculo.TipoMedidor          = request.TipoMedidor;
        vehiculo.Kilometraje          = kilometraje;
        vehiculo.HorasUso             = horasUso;
        vehiculo.LecturaMedidorActual = lecturaActual;
        vehiculo.ValorEstimado        = request.ValorEstimado;
        vehiculo.Color                = request.Color?.Trim();
        vehiculo.Observaciones        = request.Observaciones?.Trim();
        vehiculo.FechaModificacion    = DateTime.UtcNow;

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

    private static string? ValidateBasicFields(string? placa, string marca, string modelo)
    {
        if (string.IsNullOrWhiteSpace(marca)) return "Marca es obligatoria.";
        if (string.IsNullOrWhiteSpace(modelo)) return "Modelo es obligatorio.";
        return null;
    }

    private static VehiculoResponse MapToResponse(Vehiculo vehiculo) => new(
        vehiculo.Id,
        vehiculo.ClienteId,
        vehiculo.Cliente?.NombreCompleto ?? string.Empty,
        vehiculo.Placa,
        vehiculo.Marca,
        vehiculo.Modelo,
        vehiculo.Anio,
        vehiculo.Kilometraje,
        vehiculo.Color,
        vehiculo.Observaciones,
        vehiculo.Activo,
        vehiculo.TipoUnidad.ToString(),
        (int)vehiculo.TipoUnidad,
        vehiculo.NumeroSerieVIN,
        vehiculo.NumeroMotor,
        vehiculo.TipoMedidor.ToString(),
        (int)vehiculo.TipoMedidor,
        vehiculo.HorasUso,
        vehiculo.ValorEstimado,
        vehiculo.LecturaMedidorActual ?? (vehiculo.TipoMedidor == TipoMedidor.Kilometraje ? (vehiculo.Kilometraje.HasValue ? (decimal?)vehiculo.Kilometraje.Value : null) : vehiculo.HorasUso));
}
