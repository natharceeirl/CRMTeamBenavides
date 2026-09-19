using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Ventas;

public static class VentaEndpoints
{
    public static void MapVentaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ventas").RequireAuthorization();

        group.MapGet("/", async (
            Guid? clienteId,
            EstadoVenta? estado,
            Guid? ordenServicioId,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            IVentaService service) =>
        {
            var ventas = await service.GetAllAsync(clienteId, estado, ordenServicioId, fechaDesde, fechaHasta);
            return Results.Ok(ventas);
        })
        .WithName("GetVentas");

        group.MapGet("/{id:guid}", async (Guid id, IVentaService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("GetVentaById");

        group.MapPost("/", async (CreateVentaRequest request, IVentaService service) =>
        {
            var result = await service.CreateAsync(request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ventas/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CreateVenta");

        group.MapPut("/{id:guid}/confirmar", async (Guid id, IVentaService service) =>
        {
            var result = await service.ConfirmarCotizacionAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("ConfirmarCotizacion");

        group.MapPut("/{id:guid}/anular", async (Guid id, IVentaService service) =>
        {
            var result = await service.AnularAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("AnularVenta");

        group.MapGet("/{id:guid}/comprobante", async (Guid id, IVentaService service) =>
        {
            var result = await service.GetComprobanteAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("GetComprobanteVenta");

        group.MapPost("/{id:guid}/comprobante", async (Guid id, RegistrarComprobanteRequest request, IVentaService service) =>
        {
            var result = await service.RegistrarComprobanteAsync(id, request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ventas/{id}/comprobante", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("RegistrarComprobanteVenta");

        group.MapPut("/{id:guid}/comprobante/anular", async (Guid id, IVentaService service) =>
        {
            var result = await service.AnularComprobanteAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("AnularComprobanteVenta");
    }
}
