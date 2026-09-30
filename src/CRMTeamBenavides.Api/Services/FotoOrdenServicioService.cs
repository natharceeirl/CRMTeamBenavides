using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Fotos;
using CRMTeamBenavides.Api.Services.Almacenamiento;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class FotoOrdenServicioService : IFotoOrdenServicioService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAlmacenamientoArchivoService _almacenamientoService;
    private readonly ILogger<FotoOrdenServicioService> _logger;

    public FotoOrdenServicioService(
        ApplicationDbContext dbContext,
        IAlmacenamientoArchivoService almacenamientoService,
        ILogger<FotoOrdenServicioService> logger)
    {
        _dbContext = dbContext;
        _almacenamientoService = almacenamientoService;
        _logger = logger;
    }

    public async Task<ServiceResult<FotoOrdenServicioResponse>> SubirFotoAsync(
        Guid ordenServicioId,
        Stream stream,
        string nombreOriginal,
        string contentType,
        long tamanioBytes,
        EtapaFotoOrdenServicio etapa,
        string? observacion,
        Guid usuarioId,
        UserIsolationContext isolation,
        CancellationToken ct = default)
    {
        var orden = await _dbContext.OrdenesServicio
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo, ct);

        if (orden == null)
        {
            return ServiceResult<FotoOrdenServicioResponse>.NotFound("La orden de servicio especificada no existe.");
        }

        // Regla: Clientes nunca pueden subir fotos a una OS
        if (isolation.EsCliente)
        {
            return ServiceResult<FotoOrdenServicioResponse>.Forbidden("Los clientes no tienen permitido subir fotografías.");
        }

        // Regla: Técnico solo puede subir fotos a órdenes asignadas a su cargo
        if (isolation.EsTecnico && !isolation.EsStaff)
        {
            if (orden.TecnicoAsignadoId != isolation.UsuarioId)
            {
                return ServiceResult<FotoOrdenServicioResponse>.Forbidden("El técnico solo puede gestionar fotografías de las órdenes de servicio asignadas.");
            }
        }

        if (orden.Estado == EstadoOrdenServicio.Cancelada)
        {
            return ServiceResult<FotoOrdenServicioResponse>.Invalid("No se pueden registrar fotografías en una orden de servicio cancelada.");
        }

        try
        {
            var subcarpeta = $"os_{ordenServicioId:N}";
            var (rutaRelativa, nombreAlmacenado) = await _almacenamientoService.GuardarFotoAsync(
                stream,
                nombreOriginal,
                contentType,
                subcarpeta,
                ct);

            var foto = new FotoOrdenServicio
            {
                OrdenServicioId = ordenServicioId,
                NombreArchivoOriginal = Path.GetFileName(nombreOriginal),
                NombreArchivoAlmacenado = nombreAlmacenado,
                RutaRelativa = rutaRelativa,
                ContentType = contentType,
                TamanioBytes = tamanioBytes,
                Etapa = etapa,
                UsuarioId = usuarioId,
                Observacion = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim(),
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            };

            _dbContext.FotosOrdenServicio.Add(foto);
            await _dbContext.SaveChangesAsync(ct);

            var usuario = await _dbContext.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == usuarioId, ct);

            var response = new FotoOrdenServicioResponse(
                foto.Id,
                foto.OrdenServicioId,
                foto.NombreArchivoOriginal,
                $"/api/ordenes-servicio/{foto.OrdenServicioId}/fotos/{foto.Id}/archivo",
                foto.ContentType,
                foto.TamanioBytes,
                foto.Etapa,
                foto.Etapa.ToString(),
                foto.UsuarioId,
                usuario?.NombreCompleto,
                foto.Observacion,
                foto.FechaCreacion);

            return ServiceResult<FotoOrdenServicioResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult<FotoOrdenServicioResponse>.Invalid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<FotoOrdenServicioResponse>.Invalid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al guardar fotografía para orden {OrdenId}", ordenServicioId);
            return ServiceResult<FotoOrdenServicioResponse>.Invalid("Ocurrió un error al procesar la fotografía.");
        }
    }

    public async Task<ServiceResult<IReadOnlyList<FotoOrdenServicioResponse>>> GetFotosByOrdenIdAsync(
        Guid ordenServicioId,
        UserIsolationContext isolation,
        CancellationToken ct = default)
    {
        var orden = await _dbContext.OrdenesServicio
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo, ct);

        if (orden == null)
        {
            return ServiceResult<IReadOnlyList<FotoOrdenServicioResponse>>.NotFound("Orden de servicio no encontrada.");
        }

        // Aislamiento cliente: solo sus propias órdenes
        if (isolation.SoloClienteId.HasValue && orden.ClienteId != isolation.SoloClienteId.Value)
        {
            return ServiceResult<IReadOnlyList<FotoOrdenServicioResponse>>.NotFound();
        }

        // Aislamiento técnico: solo órdenes asignadas
        if (isolation.EsTecnico && !isolation.EsStaff && orden.TecnicoAsignadoId != isolation.UsuarioId)
        {
            return ServiceResult<IReadOnlyList<FotoOrdenServicioResponse>>.NotFound();
        }

        var fotos = await _dbContext.FotosOrdenServicio
            .AsNoTracking()
            .Include(f => f.Usuario)
            .Where(f => f.OrdenServicioId == ordenServicioId && f.Activo)
            .OrderByDescending(f => f.FechaCreacion)
            .Select(f => new FotoOrdenServicioResponse(
                f.Id,
                f.OrdenServicioId,
                f.NombreArchivoOriginal,
                $"/api/ordenes-servicio/{f.OrdenServicioId}/fotos/{f.Id}/archivo",
                f.ContentType,
                f.TamanioBytes,
                f.Etapa,
                f.Etapa.ToString(),
                f.UsuarioId,
                f.Usuario != null ? f.Usuario.NombreCompleto : null,
                f.Observacion,
                f.FechaCreacion))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<FotoOrdenServicioResponse>>.Success(fotos);
    }

    public async Task<ServiceResult<FotoOrdenServicioResponse>> GetFotoByIdAsync(
        Guid ordenServicioId,
        Guid fotoId,
        UserIsolationContext isolation,
        CancellationToken ct = default)
    {
        var orden = await _dbContext.OrdenesServicio
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo, ct);

        if (orden == null)
        {
            return ServiceResult<FotoOrdenServicioResponse>.NotFound("Orden de servicio no encontrada.");
        }

        if (isolation.SoloClienteId.HasValue && orden.ClienteId != isolation.SoloClienteId.Value)
        {
            return ServiceResult<FotoOrdenServicioResponse>.NotFound();
        }

        if (isolation.EsTecnico && !isolation.EsStaff && orden.TecnicoAsignadoId != isolation.UsuarioId)
        {
            return ServiceResult<FotoOrdenServicioResponse>.NotFound();
        }

        var foto = await _dbContext.FotosOrdenServicio
            .AsNoTracking()
            .Include(f => f.Usuario)
            .FirstOrDefaultAsync(f => f.Id == fotoId && f.OrdenServicioId == ordenServicioId && f.Activo, ct);

        if (foto == null)
        {
            return ServiceResult<FotoOrdenServicioResponse>.NotFound("Fotografía no encontrada.");
        }

        var response = new FotoOrdenServicioResponse(
            foto.Id,
            foto.OrdenServicioId,
            foto.NombreArchivoOriginal,
            $"/api/ordenes-servicio/{foto.OrdenServicioId}/fotos/{foto.Id}/archivo",
            foto.ContentType,
            foto.TamanioBytes,
            foto.Etapa,
            foto.Etapa.ToString(),
            foto.UsuarioId,
            foto.Usuario != null ? foto.Usuario.NombreCompleto : null,
            foto.Observacion,
            foto.FechaCreacion);

        return ServiceResult<FotoOrdenServicioResponse>.Success(response);
    }

    public async Task<ServiceResult<(Stream Stream, string ContentType, string NombreOriginal)>> GetArchivoFotoAsync(
        Guid ordenServicioId,
        Guid fotoId,
        UserIsolationContext isolation,
        CancellationToken ct = default)
    {
        var orden = await _dbContext.OrdenesServicio
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo, ct);

        if (orden == null)
        {
            return ServiceResult<(Stream, string, string)>.NotFound("Orden de servicio no encontrada.");
        }

        if (isolation.SoloClienteId.HasValue && orden.ClienteId != isolation.SoloClienteId.Value)
        {
            return ServiceResult<(Stream, string, string)>.NotFound();
        }

        if (isolation.EsTecnico && !isolation.EsStaff && orden.TecnicoAsignadoId != isolation.UsuarioId)
        {
            return ServiceResult<(Stream, string, string)>.NotFound();
        }

        var foto = await _dbContext.FotosOrdenServicio
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fotoId && f.OrdenServicioId == ordenServicioId && f.Activo, ct);

        if (foto == null)
        {
            return ServiceResult<(Stream, string, string)>.NotFound("Fotografía no encontrada.");
        }

        var archivo = await _almacenamientoService.ObtenerArchivoAsync(foto.RutaRelativa, ct);
        if (archivo == null)
        {
            return ServiceResult<(Stream, string, string)>.NotFound("El archivo físico no se encuentra disponible.");
        }

        return ServiceResult<(Stream, string, string)>.Success(
            (archivo.Value.stream, archivo.Value.contentType, foto.NombreArchivoOriginal));
    }

    public async Task<ServiceResult<bool>> EliminarFotoAsync(
        Guid ordenServicioId,
        Guid fotoId,
        Guid usuarioId,
        UserIsolationContext isolation,
        CancellationToken ct = default)
    {
        var orden = await _dbContext.OrdenesServicio
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo, ct);

        if (orden == null)
        {
            return ServiceResult<bool>.NotFound("Orden de servicio no encontrada.");
        }

        if (isolation.EsCliente)
        {
            return ServiceResult<bool>.Forbidden("Los clientes no tienen permitido eliminar fotografías.");
        }

        if (isolation.EsTecnico && !isolation.EsStaff)
        {
            if (orden.TecnicoAsignadoId != isolation.UsuarioId)
            {
                return ServiceResult<bool>.Forbidden("El técnico solo puede gestionar fotografías de las órdenes de servicio asignadas.");
            }
        }

        var foto = await _dbContext.FotosOrdenServicio
            .FirstOrDefaultAsync(f => f.Id == fotoId && f.OrdenServicioId == ordenServicioId && f.Activo, ct);

        if (foto == null)
        {
            return ServiceResult<bool>.NotFound("Fotografía no encontrada.");
        }

        foto.Activo = false;
        foto.ModificadoPorId = usuarioId;
        foto.FechaModificacion = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        // Limpieza física en segundo plano (tolerante a fallos si el archivo ya no existía)
        await _almacenamientoService.EliminarArchivoAsync(foto.RutaRelativa, ct);

        return ServiceResult<bool>.Success(true);
    }
}
