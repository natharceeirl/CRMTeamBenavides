using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Clientes;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class ClienteService : IClienteService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<Usuario> _userManager;

    public ClienteService(ApplicationDbContext context, UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<ClienteResponse>> GetAllAsync(Guid? soloClienteId = null)
    {
        var query = _context.Clientes
            .AsNoTracking()
            .Where(c => c.Activo);

        if (soloClienteId.HasValue)
        {
            query = query.Where(c => c.Id == soloClienteId.Value);
        }

        return await query
            .OrderBy(c => c.NombreCompleto)
            .Select(c => MapToResponse(c))
            .ToListAsync();
    }

    public async Task<ServiceResult<ClienteResponse>> GetByIdAsync(Guid id, Guid? soloClienteId = null)
    {
        if (soloClienteId.HasValue && id != soloClienteId.Value)
        {
            return ServiceResult<ClienteResponse>.NotFound();
        }

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

        var (tipoDoc, numDoc, errorDoc) = ValidarYResolverDocumento(request.TipoDocumento, request.NumeroDocumento, request.DocumentoIdentidad);
        if (errorDoc is not null)
        {
            return ServiceResult<ClienteResponse>.Invalid(errorDoc);
        }

        if (!string.IsNullOrWhiteSpace(numDoc))
        {
            var existeDoc = await _context.Clientes
                .AnyAsync(c => c.Activo && (c.NumeroDocumento == numDoc || c.DocumentoIdentidad == numDoc));
            if (existeDoc)
            {
                return ServiceResult<ClienteResponse>.Invalid($"Ya existe un cliente activo registrado con el número de documento '{numDoc}'.");
            }
        }

        var cliente = new Cliente
        {
            NombreCompleto     = request.NombreCompleto.Trim(),
            RazonSocial        = request.RazonSocial?.Trim(),
            TipoDocumento      = tipoDoc,
            NumeroDocumento    = numDoc,
            DocumentoIdentidad = numDoc,
            Telefono           = request.Telefono?.Trim(),
            Email              = request.Email?.Trim(),
            Direccion          = request.Direccion?.Trim(),
            Observaciones      = request.Observaciones?.Trim(),
            FechaCreacion      = DateTime.UtcNow,
            Activo             = true
        };

        _context.Clientes.Add(cliente);
        if (!string.IsNullOrWhiteSpace(numDoc))
        {
            await AsegurarUsuarioClienteAsync(cliente, numDoc);
        }
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

        var (tipoDoc, numDoc, errorDoc) = ValidarYResolverDocumento(request.TipoDocumento, request.NumeroDocumento, request.DocumentoIdentidad);
        if (errorDoc is not null)
        {
            return ServiceResult<ClienteResponse>.Invalid(errorDoc);
        }

        if (!string.IsNullOrWhiteSpace(numDoc))
        {
            var existeDoc = await _context.Clientes
                .AnyAsync(c => c.Activo && c.Id != id && (c.NumeroDocumento == numDoc || c.DocumentoIdentidad == numDoc));
            if (existeDoc)
            {
                return ServiceResult<ClienteResponse>.Invalid($"Ya existe un cliente activo registrado con el número de documento '{numDoc}'.");
            }
        }

        cliente.NombreCompleto     = request.NombreCompleto.Trim();
        cliente.RazonSocial        = request.RazonSocial?.Trim();
        cliente.TipoDocumento      = tipoDoc;
        cliente.NumeroDocumento    = numDoc;
        cliente.DocumentoIdentidad = numDoc;
        cliente.Telefono           = request.Telefono?.Trim();
        cliente.Email              = request.Email?.Trim();
        cliente.Direccion          = request.Direccion?.Trim();
        cliente.Observaciones      = request.Observaciones?.Trim();
        cliente.FechaModificacion  = DateTime.UtcNow;

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

    public async Task<ServiceResult<ClienteResponse>> GetByDocumentoAsync(string documento, Guid? soloClienteId = null)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return ServiceResult<ClienteResponse>.NotFound();
        }

        var doc = documento.Trim();
        var query = _context.Clientes
            .AsNoTracking()
            .Where(c => c.Activo && (c.NumeroDocumento == doc || c.DocumentoIdentidad == doc));

        if (soloClienteId.HasValue)
        {
            query = query.Where(c => c.Id == soloClienteId.Value);
        }

        var cliente = await query.FirstOrDefaultAsync();

        return cliente is null
            ? ServiceResult<ClienteResponse>.NotFound()
            : ServiceResult<ClienteResponse>.Success(MapToResponse(cliente));
    }

    public async Task<ServiceResult<ClienteResponse>> AltaRapidaAsync(AltaRapidaClienteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreCompleto))
        {
            return ServiceResult<ClienteResponse>.Invalid("NombreCompleto es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.NumeroDocumento))
        {
            return ServiceResult<ClienteResponse>.Invalid("NumeroDocumento es obligatorio.");
        }

        var numDoc = request.NumeroDocumento.Trim();
        var tipoDoc = request.TipoDocumento ?? TipoDocumentoCliente.DNI;

        var clienteExistente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Activo && (c.NumeroDocumento == numDoc || c.DocumentoIdentidad == numDoc));

        if (clienteExistente != null)
        {
            return ServiceResult<ClienteResponse>.Success(MapToResponse(clienteExistente));
        }

        var cliente = new Cliente
        {
            NombreCompleto     = request.NombreCompleto.Trim(),
            TipoDocumento      = tipoDoc,
            NumeroDocumento    = numDoc,
            DocumentoIdentidad = numDoc,
            Telefono           = request.Telefono?.Trim(),
            Email              = request.Email?.Trim(),
            Direccion          = request.Direccion?.Trim(),
            FechaCreacion      = DateTime.UtcNow,
            Activo             = true
        };

        _context.Clientes.Add(cliente);
        await AsegurarUsuarioClienteAsync(cliente, numDoc);
        await _context.SaveChangesAsync();

        return ServiceResult<ClienteResponse>.Success(MapToResponse(cliente));
    }

    private async Task AsegurarUsuarioClienteAsync(Cliente cliente, string? passwordDni)
    {
        if (string.IsNullOrWhiteSpace(passwordDni) || passwordDni.Trim().Length < 6) return;

        var dni = passwordDni.Trim();
        var existingUser = await _userManager.FindByNameAsync(dni);
        if (existingUser == null)
        {
            var email = !string.IsNullOrWhiteSpace(cliente.Email) ? cliente.Email.Trim() : $"{dni}@client.teambenavides.pe";
            existingUser = await _userManager.FindByEmailAsync(email);
        }

        if (existingUser == null)
        {
            var email = !string.IsNullOrWhiteSpace(cliente.Email) ? cliente.Email.Trim() : $"{dni}@client.teambenavides.pe";
            var user = new Usuario
            {
                UserName = dni,
                Email = email,
                NombreCompleto = cliente.NombreCompleto,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            var createRes = await _userManager.CreateAsync(user, dni);
            if (createRes.Succeeded)
            {
                cliente.UsuarioId = user.Id;
                cliente.Usuario = user;

                var rolCliente = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == RolesDefinidos.Cliente && r.Activo);
                if (rolCliente != null)
                {
                    _context.UsuarioRoles.Add(new UsuarioRol
                    {
                        UsuarioId = user.Id,
                        RolId = rolCliente.Id
                    });

                }
            }
        }
        else
        {
            cliente.UsuarioId = existingUser.Id;
            cliente.Usuario = existingUser;
        }
    }

    private static (TipoDocumentoCliente? tipo, string? numero, string? error) ValidarYResolverDocumento(
        TipoDocumentoCliente? tipoRequest,
        string? numeroDocRequest,
        string? docIdentidadRequest)
    {
        var numero = (numeroDocRequest ?? docIdentidadRequest)?.Trim();
        if (string.IsNullOrWhiteSpace(numero))
        {
            return (tipoRequest, null, null);
        }

        var tipo = tipoRequest;
        if (!tipo.HasValue)
        {
            if (numero.Length == 8 && System.Text.RegularExpressions.Regex.IsMatch(numero, @"^\d{8}$"))
            {
                tipo = TipoDocumentoCliente.DNI;
            }
            else if (numero.Length == 11 && System.Text.RegularExpressions.Regex.IsMatch(numero, @"^\d{11}$"))
            {
                tipo = TipoDocumentoCliente.RUC;
            }
            else
            {
                tipo = TipoDocumentoCliente.Otro;
            }
        }

        if (tipo == TipoDocumentoCliente.DNI && !System.Text.RegularExpressions.Regex.IsMatch(numero, @"^\d{8}$"))
        {
            return (tipo, numero, "El DNI debe contener exactamente 8 dígitos numéricos.");
        }

        if (tipo == TipoDocumentoCliente.RUC && !System.Text.RegularExpressions.Regex.IsMatch(numero, @"^\d{11}$"))
        {
            return (tipo, numero, "El RUC debe contener exactamente 11 dígitos numéricos.");
        }

        return (tipo, numero, null);
    }

    private static ClienteResponse MapToResponse(Cliente cliente) => new(
        cliente.Id,
        cliente.NombreCompleto,
        cliente.RazonSocial,
        cliente.NumeroDocumento ?? cliente.DocumentoIdentidad,
        cliente.Telefono,
        cliente.Email,
        cliente.Direccion,
        cliente.Observaciones,
        cliente.Activo,
        cliente.TipoDocumento?.ToString(),
        cliente.TipoDocumento.HasValue ? (int)cliente.TipoDocumento.Value : null,
        cliente.NumeroDocumento ?? cliente.DocumentoIdentidad);
}
