using CRMTeamBenavides.Api.Features.Clientes;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class ClienteService : IClienteService
{
    private readonly ApplicationDbContext _context;

    public ClienteService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClienteResponse>> GetAllAsync()
    {
        return await _context.Clientes
            .AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.NombreCompleto)
            .Select(c => MapToResponse(c))
            .ToListAsync();
    }

    public async Task<ServiceResult<ClienteResponse>> GetByIdAsync(Guid id)
    {
        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo);

        return cliente is null
            ? ServiceResult<ClienteResponse>.NotFound()
            : ServiceResult<ClienteResponse>.Success(MapToResponse(cliente));
    }

    public async Task<ServiceResult<ClienteResponse>> CreateAsync(CreateClienteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreCompleto))
        {
            return ServiceResult<ClienteResponse>.Invalid("NombreCompleto es obligatorio.");
        }

        var cliente = new Cliente
        {
            NombreCompleto = request.NombreCompleto.Trim(),
            RazonSocial = request.RazonSocial,
            DocumentoIdentidad = request.DocumentoIdentidad,
            Telefono = request.Telefono,
            Email = request.Email,
            Direccion = request.Direccion,
            Observaciones = request.Observaciones
        };

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        return ServiceResult<ClienteResponse>.Success(MapToResponse(cliente));
    }

    public async Task<ServiceResult<ClienteResponse>> UpdateAsync(Guid id, UpdateClienteRequest request)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo);

        if (cliente is null)
        {
            return ServiceResult<ClienteResponse>.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.NombreCompleto))
        {
            return ServiceResult<ClienteResponse>.Invalid("NombreCompleto es obligatorio.");
        }

        cliente.NombreCompleto = request.NombreCompleto.Trim();
        cliente.RazonSocial = request.RazonSocial;
        cliente.DocumentoIdentidad = request.DocumentoIdentidad;
        cliente.Telefono = request.Telefono;
        cliente.Email = request.Email;
        cliente.Direccion = request.Direccion;
        cliente.Observaciones = request.Observaciones;
        cliente.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<ClienteResponse>.Success(MapToResponse(cliente));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo);

        if (cliente is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        cliente.Activo = false;
        cliente.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private static ClienteResponse MapToResponse(Cliente cliente) => new(
        cliente.Id,
        cliente.NombreCompleto,
        cliente.RazonSocial,
        cliente.DocumentoIdentidad,
        cliente.Telefono,
        cliente.Email,
        cliente.Direccion,
        cliente.Observaciones,
        cliente.Activo);
}
