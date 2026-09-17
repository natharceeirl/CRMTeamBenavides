namespace CRMTeamBenavides.Api.Features.Dashboard;

public record OrdenesServicioResumenResponse(
    int Abierta,
    int Diagnostico,
    int Aprobada,
    int EnProceso,
    int Lista,
    int Entregada,
    int Cancelada,
    int Total);

public record VentasResumenResponse(
    int Confirmadas,
    int Cotizaciones,
    int Anuladas,
    decimal MontoConfirmadas,
    decimal TicketPromedio);

public record InventarioResumenResponse(
    int ProductosConStockBajo,
    int ProductosSinStock,
    decimal ValorEstimadoInventario);

public record ClientesVehiculosResumenResponse(
    int ClientesActivos,
    int VehiculosActivos);

public record DashboardResumenResponse(
    DateTime? FechaDesde,
    DateTime? FechaHasta,
    OrdenesServicioResumenResponse OrdenesServicio,
    VentasResumenResponse Ventas,
    InventarioResumenResponse Inventario,
    ClientesVehiculosResumenResponse CrmActivos);
