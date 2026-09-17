using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Inventario;

public static class InventarioEndpoints
{
    public static void MapInventarioEndpoints(this IEndpointRouteBuilder app)
    {
        // --- Endpoints de Productos y operaciones sobre productos ---
        var groupProductos = app.MapGroup("/api/productos").RequireAuthorization();

        groupProductos.MapGet("/", async (
            Guid? categoriaId,
            string? busqueda,
            bool? bajoStock,
            IInventarioService service) =>
        {
            var productos = await service.GetAllProductosAsync(categoriaId, busqueda, bajoStock);
            return Results.Ok(productos);
        })
        .WithName("GetProductos");

        groupProductos.MapGet("/{id:guid}", async (Guid id, IInventarioService service) =>
        {
            var result = await service.GetProductoByIdAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("GetProductoById");

        groupProductos.MapPost("/", async (CreateProductoRequest request, IInventarioService service) =>
        {
            var result = await service.CreateProductoAsync(request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/productos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CreateProducto");

        groupProductos.MapPut("/{id:guid}", async (Guid id, UpdateProductoRequest request, IInventarioService service) =>
        {
            var result = await service.UpdateProductoAsync(id, request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("UpdateProducto");

        groupProductos.MapDelete("/{id:guid}", async (Guid id, IInventarioService service) =>
        {
            var result = await service.DeleteProductoAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("DeleteProducto");

        groupProductos.MapPost("/{id:guid}/entradas", async (Guid id, RegistrarEntradaRequest request, IInventarioService service) =>
        {
            var result = await service.RegistrarEntradaAsync(id, request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("RegistrarEntradaInventario");

        groupProductos.MapPost("/{id:guid}/salidas", async (Guid id, RegistrarSalidaRequest request, IInventarioService service) =>
        {
            var result = await service.RegistrarSalidaAsync(id, request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("RegistrarSalidaInventario");

        groupProductos.MapPost("/{id:guid}/ajustes", async (Guid id, RegistrarAjusteRequest request, IInventarioService service) =>
        {
            var result = await service.RegistrarAjusteAsync(id, request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("RegistrarAjusteInventario");

        groupProductos.MapGet("/{id:guid}/movimientos", async (Guid id, IInventarioService service) =>
        {
            var result = await service.GetMovimientosByProductoAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("GetMovimientosByProducto");

        // --- Endpoints globales de Inventario ---
        var groupInventario = app.MapGroup("/api/inventario").RequireAuthorization();

        groupInventario.MapGet("/movimientos", async (
            Guid? productoId,
            TipoMovimientoInventario? tipo,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            IInventarioService service) =>
        {
            var movimientos = await service.GetAllMovimientosAsync(productoId, tipo, fechaDesde, fechaHasta);
            return Results.Ok(movimientos);
        })
        .WithName("GetMovimientosInventario");
    }
}
