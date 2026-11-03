using System.Security.Cryptography;
using System.Text;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Clientes;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

/// <summary>
/// El cliente activa su cuenta del portal, en la web o en la app, con su DNI y un código
/// que le da el personal en el taller. La cuenta queda ligada a su ficha y entra con su
/// DNI y su contraseña.
/// </summary>
public class ActivacionCuentaService
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromHours(48);
    public const int IntentosMaximos = 5;

    // El mismo mensaje si el DNI no existe, el código está mal o ya venció: no se puede
    // averiguar qué DNI tiene un código pendiente.
    public const string MensajeCodigoInvalido =
        "El DNI o el código no son válidos, o el código ya venció. Pide uno nuevo en el taller.";

    private readonly UserManager<Usuario> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoria;

    public ActivacionCuentaService(UserManager<Usuario> userManager, ApplicationDbContext context, IAuditoriaService auditoria)
    {
        _userManager = userManager;
        _context = context;
        _auditoria = auditoria;
    }

    public async Task<ServiceResult<CuentaAppClienteResponse>> ObtenerEstadoAsync(Guid clienteId)
    {
        var cliente = await _context.Clientes.AsNoTracking()
            .Include(c => c.Usuario)
            .FirstOrDefaultAsync(c => c.Id == clienteId && c.Activo);
        if (cliente is null)
        {
            return ServiceResult<CuentaAppClienteResponse>.NotFound();
        }

        var tieneCuenta = cliente.Usuario is { Activo: true };
        var codigoVigente = !tieneCuenta && cliente.CodigoActivacionHash != null && cliente.CodigoActivacionVence > DateTime.UtcNow
            ? cliente.CodigoActivacionVence
            : null;
        return ServiceResult<CuentaAppClienteResponse>.Success(
            new CuentaAppClienteResponse(tieneCuenta, tieneCuenta ? cliente.Usuario!.Email : null, codigoVigente));
    }

    /// <summary>Un código nuevo reemplaza al anterior, que deja de servir.</summary>
    public async Task<ServiceResult<CodigoActivacionResponse>> GenerarCodigoAsync(Guid clienteId, Guid? usuarioId)
    {
        var cliente = await _context.Clientes
            .Include(c => c.Usuario)
            .FirstOrDefaultAsync(c => c.Id == clienteId && c.Activo);
        if (cliente is null)
        {
            return ServiceResult<CodigoActivacionResponse>.NotFound();
        }

        if (string.IsNullOrWhiteSpace(Documento(cliente)))
        {
            return ServiceResult<CodigoActivacionResponse>.Invalid(
                "Registra el DNI del cliente: lo escribe junto con el código para activar su cuenta.");
        }

        if (cliente.Usuario is { Activo: true })
        {
            return ServiceResult<CodigoActivacionResponse>.Conflict(
                "El cliente ya tiene cuenta en el portal: entra con su DNI y su contraseña.");
        }

        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var vence = DateTime.UtcNow.Add(Vigencia);
        cliente.CodigoActivacionHash = Hash(cliente.Id, codigo);
        cliente.CodigoActivacionVence = vence;
        cliente.CodigoActivacionIntentos = 0;
        cliente.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // El código no va a la auditoría: con él cualquiera activaría la cuenta.
        await _auditoria.RegistrarEventoAsync(usuarioId, "GenerarCodigoActivacion", "Cliente", cliente.Id.ToString(),
            new { Vence = vence });

        return ServiceResult<CodigoActivacionResponse>.Success(new CodigoActivacionResponse(codigo, vence));
    }

    public async Task<ServiceResult<Usuario>> ActivarAsync(ActivarCuentaRequest request)
    {
        var documento = request.NumeroDocumento?.Trim() ?? string.Empty;
        var codigo = request.Codigo?.Trim() ?? string.Empty;
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        if (documento.Length == 0 || codigo.Length == 0)
        {
            return ServiceResult<Usuario>.Invalid(MensajeCodigoInvalido);
        }

        if (email != null && await _userManager.FindByEmailAsync(email) is not null)
        {
            return ServiceResult<Usuario>.Invalid("Ese correo ya lo usa otra cuenta. Escribe otro o déjalo vacío.");
        }

        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == RolesDefinidos.Cliente && r.Activo);
        if (rol is null)
        {
            return ServiceResult<Usuario>.Invalid($"No existe el rol {RolesDefinidos.Cliente}.");
        }

        // La ficha se bloquea: dos intentos a la vez con el mismo código no crean dos cuentas.
        await using var transaccion = await _context.Database.BeginTransactionAsync();
        if (_context.Database.IsRelational())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT 1 FROM "Clientes"
                WHERE "Activo" AND "CodigoActivacionHash" IS NOT NULL
                  AND ("NumeroDocumento" = {documento} OR "DocumentoIdentidad" = {documento})
                FOR UPDATE
                """);
        }

        var candidatos = await _context.Clientes
            .Include(c => c.Usuario)
            .Where(c => c.Activo && c.CodigoActivacionHash != null
                && (c.NumeroDocumento == documento || c.DocumentoIdentidad == documento))
            .ToListAsync();

        var cliente = candidatos.FirstOrDefault(c => CodigoCorrecto(c, codigo));
        if (cliente is null)
        {
            foreach (var otro in candidatos)
            {
                // Cinco fallos invalidan el código: hay que pedir otro en el taller.
                otro.CodigoActivacionIntentos++;
                if (otro.CodigoActivacionIntentos >= IntentosMaximos)
                {
                    LimpiarCodigo(otro);
                }
            }
            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();
            return ServiceResult<Usuario>.Invalid(MensajeCodigoInvalido);
        }

        if (cliente.CodigoActivacionVence is not { } vence || vence <= DateTime.UtcNow)
        {
            LimpiarCodigo(cliente);
            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();
            return ServiceResult<Usuario>.Invalid(MensajeCodigoInvalido);
        }

        if (cliente.Usuario is { Activo: true })
        {
            LimpiarCodigo(cliente);
            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();
            return ServiceResult<Usuario>.Invalid("Ya tienes cuenta: entra con tu DNI y tu contraseña.");
        }

        // El nombre de usuario no es el DNI: la fila de una cuenta eliminada sigue
        // existiendo y bloquearía activar otra. El cliente entra con su DNI.
        var usuario = new Usuario
        {
            UserName = $"cliente.{Guid.NewGuid():N}",
            Email = email,
            EmailConfirmed = email != null,
            NombreCompleto = cliente.NombreCompleto,
            PhoneNumber = cliente.Telefono,
            Activo = true
        };

        var resultado = await _userManager.CreateAsync(usuario, request.Password ?? string.Empty);
        if (!resultado.Succeeded)
        {
            await transaccion.RollbackAsync();
            return ServiceResult<Usuario>.Invalid(string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }

        _context.UsuarioRoles.Add(new UsuarioRol { UsuarioId = usuario.Id, RolId = rol.Id });
        cliente.UsuarioId = usuario.Id;
        LimpiarCodigo(cliente);
        await _context.SaveChangesAsync();
        await transaccion.CommitAsync();

        await _auditoria.RegistrarEventoAsync(usuario.Id, "ActivarCuenta", "Cliente", cliente.Id.ToString(),
            new { UsuarioId = usuario.Id, ConCorreo = email != null });

        return ServiceResult<Usuario>.Success(usuario);
    }

    private static string? Documento(Cliente cliente) =>
        string.IsNullOrWhiteSpace(cliente.NumeroDocumento) ? cliente.DocumentoIdentidad : cliente.NumeroDocumento;

    private static bool CodigoCorrecto(Cliente cliente, string codigo) =>
        cliente.CodigoActivacionHash != null
        && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(cliente.CodigoActivacionHash),
            Encoding.ASCII.GetBytes(Hash(cliente.Id, codigo)));

    private static string Hash(Guid clienteId, string codigo) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{clienteId:N}:{codigo}")));

    private static void LimpiarCodigo(Cliente cliente)
    {
        cliente.CodigoActivacionHash = null;
        cliente.CodigoActivacionVence = null;
        cliente.CodigoActivacionIntentos = 0;
        cliente.FechaModificacion = DateTime.UtcNow;
    }
}
