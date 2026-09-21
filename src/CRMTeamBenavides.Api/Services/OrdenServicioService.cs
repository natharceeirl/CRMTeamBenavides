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

    public async Task<List<OrdenServicioResponse>> GetAllAsync(Guid? vehiculoId, EstadoOrdenServicio? estado, Guid? clienteId = null)
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

        if (clienteId.HasValue)
        {
            query = query.Where(o => o.Vehiculo.ClienteId == clienteId.Value);
        }

        // Proyección a tipo anónimo: EF Core genera un único JOIN en SQL y trae solo las columnas necesarias.
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
            .Include(o => o.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
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
            tecnico = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == request.TecnicoAsignadoId.Value && u.Activo);

            if (tecnico is null)
            {
                return ServiceResult<OrdenServicioResponse>.Invalid("El técnico asignado no existe o está inactivo.");
            }
        }

        var orden = new OrdenServicio
        {
            VehiculoId        = request.VehiculoId,
            TecnicoAsignadoId = request.TecnicoAsignadoId,
            Observaciones     = request.Observaciones,
            Estado            = EstadoOrdenServicio.Abierta,
            FechaApertura     = DateTime.UtcNow,
            FechaCreacion     = DateTime.UtcNow,
            Activo            = true
        };

        _context.OrdenesServicio.Add(orden);
        await _context.SaveChangesAsync();

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

        if (request.Observaciones is not null)
        {
            orden.Observaciones = request.Observaciones;
        }

        orden.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
    }

    public async Task<ServiceResult<DetalleServicioResponse>> AgregarDetalleAsync(Guid ordenServicioId, AgregarDetalleServicioRequest request)
    {
        if (request.Cantidad <= 0)
        {
            return ServiceResult<DetalleServicioResponse>.Invalid("La cantidad debe ser mayor a 0.");
        }

        var orden = await _context.OrdenesServicio
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo);

        if (orden is null)
        {
            return ServiceResult<DetalleServicioResponse>.NotFound();
        }

        if (orden.Estado == EstadoOrdenServicio.Entregada || orden.Estado == EstadoOrdenServicio.Cancelada)
        {
            return ServiceResult<DetalleServicioResponse>.Invalid(
                $"No se pueden agregar detalles a una orden que se encuentra en estado '{orden.Estado}'.");
        }

        if (orden.Estado == EstadoOrdenServicio.Lista)
        {
            return ServiceResult<DetalleServicioResponse>.Invalid(
                "No se pueden agregar detalles a una orden en estado 'Lista'. Debe reabrirse a 'EnProceso' para realizar trabajos adicionales.");
        }

        // --- Caso 1: Repuesto / Producto de inventario ---
        if (request.ProductoId.HasValue)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Bloqueo pesimista a nivel de fila (FOR UPDATE) para evitar condiciones de carrera en stock
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {request.ProductoId.Value} FOR UPDATE");

                var producto = await _context.Productos
                    .FirstOrDefaultAsync(p => p.Id == request.ProductoId.Value && p.Activo);

                if (producto is null)
                {
                    return ServiceResult<DetalleServicioResponse>.Invalid("El producto indicado no existe o está inactivo.");
                }

                if (producto.StockActual < request.Cantidad)
                {
                    return ServiceResult<DetalleServicioResponse>.Invalid(
                        $"Stock insuficiente para el producto '{producto.Nombre}'. Stock disponible: {producto.StockActual}, solicitado: {request.Cantidad}.");
                }

                producto.StockActual -= request.Cantidad;
                producto.FechaModificacion = DateTime.UtcNow;

                var movimiento = new MovimientoInventario
                {
                    ProductoId      = producto.Id,
                    Tipo            = TipoMovimientoInventario.Salida,
                    Cantidad        = request.Cantidad,
                    Motivo          = $"Asignación a Orden de Servicio #{orden.Id}",
                    OrdenServicioId = orden.Id,
                    FechaCreacion   = DateTime.UtcNow,
                    Activo          = true
                };
                _context.MovimientosInventario.Add(movimiento);

                var descripcion = !string.IsNullOrWhiteSpace(request.Descripcion)
                    ? request.Descripcion.Trim()
                    : producto.Nombre;

                var detalle = new DetalleServicio
                {
                    OrdenServicioId = orden.Id,
                    ProductoId      = producto.Id,
                    Producto        = producto,
                    Descripcion     = descripcion,
                    Cantidad        = request.Cantidad,
                    PrecioUnitario  = producto.PrecioVenta, // Autoridad de precio: catálogo oficial
                    FechaCreacion   = DateTime.UtcNow,
                    Activo          = true
                };
                _context.DetallesServicio.Add(detalle);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<DetalleServicioResponse>.Success(new DetalleServicioResponse(
                    detalle.Id,
                    detalle.ProductoId,
                    producto.Codigo,
                    detalle.Descripcion,
                    detalle.Cantidad,
                    detalle.PrecioUnitario,
                    detalle.Cantidad * detalle.PrecioUnitario,
                    true));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // --- Caso 2: Mano de Obra / Servicio ---
        if (string.IsNullOrWhiteSpace(request.Descripcion))
        {
            return ServiceResult<DetalleServicioResponse>.Invalid("La descripción del servicio o mano de obra es obligatoria.");
        }

        if (!request.PrecioUnitario.HasValue || request.PrecioUnitario.Value < 0)
        {
            return ServiceResult<DetalleServicioResponse>.Invalid("El precio unitario de la mano de obra es obligatorio y no puede ser negativo.");
        }

        var detalleServicio = new DetalleServicio
        {
            OrdenServicioId = orden.Id,
            ProductoId      = null,
            Descripcion     = request.Descripcion.Trim(),
            Cantidad        = request.Cantidad,
            PrecioUnitario  = request.PrecioUnitario.Value,
            FechaCreacion   = DateTime.UtcNow,
            Activo          = true
        };

        _context.DetallesServicio.Add(detalleServicio);
        await _context.SaveChangesAsync();

        return ServiceResult<DetalleServicioResponse>.Success(new DetalleServicioResponse(
            detalleServicio.Id,
            null,
            null,
            detalleServicio.Descripcion,
            detalleServicio.Cantidad,
            detalleServicio.PrecioUnitario,
            detalleServicio.Cantidad * detalleServicio.PrecioUnitario,
            false));
    }

    public async Task<ServiceResult<bool>> EliminarDetalleAsync(Guid ordenServicioId, Guid detalleId)
    {
        var orden = await _context.OrdenesServicio
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo);

        if (orden is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        if (orden.Estado == EstadoOrdenServicio.Entregada || orden.Estado == EstadoOrdenServicio.Cancelada)
        {
            return ServiceResult<bool>.Invalid(
                $"No se pueden eliminar detalles de una orden que se encuentra en estado '{orden.Estado}'.");
        }

        if (orden.Estado == EstadoOrdenServicio.Lista)
        {
            return ServiceResult<bool>.Invalid(
                "No se pueden eliminar detalles de una orden en estado 'Lista'. Debe reabrirse a 'EnProceso' si requiere ajustes.");
        }

        var detalle = await _context.DetallesServicio
            .FirstOrDefaultAsync(d => d.Id == detalleId && d.OrdenServicioId == ordenServicioId && d.Activo);

        if (detalle is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        if (detalle.ProductoId.HasValue)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Bloqueo pesimista a nivel de fila (FOR UPDATE) para evitar condiciones de carrera en stock
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {detalle.ProductoId.Value} FOR UPDATE");

                var producto = await _context.Productos
                    .FirstOrDefaultAsync(p => p.Id == detalle.ProductoId.Value);

                if (producto is not null)
                {
                    producto.StockActual += detalle.Cantidad;
                    producto.FechaModificacion = DateTime.UtcNow;

                    var movimiento = new MovimientoInventario
                    {
                        ProductoId      = producto.Id,
                        Tipo            = TipoMovimientoInventario.Entrada,
                        Cantidad        = detalle.Cantidad,
                        Motivo          = $"Devolución por eliminación de ítem en Orden de Servicio #{orden.Id}",
                        OrdenServicioId = orden.Id,
                        FechaCreacion   = DateTime.UtcNow,
                        Activo          = true
                    };
                    _context.MovimientosInventario.Add(movimiento);
                }

                detalle.Activo = false;
                detalle.FechaModificacion = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<bool>.Success(true);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        detalle.Activo = false;
        detalle.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<OrdenServicioResponse>> CambiarEstadoAsync(Guid ordenServicioId, CambiarEstadoOrdenServicioRequest request)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.TecnicoAsignado)
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo);

        if (orden is null)
        {
            return ServiceResult<OrdenServicioResponse>.NotFound();
        }

        if (orden.Estado == EstadoOrdenServicio.Entregada || orden.Estado == EstadoOrdenServicio.Cancelada)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(
                $"No se puede cambiar el estado de una orden que ya se encuentra en estado terminal '{orden.Estado}'.");
        }

        if (orden.Estado == request.NuevoEstado)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(
                $"La orden ya se encuentra en estado '{orden.Estado}'.");
        }

        bool esTransicionValida = (orden.Estado, request.NuevoEstado) switch
        {
            (EstadoOrdenServicio.Abierta, EstadoOrdenServicio.Diagnostico) => true,
            (EstadoOrdenServicio.Abierta, EstadoOrdenServicio.Cancelada)   => true,

            (EstadoOrdenServicio.Diagnostico, EstadoOrdenServicio.Aprobada)  => true,
            (EstadoOrdenServicio.Diagnostico, EstadoOrdenServicio.Cancelada) => true,

            (EstadoOrdenServicio.Aprobada, EstadoOrdenServicio.EnProceso) => true,
            (EstadoOrdenServicio.Aprobada, EstadoOrdenServicio.Cancelada) => true,

            (EstadoOrdenServicio.EnProceso, EstadoOrdenServicio.Lista)     => true,
            (EstadoOrdenServicio.EnProceso, EstadoOrdenServicio.Cancelada) => true,

            (EstadoOrdenServicio.Lista, EstadoOrdenServicio.Entregada) => true,
            (EstadoOrdenServicio.Lista, EstadoOrdenServicio.EnProceso) => true, // Reingreso técnico por ajuste
            (EstadoOrdenServicio.Lista, EstadoOrdenServicio.Cancelada) => true,

            _ => false
        };

        if (!esTransicionValida)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(
                $"Transición de estado no permitida de '{orden.Estado}' a '{request.NuevoEstado}'.");
        }

        // --- Transición a Cancelada: revertir stock de todos los repuestos activos ---
        if (request.NuevoEstado == EstadoOrdenServicio.Cancelada)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var repuestosActivos = await _context.DetallesServicio
                    .Where(d => d.OrdenServicioId == orden.Id && d.Activo && d.ProductoId != null)
                    .ToListAsync();

                var orderedProductIds = repuestosActivos
                    .Select(r => r.ProductoId!.Value)
                    .Distinct()
                    .OrderBy(pId => pId)
                    .ToList();

                // Bloqueo pesimista a nivel de fila (FOR UPDATE) en orden determinista para evitar deadlocks y condiciones de carrera
                foreach (var prodId in orderedProductIds)
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {prodId} FOR UPDATE");
                }

                var productos = await _context.Productos
                    .Where(p => orderedProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                foreach (var repuesto in repuestosActivos)
                {
                    if (productos.TryGetValue(repuesto.ProductoId!.Value, out var producto))
                    {
                        producto.StockActual += repuesto.Cantidad;
                        producto.FechaModificacion = DateTime.UtcNow;

                        var movimiento = new MovimientoInventario
                        {
                            ProductoId      = producto.Id,
                            Tipo            = TipoMovimientoInventario.Entrada,
                            Cantidad        = repuesto.Cantidad,
                            Motivo          = $"Devolución por cancelación de Orden de Servicio #{orden.Id}",
                            OrdenServicioId = orden.Id,
                            FechaCreacion   = DateTime.UtcNow,
                            Activo          = true
                        };
                        _context.MovimientosInventario.Add(movimiento);
                    }

                    repuesto.Activo = false;
                    repuesto.FechaModificacion = DateTime.UtcNow;
                }

                orden.Estado            = EstadoOrdenServicio.Cancelada;
                orden.FechaCierre       = DateTime.UtcNow;
                orden.FechaModificacion = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(request.Observaciones))
                {
                    orden.Observaciones = request.Observaciones;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // --- Transición a Entregada: fijar fecha de cierre ---
        if (request.NuevoEstado == EstadoOrdenServicio.Entregada)
        {
            orden.Estado            = EstadoOrdenServicio.Entregada;
            orden.FechaCierre       = DateTime.UtcNow;
            orden.FechaModificacion = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Observaciones))
            {
                orden.Observaciones = request.Observaciones;
            }

            await _context.SaveChangesAsync();
            return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
        }

        // --- Demás transiciones intermedias (Aprobada, EnProceso, Lista) ---
        orden.Estado            = request.NuevoEstado;
        orden.FechaModificacion = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Observaciones))
        {
            orden.Observaciones = request.Observaciones;
        }

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

    private static OrdenServicioDetalleResponse MapToDetalleResponse(OrdenServicio orden)
    {
        var detalles = orden.Detalles
            .Where(d => d.Activo)
            .OrderBy(d => d.FechaCreacion)
            .Select(d => new DetalleServicioResponse(
                d.Id,
                d.ProductoId,
                d.Producto?.Codigo,
                d.Descripcion,
                d.Cantidad,
                d.PrecioUnitario,
                d.Cantidad * d.PrecioUnitario,
                d.ProductoId.HasValue))
            .ToList();

        var total = detalles.Sum(d => d.Subtotal);

        return new OrdenServicioDetalleResponse(
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
            orden.Activo,
            detalles,
            total);
    }
}
