using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class OrdenServicioService : IOrdenServicioService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<Usuario> _userManager;

    public OrdenServicioService(ApplicationDbContext context, UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<OrdenServicioResponse>> GetAllAsync(Guid? vehiculoId, EstadoOrdenServicio? estado)
    {
        var query = _context.OrdenesServicio
            .Where(o => o.Activo);

        if (vehiculoId.HasValue)
        {
            query = query.Where(o => o.VehiculoId == vehiculoId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(o => o.Estado == estado.Value);
        }

        // Proyección a tipo anónimo: EF Core genera un único JOIN en SQL y trae solo las columnas
        // necesarias. No se usan Include() porque la proyección los reemplaza.
        // El enum .ToString() y el constructor del record se aplican en memoria, tras la consulta SQL.
        var filas = await query
            .OrderByDescending(o => o.FechaApertura)
            .Select(o => new
            {
                o.Id,
                o.VehiculoId,
                VehiculoPlaca       = o.Vehiculo.Placa,
                VehiculoMarca       = o.Vehiculo.Marca,
                VehiculoModelo      = o.Vehiculo.Modelo,
                ClienteId           = o.Vehiculo.ClienteId,
                ClienteNombre       = o.Vehiculo.Cliente.NombreCompleto,
                o.TecnicoAsignadoId,
                TecnicoNombre       = o.TecnicoAsignado != null ? o.TecnicoAsignado.NombreCompleto : null,
                o.Estado,
                o.FechaApertura,
                o.FechaCierre,
                o.Diagnostico,
                o.Observaciones,
                o.Activo
            })
            .ToListAsync();

        return filas.Select(o => new OrdenServicioResponse(
            o.Id,
            o.VehiculoId,
            o.VehiculoPlaca,
            o.VehiculoMarca,
            o.VehiculoModelo,
            o.ClienteId,
            o.ClienteNombre,
            o.TecnicoAsignadoId,
            o.TecnicoNombre,
            o.Estado.ToString(),
            (int)o.Estado,
            o.FechaApertura,
            o.FechaCierre,
            o.Diagnostico,
            o.Observaciones,
            o.Activo)).ToList();
    }

    public async Task<ServiceResult<OrdenServicioDetalleResponse>> GetByIdAsync(Guid id)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.TecnicoAsignado)
            .FirstOrDefaultAsync(o => o.Id == id && o.Activo);

        return orden is null
            ? ServiceResult<OrdenServicioDetalleResponse>.NotFound()
            : ServiceResult<OrdenServicioDetalleResponse>.Success(MapToDetalleResponse(orden));
    }

    public async Task<ServiceResult<OrdenServicioResponse>> CreateAperturaAsync(AperturaOrdenServicioRequest request)
    {
        var vehiculo = await _context.Vehiculos
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == request.VehiculoId && v.Activo);

        if (vehiculo is null)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid("El vehículo indicado no existe o está inactivo.");
        }

        Usuario? tecnico = null;
        if (request.TecnicoAsignadoId.HasValue)
        {
            // Patrón del proyecto: acceso a usuarios siempre a través de _userManager.Users.
            tecnico = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == request.TecnicoAsignadoId.Value && u.Activo);

            if (tecnico is null)
            {
                return ServiceResult<OrdenServicioResponse>.Invalid("El técnico asignado no existe o está inactivo.");
            }
        }

        var orden = new OrdenServicio
        {
            VehiculoId       = request.VehiculoId,
            TecnicoAsignadoId = request.TecnicoAsignadoId,
            Observaciones    = request.Observaciones,
            Estado           = EstadoOrdenServicio.Abierta,
            FechaApertura    = DateTime.UtcNow,
            FechaCreacion    = DateTime.UtcNow,
            Activo           = true
        };

        _context.OrdenesServicio.Add(orden);
        await _context.SaveChangesAsync();

        // Se construye la respuesta directamente desde las entidades cargadas localmente (vehiculo con
        // Cliente, y tecnico), en lugar de pasar por orden.Vehiculo.Cliente. Esto evita que un futuro
        // cambio en la carga del vehículo cause un NullReferenceException aquí.
        return ServiceResult<OrdenServicioResponse>.Success(new OrdenServicioResponse(
            orden.Id,
            vehiculo.Id,
            vehiculo.Placa,
            vehiculo.Marca,
            vehiculo.Modelo,
            vehiculo.ClienteId,
            vehiculo.Cliente.NombreCompleto,
            tecnico?.Id,
            tecnico?.NombreCompleto,
            orden.Estado.ToString(),
            (int)orden.Estado,
            orden.FechaApertura,
            orden.FechaCierre,
            orden.Diagnostico,
            orden.Observaciones,
            orden.Activo));
    }

    public async Task<ServiceResult<OrdenServicioResponse>> RegistrarDiagnosticoAsync(Guid id, RegistrarDiagnosticoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Diagnostico))
        {
            return ServiceResult<OrdenServicioResponse>.Invalid("El texto del diagnóstico es obligatorio.");
        }

        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.TecnicoAsignado)
            .FirstOrDefaultAsync(o => o.Id == id && o.Activo);

        if (orden is null)
        {
            return ServiceResult<OrdenServicioResponse>.NotFound();
        }

        if (orden.Estado == EstadoOrdenServicio.Cancelada || orden.Estado == EstadoOrdenServicio.Entregada)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(
                $"No se puede registrar diagnóstico en una orden que se encuentra en estado '{orden.Estado}'.");
        }

        if (request.TecnicoAsignadoId.HasValue)
        {
            // Patrón del proyecto: acceso a usuarios siempre a través de _userManager.Users.
            var tecnico = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == request.TecnicoAsignadoId.Value && u.Activo);

            if (tecnico is null)
            {
                return ServiceResult<OrdenServicioResponse>.Invalid("El técnico asignado no existe o está inactivo.");
            }

            orden.TecnicoAsignadoId = request.TecnicoAsignadoId.Value;
            orden.TecnicoAsignado   = tecnico;
        }

        if (orden.Estado == EstadoOrdenServicio.Abierta)
        {
            orden.Estado = EstadoOrdenServicio.Diagnostico;
        }

        orden.Diagnostico = request.Diagnostico.Trim();

        // Semántica de Observaciones:
        //   null  → conservar el valor existente sin modificarlo.
        //   ""    → limpiar (dejar en cadena vacía).
        //   texto → reemplazar con el nuevo valor.
        if (request.Observaciones is not null)
        {
            orden.Observaciones = request.Observaciones;
        }

        orden.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
    }

    private static OrdenServicioResponse MapToResponse(OrdenServicio orden) => new(
        orden.Id,
        orden.VehiculoId,
        orden.Vehiculo.Placa,
        orden.Vehiculo.Marca,
        orden.Vehiculo.Modelo,
        orden.Vehiculo.ClienteId,
        orden.Vehiculo.Cliente.NombreCompleto,
        orden.TecnicoAsignadoId,
        orden.TecnicoAsignado?.NombreCompleto,
        orden.Estado.ToString(),
        (int)orden.Estado,
        orden.FechaApertura,
        orden.FechaCierre,
        orden.Diagnostico,
        orden.Observaciones,
        orden.Activo);

    private static OrdenServicioDetalleResponse MapToDetalleResponse(OrdenServicio orden) => new(
        orden.Id,
        orden.VehiculoId,
        orden.Vehiculo.Placa,
        orden.Vehiculo.Marca,
        orden.Vehiculo.Modelo,
        orden.Vehiculo.Anio,
        orden.Vehiculo.Kilometraje,
        orden.Vehiculo.Color,
        orden.Vehiculo.ClienteId,
        orden.Vehiculo.Cliente.NombreCompleto,
        orden.Vehiculo.Cliente.Telefono,
        orden.Vehiculo.Cliente.DocumentoIdentidad,
        orden.TecnicoAsignadoId,
        orden.TecnicoAsignado?.NombreCompleto,
        orden.Estado.ToString(),
        (int)orden.Estado,
        orden.FechaApertura,
        orden.FechaCierre,
        orden.Diagnostico,
        orden.Observaciones,
        orden.Activo);
}
