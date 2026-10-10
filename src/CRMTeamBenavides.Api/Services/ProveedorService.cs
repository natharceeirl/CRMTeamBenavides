using System.Text.Json;
using System.Text.RegularExpressions;
using CRMTeamBenavides.Api.Features.Compras;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRMTeamBenavides.Api.Services;

public partial class ProveedorService : IProveedorService
{
    private readonly ApplicationDbContext _context;

    public ProveedorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProveedorResponse>> ListarAsync(string? busqueda, CancellationToken ct = default)
    {
        var query = _context.Proveedores.AsNoTracking().Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var patron = $"%{busqueda.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.RazonSocial, patron) ||
                EF.Functions.ILike(p.NumeroDocumento, patron));
        }

        var proveedores = await query.OrderBy(p => p.RazonSocial).ToListAsync(ct);
        return proveedores.Select(Mapear).ToList();
    }

    public async Task<ServiceResult<ProveedorResponse>> CrearAsync(
        GuardarProveedorRequest request, Guid? usuarioId, CancellationToken ct = default)
    {
        var (datos, error) = Validar(request);
        if (error is not null)
        {
            return ServiceResult<ProveedorResponse>.Invalid(error);
        }

        var duplicado = await BuscarPorDocumentoAsync(datos!.NumeroDocumento, null, ct);
        if (duplicado is not null)
        {
            return ServiceResult<ProveedorResponse>.Conflict(
                $"Ya está registrado el proveedor «{duplicado.RazonSocial}» con el documento {duplicado.NumeroDocumento}.");
        }

        var proveedor = new Proveedor
        {
            TipoDocumento = datos.TipoDocumento,
            NumeroDocumento = datos.NumeroDocumento,
            RazonSocial = datos.RazonSocial,
            Telefono = datos.Telefono,
            Email = datos.Email,
            Direccion = datos.Direccion,
            Contacto = datos.Contacto,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        _context.Proveedores.Add(proveedor);
        Auditar(usuarioId, "Crear", proveedor.Id, new { proveedor.NumeroDocumento, proveedor.RazonSocial });

        if (!await GuardarSinDuplicarAsync(ct))
        {
            return ServiceResult<ProveedorResponse>.Conflict($"Ya está registrado un proveedor con el documento {proveedor.NumeroDocumento}.");
        }

        return ServiceResult<ProveedorResponse>.Success(Mapear(proveedor));
    }

    public async Task<ServiceResult<ProveedorResponse>> ActualizarAsync(
        Guid id, GuardarProveedorRequest request, Guid? usuarioId, CancellationToken ct = default)
    {
        var proveedor = await _context.Proveedores.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (proveedor is null)
        {
            return ServiceResult<ProveedorResponse>.NotFound("Proveedor no encontrado.");
        }

        var (datos, error) = Validar(request);
        if (error is not null)
        {
            return ServiceResult<ProveedorResponse>.Invalid(error);
        }

        var duplicado = await BuscarPorDocumentoAsync(datos!.NumeroDocumento, id, ct);
        if (duplicado is not null)
        {
            return ServiceResult<ProveedorResponse>.Conflict(
                $"Ya está registrado el proveedor «{duplicado.RazonSocial}» con el documento {duplicado.NumeroDocumento}.");
        }

        var anterior = new { proveedor.NumeroDocumento, proveedor.RazonSocial };

        proveedor.TipoDocumento = datos.TipoDocumento;
        proveedor.NumeroDocumento = datos.NumeroDocumento;
        proveedor.RazonSocial = datos.RazonSocial;
        proveedor.Telefono = datos.Telefono;
        proveedor.Email = datos.Email;
        proveedor.Direccion = datos.Direccion;
        proveedor.Contacto = datos.Contacto;
        proveedor.ModificadoPorId = usuarioId;
        proveedor.FechaModificacion = DateTime.UtcNow;

        Auditar(usuarioId, "Editar", proveedor.Id, new
        {
            Anterior = anterior,
            Nuevo = new { proveedor.NumeroDocumento, proveedor.RazonSocial }
        });

        if (!await GuardarSinDuplicarAsync(ct))
        {
            return ServiceResult<ProveedorResponse>.Conflict($"Ya está registrado un proveedor con el documento {proveedor.NumeroDocumento}.");
        }

        return ServiceResult<ProveedorResponse>.Success(Mapear(proveedor));
    }

    /// <summary>Baja lógica: sus compras lo siguen mostrando y el documento queda libre para registrarlo de nuevo.</summary>
    public async Task<ServiceResult<bool>> EliminarAsync(Guid id, Guid? usuarioId, CancellationToken ct = default)
    {
        var proveedor = await _context.Proveedores.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (proveedor is null)
        {
            return ServiceResult<bool>.NotFound("Proveedor no encontrado.");
        }

        proveedor.Activo = false;
        proveedor.ModificadoPorId = usuarioId;
        proveedor.FechaModificacion = DateTime.UtcNow;
        Auditar(usuarioId, "Eliminar", proveedor.Id, new { proveedor.NumeroDocumento, proveedor.RazonSocial });

        await _context.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private sealed record DatosProveedor(
        TipoDocumentoCliente TipoDocumento,
        string NumeroDocumento,
        string RazonSocial,
        string? Telefono,
        string? Email,
        string? Direccion,
        string? Contacto);

    private static (DatosProveedor? Datos, string? Error) Validar(GuardarProveedorRequest request)
    {
        if (!Enum.IsDefined(request.TipoDocumento))
        {
            return (null, "El tipo de documento no es válido.");
        }

        var numero = request.NumeroDocumento?.Trim().ToUpperInvariant() ?? string.Empty;
        if (numero.Length == 0)
        {
            return (null, "El número de documento es obligatorio.");
        }

        if (request.TipoDocumento == TipoDocumentoCliente.RUC && !Ruc().IsMatch(numero))
        {
            return (null, "El RUC debe tener 11 dígitos.");
        }

        if (request.TipoDocumento == TipoDocumentoCliente.DNI && !Dni().IsMatch(numero))
        {
            return (null, "El DNI debe tener 8 dígitos.");
        }

        if (numero.Length > 20)
        {
            return (null, "El número de documento no puede pasar de 20 caracteres.");
        }

        var razonSocial = request.RazonSocial?.Trim() ?? string.Empty;
        if (razonSocial.Length == 0)
        {
            return (null, "La razón social o el nombre del proveedor es obligatorio.");
        }

        if (razonSocial.Length > 200)
        {
            return (null, "La razón social no puede pasar de 200 caracteres.");
        }

        var email = Opcional(request.Email);
        if (email is not null && (email.Length > 150 || !System.Net.Mail.MailAddress.TryCreate(email, out _)))
        {
            return (null, "El correo del proveedor no es válido.");
        }

        var telefono = Opcional(request.Telefono);
        var direccion = Opcional(request.Direccion);
        var contacto = Opcional(request.Contacto);
        if (telefono?.Length > 30 || direccion?.Length > 300 || contacto?.Length > 150)
        {
            return (null, "Teléfono, dirección o contacto son demasiado largos.");
        }

        return (new DatosProveedor(request.TipoDocumento, numero, razonSocial, telefono, email, direccion, contacto), null);
    }

    private static string? Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private Task<Proveedor?> BuscarPorDocumentoAsync(string numeroDocumento, Guid? excluirId, CancellationToken ct) =>
        _context.Proveedores.AsNoTracking().FirstOrDefaultAsync(p =>
            p.Activo && p.NumeroDocumento == numeroDocumento && (excluirId == null || p.Id != excluirId), ct);

    /// <summary>El índice único atrapa a dos usuarios que registran el mismo documento a la vez.</summary>
    private async Task<bool> GuardarSinDuplicarAsync(CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    private void Auditar(Guid? usuarioId, string accion, Guid proveedorId, object detalle) =>
        _context.EventosAuditoria.Add(new EventoAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Accion = accion,
            Entidad = "Proveedor",
            EntidadId = proveedorId.ToString(),
            Detalle = JsonSerializer.Serialize(detalle)
        });

    private static ProveedorResponse Mapear(Proveedor p) => new(
        p.Id,
        p.TipoDocumento,
        p.NumeroDocumento,
        p.RazonSocial,
        p.Telefono,
        p.Email,
        p.Direccion,
        p.Contacto,
        p.FechaCreacion);

    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex Ruc();

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex Dni();
}
