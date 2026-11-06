using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services.ConsultaDocumento;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CRMTeamBenavides.Api.Features.ConsultaDocumento;

/// <summary>Completa nombres por DNI y razón social por RUC al registrar clientes y proveedores.</summary>
public static class ConsultaDocumentoEndpoints
{
    /// <summary>Quien registra o edita clientes, o registra compras (proveedores).</summary>
    public const string Politica = "ConsultarDocumento";

    /// <summary>Consultas por usuario y minuto: cuida la cuota mensual de Decolecta.</summary>
    public const int ConsultasPorMinuto = 20;

    private sealed class Contador
    {
        public int Valor;
    }

    public static void MapConsultaDocumentoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/consulta-documento/{numero}", async (
            string numero,
            ClaimsPrincipal user,
            IConsultaDocumentoService servicio,
            ApplicationDbContext db,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            var limpio = new string(numero.Where(char.IsAsciiDigit).ToArray());
            if (limpio.Length is not (8 or 11) || limpio.Length != numero.Trim().Length)
                return Results.BadRequest(new { mensaje = "El DNI tiene 8 dígitos y el RUC 11." });

            var usuario = UserIsolationHelper.ObtenerUsuarioId(user)?.ToString() ?? "sin-usuario";
            var contador = cache.GetOrCreate($"consulta-documento:{usuario}:{DateTime.UtcNow:yyyyMMddHHmm}", entrada =>
            {
                entrada.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return new Contador();
            })!;
            if (Interlocked.Increment(ref contador.Valor) > ConsultasPorMinuto)
            {
                return Results.Json(new { mensaje = "Demasiadas consultas seguidas: espera un minuto o escribe el nombre a mano." },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var resultado = await servicio.ConsultarAsync(limpio, ct);
            if (resultado.Resultado == ResultadoConsultaDocumento.NoEncontrado)
                return Results.NotFound(new { mensaje = resultado.Mensaje });
            if (resultado.Resultado == ResultadoConsultaDocumento.NoDisponible)
                return Results.Json(new { mensaje = resultado.Mensaje }, statusCode: StatusCodes.Status503ServiceUnavailable);

            // Aviso temprano: ese documento ya es de un cliente registrado.
            var existente = await db.Clientes.AsNoTracking()
                .Where(c => c.Activo && (c.NumeroDocumento == limpio || c.DocumentoIdentidad == limpio))
                .Select(c => new { c.Id, c.NombreCompleto })
                .FirstOrDefaultAsync(ct);

            return Results.Ok(resultado.Datos! with
            {
                ClienteExistenteId = existente?.Id,
                ClienteExistenteNombre = existente?.NombreCompleto,
            });
        })
        .RequireAuthorization(Politica)
        .WithName("ConsultarDocumento");
    }
}
