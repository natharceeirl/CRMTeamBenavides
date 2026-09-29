using CRMTeamBenavides.Api.Configuration.Autorizacion;
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
    private readonly IConfiguracionService _configuracionService;

    public OrdenServicioService(
        ApplicationDbContext context,
        UserManager<Usuario> userManager,
        IConfiguracionService configuracionService)
    {
        _context = context;
        _userManager = userManager;
        _configuracionService = configuracionService;
    }

    private async Task<string> GenerarNumeroOrdenAsync()
    {
        var seqVal = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('\"OrdenServicioNumeroSeq\"') AS \"Value\"")
            .SingleAsync();
        return $"OS-{seqVal:D6}";
    }

    public async Task<List<OrdenServicioResponse>> GetAllAsync(
        Guid? vehiculoId = null,
        EstadoOrdenServicio? estado = null,
        Guid? clienteId = null,
        Guid? soloTecnicoId = null,
        Guid? soloClienteId = null,
        string? busqueda = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        Guid? tecnicoId = null)
    {
        var query = _context.OrdenesServicio
            .Include(o => o.Vehiculo)
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoAsignado)
            .Include(o => o.Ventas)
                .ThenInclude(v => v.Comprobante)
            .Where(o => o.Activo);

        // Aislamiento RBAC de Técnico
        if (soloTecnicoId.HasValue)
        {
            query = query.Where(o => o.TecnicoAsignadoId == soloTecnicoId.Value);
        }

        // Aislamiento RBAC de Cliente
        if (soloClienteId.HasValue)
        {
            query = query.Where(o => o.ClienteId == soloClienteId.Value || o.Vehiculo.ClienteId == soloClienteId.Value);
        }
        else if (clienteId.HasValue)
        {
            query = query.Where(o => o.ClienteId == clienteId.Value || o.Vehiculo.ClienteId == clienteId.Value);
        }

        if (vehiculoId.HasValue)
        {
            query = query.Where(o => o.VehiculoId == vehiculoId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(o => o.Estado == estado.Value);
        }

        if (tecnicoId.HasValue && !soloTecnicoId.HasValue)
        {
            query = query.Where(o => o.TecnicoAsignadoId == tecnicoId.Value);
        }

        if (fechaDesde.HasValue)
        {
            query = query.Where(o => o.FechaIngreso >= fechaDesde.Value || o.FechaApertura >= fechaDesde.Value);
        }

        if (fechaHasta.HasValue)
        {
            query = query.Where(o => o.FechaIngreso <= fechaHasta.Value || o.FechaApertura <= fechaHasta.Value);
        }

        // Búsqueda amplia por: número OS, placa, VIN/serie, marca, modelo, color, cliente, documento, técnico, falla, diagnóstico
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim().ToLower();
            query = query.Where(o =>
                (o.NumeroOrden != null && o.NumeroOrden.ToLower().Contains(term)) ||
                (o.Vehiculo.Placa != null && o.Vehiculo.Placa.ToLower().Contains(term)) ||
                (o.Vehiculo.NumeroSerieVIN != null && o.Vehiculo.NumeroSerieVIN.ToLower().Contains(term)) ||
                o.Vehiculo.Marca.ToLower().Contains(term) ||
                o.Vehiculo.Modelo.ToLower().Contains(term) ||
                (o.Vehiculo.Color != null && o.Vehiculo.Color.ToLower().Contains(term)) ||
                o.Cliente.NombreCompleto.ToLower().Contains(term) ||
                (o.Cliente.RazonSocial != null && o.Cliente.RazonSocial.ToLower().Contains(term)) ||
                (o.Cliente.DocumentoIdentidad != null && o.Cliente.DocumentoIdentidad.ToLower().Contains(term)) ||
                (o.TecnicoAsignado != null && o.TecnicoAsignado.NombreCompleto.ToLower().Contains(term)) ||
                (o.MotivoFalla != null && o.MotivoFalla.ToLower().Contains(term)) ||
                (o.Diagnostico != null && o.Diagnostico.ToLower().Contains(term)));
        }

        var lista = await query
            .OrderByDescending(o => o.FechaIngreso)
            .ToListAsync();

        return lista.Select(MapToResponse).ToList();
    }

    public async Task<ServiceResult<OrdenServicioDetalleResponse>> GetByIdAsync(
        Guid id,
        Guid? soloTecnicoId = null,
        Guid? soloClienteId = null)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoAsignado)
            .Include(o => o.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
            .Include(o => o.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Servicio)
            .Include(o => o.HistorialEstados.Where(h => h.Activo))
                .ThenInclude(h => h.Usuario)
            .Include(o => o.Ventas.Where(v => v.Activo))
                .ThenInclude(v => v.Comprobante)
            .FirstOrDefaultAsync(o => o.Id == id && o.Activo);

        if (orden is null)
        {
            return ServiceResult<OrdenServicioDetalleResponse>.NotFound();
        }

        if (soloTecnicoId.HasValue && orden.TecnicoAsignadoId != soloTecnicoId.Value)
        {
            return ServiceResult<OrdenServicioDetalleResponse>.NotFound();
        }

        var clienteAsociadoId = orden.ClienteId != Guid.Empty ? orden.ClienteId : orden.Vehiculo.ClienteId;
        if (soloClienteId.HasValue && clienteAsociadoId != soloClienteId.Value)
        {
            return ServiceResult<OrdenServicioDetalleResponse>.NotFound();
        }

        return ServiceResult<OrdenServicioDetalleResponse>.Success(MapToDetalleResponse(orden));
    }

    public async Task<ServiceResult<OrdenServicioResponse>> CreateAperturaAsync(
        AperturaOrdenServicioRequest request,
        Guid? usuarioId = null)
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

        var ahora = DateTime.UtcNow;

        var motivoEntrega = MotivoEntregaInvalida(request.FechaEstimadaEntrega, ahora);
        if (motivoEntrega is not null)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(motivoEntrega);
        }

        // Un medidor no retrocede: la lectura nueva no puede ser menor que la de
        // la unidad ni que la de sus órdenes anteriores.
        var lecturaNueva = LecturaSegunMedidor(
            vehiculo.TipoMedidor, request.KilometrajeIngreso, request.HorasUsoIngreso, request.LecturaMedidorIngreso);
        if (lecturaNueva.HasValue)
        {
            var ultimaDeOrdenes = await UltimaLecturaDeOrdenesAsync(vehiculo.Id, vehiculo.TipoMedidor);
            var ultimaRegistrada = new[] { LecturaActualDeUnidad(vehiculo), ultimaDeOrdenes }.Max();
            var motivoLectura = MotivoLecturaMenor(
                lecturaNueva.Value, ultimaRegistrada, vehiculo.TipoMedidor, "la última registrada para la unidad");
            if (motivoLectura is not null)
            {
                return ServiceResult<OrdenServicioResponse>.Invalid(motivoLectura);
            }

            AvanzarLecturaDeUnidad(vehiculo, lecturaNueva.Value);
        }

        var numeroOrden = await GenerarNumeroOrdenAsync();

        var orden = new OrdenServicio
        {
            VehiculoId             = request.VehiculoId,
            ClienteId              = vehiculo.ClienteId,
            TecnicoAsignadoId      = request.TecnicoAsignadoId,
            NumeroOrden            = numeroOrden,
            Observaciones          = request.Observaciones?.Trim(),
            MotivoFalla            = request.MotivoFalla?.Trim() ?? request.Observaciones?.Trim(),
            FechaEstimadaEntrega   = request.FechaEstimadaEntrega,
            TipoAtencion           = request.TipoAtencion,
            ModalidadAtencion      = request.ModalidadAtencion,
            TipoFalla              = request.TipoFalla,
            KilometrajeIngreso     = request.KilometrajeIngreso ?? vehiculo.Kilometraje,
            HorasUsoIngreso        = request.HorasUsoIngreso ?? vehiculo.HorasUso,
            LecturaMedidorIngreso  = request.LecturaMedidorIngreso ?? lecturaNueva ?? vehiculo.LecturaMedidorActual,
            Estado                 = EstadoOrdenServicio.Abierta,
            FechaApertura          = ahora,
            FechaIngreso           = ahora,
            FechaCreacion          = DateTime.UtcNow,
            Activo                 = true
        };

        _context.OrdenesServicio.Add(orden);

        // Registro de hito inicial en el historial de estados
        var primerHistorial = new HistorialEstadoOrden
        {
            OrdenServicioId = orden.Id,
            EstadoAnterior  = null,
            EstadoNuevo     = EstadoOrdenServicio.Abierta,
            UsuarioId       = usuarioId,
            FechaCambio     = DateTime.UtcNow,
            Observaciones   = "Apertura de orden de servicio",
            FechaCreacion   = DateTime.UtcNow,
            Activo          = true
        };
        _context.HistorialEstadosOrden.Add(primerHistorial);

        await _context.SaveChangesAsync();

        orden.Vehiculo = vehiculo;
        orden.Cliente  = vehiculo.Cliente;
        orden.TecnicoAsignado = tecnico;

        return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
    }

    public async Task<ServiceResult<OrdenServicioResponse>> RegistrarDiagnosticoAsync(
        Guid id,
        RegistrarDiagnosticoRequest request,
        Guid? soloTecnicoId = null,
        Guid? usuarioId = null)
    {
        if (string.IsNullOrWhiteSpace(request.Diagnostico))
        {
            return ServiceResult<OrdenServicioResponse>.Invalid("El texto del diagnóstico es obligatorio.");
        }

        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoAsignado)
            .FirstOrDefaultAsync(o => o.Id == id && o.Activo);

        if (orden is null)
        {
            return ServiceResult<OrdenServicioResponse>.NotFound();
        }

        if (soloTecnicoId.HasValue && orden.TecnicoAsignadoId != soloTecnicoId.Value)
        {
            return ServiceResult<OrdenServicioResponse>.Forbidden("No tiene autorización para registrar diagnósticos en órdenes asignadas a otro técnico.");
        }

        if (orden.Estado == EstadoOrdenServicio.Cancelada || orden.Estado == EstadoOrdenServicio.Entregada)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(
                $"No se puede registrar diagnóstico en una orden que se encuentra en estado '{orden.Estado}'.");
        }

        var motivoEntrega = MotivoEntregaInvalida(request.FechaEstimadaEntrega, IngresoDeOrden(orden));
        if (motivoEntrega is not null)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(motivoEntrega);
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

        var estadoAnterior = orden.Estado;
        if (orden.Estado == EstadoOrdenServicio.Abierta)
        {
            orden.Estado = EstadoOrdenServicio.Diagnostico;

            _context.HistorialEstadosOrden.Add(new HistorialEstadoOrden
            {
                OrdenServicioId = orden.Id,
                EstadoAnterior  = estadoAnterior,
                EstadoNuevo     = EstadoOrdenServicio.Diagnostico,
                UsuarioId       = usuarioId,
                FechaCambio     = DateTime.UtcNow,
                Observaciones   = string.IsNullOrWhiteSpace(request.Observaciones) ? "Diagnóstico inicial registrado" : request.Observaciones.Trim(),
                FechaCreacion   = DateTime.UtcNow,
                Activo          = true
            });
        }

        orden.Diagnostico = request.Diagnostico.Trim();

        if (request.Solucion is not null)
        {
            orden.Solucion = request.Solucion.Trim();
        }

        if (request.FechaEstimadaEntrega.HasValue)
        {
            orden.FechaEstimadaEntrega = request.FechaEstimadaEntrega.Value;
        }

        if (request.TipoFalla.HasValue)
        {
            orden.TipoFalla = request.TipoFalla.Value;
        }

        orden.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
    }

    public async Task<ServiceResult<OrdenServicioResponse>> ActualizarAsync(
        Guid id,
        ActualizarOrdenServicioRequest request,
        Guid? soloTecnicoId = null,
        Guid? usuarioId = null)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoAsignado)
            .FirstOrDefaultAsync(o => o.Id == id && o.Activo);

        if (orden is null)
        {
            return ServiceResult<OrdenServicioResponse>.NotFound();
        }

        if (soloTecnicoId.HasValue && orden.TecnicoAsignadoId != soloTecnicoId.Value)
        {
            return ServiceResult<OrdenServicioResponse>.Forbidden("No tiene autorización para modificar órdenes asignadas a otro técnico.");
        }

        if (orden.Estado == EstadoOrdenServicio.Cancelada || orden.Estado == EstadoOrdenServicio.Entregada)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(
                $"No se pueden modificar datos en una orden que se encuentra en estado '{orden.Estado}'.");
        }

        var motivoEntrega = MotivoEntregaInvalida(request.FechaEstimadaEntrega, IngresoDeOrden(orden));
        if (motivoEntrega is not null)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(motivoEntrega);
        }

        // La lectura de esta orden no puede ser menor que la de las órdenes que la
        // unidad tuvo antes. La lectura actual de la unidad no cuenta: pudo
        // registrarse después de esta orden.
        var lecturaNueva = LecturaSegunMedidor(
            orden.Vehiculo.TipoMedidor, request.KilometrajeIngreso, request.HorasUsoIngreso, request.LecturaMedidorIngreso);
        if (lecturaNueva.HasValue)
        {
            var ultimaAnterior = await UltimaLecturaDeOrdenesAsync(
                orden.VehiculoId, orden.Vehiculo.TipoMedidor, excluirOrdenId: orden.Id, antesDe: orden.FechaApertura);
            var motivoLectura = MotivoLecturaMenor(
                lecturaNueva.Value, ultimaAnterior, orden.Vehiculo.TipoMedidor, "la de la orden anterior de la unidad");
            if (motivoLectura is not null)
            {
                return ServiceResult<OrdenServicioResponse>.Invalid(motivoLectura);
            }

            AvanzarLecturaDeUnidad(orden.Vehiculo, lecturaNueva.Value);
            if (!request.LecturaMedidorIngreso.HasValue) orden.LecturaMedidorIngreso = lecturaNueva.Value;
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

        if (request.MotivoFalla is not null) orden.MotivoFalla = request.MotivoFalla.Trim();
        if (request.Diagnostico is not null) orden.Diagnostico = request.Diagnostico.Trim();
        if (request.Solucion is not null) orden.Solucion = request.Solucion.Trim();
        if (request.Observaciones is not null) orden.Observaciones = request.Observaciones.Trim();
        if (request.FechaEstimadaEntrega.HasValue) orden.FechaEstimadaEntrega = request.FechaEstimadaEntrega.Value;
        if (request.TipoAtencion.HasValue) orden.TipoAtencion = request.TipoAtencion.Value;
        if (request.ModalidadAtencion.HasValue) orden.ModalidadAtencion = request.ModalidadAtencion.Value;
        if (request.TipoFalla.HasValue) orden.TipoFalla = request.TipoFalla.Value;
        if (request.KilometrajeIngreso.HasValue) orden.KilometrajeIngreso = request.KilometrajeIngreso.Value;
        if (request.HorasUsoIngreso.HasValue) orden.HorasUsoIngreso = request.HorasUsoIngreso.Value;
        if (request.LecturaMedidorIngreso.HasValue) orden.LecturaMedidorIngreso = request.LecturaMedidorIngreso.Value;

        orden.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
    }

    public async Task<ServiceResult<List<HistorialEstadoOrdenResponse>>> GetHistorialAsync(
        Guid id,
        Guid? soloTecnicoId = null,
        Guid? soloClienteId = null)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
            .FirstOrDefaultAsync(o => o.Id == id && o.Activo);

        if (orden is null)
        {
            return ServiceResult<List<HistorialEstadoOrdenResponse>>.NotFound();
        }

        if (soloTecnicoId.HasValue && orden.TecnicoAsignadoId != soloTecnicoId.Value)
        {
            return ServiceResult<List<HistorialEstadoOrdenResponse>>.NotFound();
        }

        var clienteAsociadoId = orden.ClienteId != Guid.Empty ? orden.ClienteId : orden.Vehiculo.ClienteId;
        if (soloClienteId.HasValue && clienteAsociadoId != soloClienteId.Value)
        {
            return ServiceResult<List<HistorialEstadoOrdenResponse>>.NotFound();
        }

        var historial = await _context.HistorialEstadosOrden
            .Include(h => h.Usuario)
            .Where(h => h.OrdenServicioId == id && h.Activo)
            .OrderBy(h => h.FechaCambio)
            .Select(h => new HistorialEstadoOrdenResponse(
                h.Id,
                h.OrdenServicioId,
                h.EstadoAnterior != null ? h.EstadoAnterior.ToString() : null,
                h.EstadoAnterior.HasValue ? (int)h.EstadoAnterior.Value : null,
                h.EstadoNuevo.ToString(),
                (int)h.EstadoNuevo,
                h.UsuarioId,
                h.Usuario != null ? h.Usuario.NombreCompleto : null,
                h.FechaCambio,
                h.Observaciones))
            .ToListAsync();

        return ServiceResult<List<HistorialEstadoOrdenResponse>>.Success(historial);
    }

    public async Task<ServiceResult<DetalleServicioResponse>> AgregarDetalleAsync(
        Guid ordenServicioId,
        AgregarDetalleServicioRequest request,
        bool puedeModificarPrecios,
        Guid? soloTecnicoId = null)
    {
        if (request.Cantidad <= 0)
        {
            return ServiceResult<DetalleServicioResponse>.Invalid("La cantidad debe ser mayor a 0.");
        }

        var orden = await _context.OrdenesServicio
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo);

        if (orden is null)
        {
            return ServiceResult<DetalleServicioResponse>.NotFound();
        }

        if (soloTecnicoId.HasValue && orden.TecnicoAsignadoId != soloTecnicoId.Value)
        {
            return ServiceResult<DetalleServicioResponse>.Forbidden("No tiene autorización para modificar órdenes asignadas a otro técnico.");
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

        var porcentajeIgvVigente = await _configuracionService.ObtenerPorcentajeIgvVigenteAsync();

        // Determinar TipoItem
        var tipoItem = request.TipoItem ?? (request.ProductoId.HasValue
            ? TipoItemServicio.Repuesto
            : (request.ServicioId.HasValue ? TipoItemServicio.Servicio : TipoItemServicio.ManoDeObra));

        // Determinar TipoAfectacionIgv inicial
        var tipoAfectacion = request.TipoAfectacionIgv ?? TipoAfectacionIgv.Gravado;

        decimal precioFinal = 0m;
        decimal costoHistorico = 0m;

        string descripcion = request.Descripcion?.Trim() ?? string.Empty;
        Producto? producto = null;
        Servicio? servicio = null;

        if (tipoItem == TipoItemServicio.Repuesto)
        {
            if (!request.ProductoId.HasValue)
            {
                return ServiceResult<DetalleServicioResponse>.Invalid("El ID del producto es obligatorio para ítems de tipo Repuesto.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {request.ProductoId.Value} FOR UPDATE");

                producto = await _context.Productos
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

                costoHistorico = producto.Costo;

                if (puedeModificarPrecios && request.PrecioUnitario.HasValue && request.PrecioUnitario.Value >= 0)
                {
                    precioFinal = request.PrecioUnitario.Value;
                }
                else
                {
                    if (!puedeModificarPrecios && request.PrecioUnitario.HasValue && request.PrecioUnitario.Value != producto.PrecioVenta)
                    {
                        return ServiceResult<DetalleServicioResponse>.Forbidden("El rol Técnico no está autorizado a fijar o modificar precios de repuestos.");
                    }
                    precioFinal = producto.PrecioVenta;
                }

                if (string.IsNullOrWhiteSpace(descripcion))
                {
                    descripcion = producto.Nombre;
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

                var (subtotalGravado, igvCalculado, totalCalculado, porcIgv) =
                    CalcularFinanzasLinea(request.Cantidad, precioFinal, tipoAfectacion, porcentajeIgvVigente);

                var detalle = new DetalleServicio
                {
                    OrdenServicioId        = orden.Id,
                    TipoItem               = tipoItem,
                    ProductoId             = producto.Id,
                    Producto               = producto,
                    Descripcion            = descripcion,
                    Cantidad               = request.Cantidad,
                    PrecioUnitario         = precioFinal,
                    CostoUnitarioHistorico = costoHistorico,
                    TipoAfectacionIgv      = tipoAfectacion,
                    SubtotalGravado        = subtotalGravado,
                    PorcentajeIgvAplicado  = porcIgv,
                    MontoIgv               = igvCalculado,
                    Total                  = totalCalculado,
                    FechaCreacion          = DateTime.UtcNow,
                    Activo                 = true
                };

                _context.DetallesServicio.Add(detalle);
                if (!orden.Detalles.Contains(detalle))
                {
                    orden.Detalles.Add(detalle);
                }
                RecalcularTotalesOrden(orden);
                orden.FechaModificacion = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<DetalleServicioResponse>.Success(MapToDetalleServicioResponse(detalle));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else if (tipoItem == TipoItemServicio.Servicio)
        {
            if (!request.ServicioId.HasValue)
            {
                return ServiceResult<DetalleServicioResponse>.Invalid("El ID del servicio es obligatorio para ítems de tipo Servicio.");
            }

            servicio = await _context.Servicios
                .FirstOrDefaultAsync(s => s.Id == request.ServicioId.Value && s.Activo);

            if (servicio is null)
            {
                return ServiceResult<DetalleServicioResponse>.Invalid("El servicio indicado no existe o está inactivo.");
            }

            if (!request.TipoAfectacionIgv.HasValue)
            {
                tipoAfectacion = servicio.TipoAfectacionIgv;
            }

            if (puedeModificarPrecios && request.PrecioUnitario.HasValue && request.PrecioUnitario.Value >= 0)
            {
                precioFinal = request.PrecioUnitario.Value;
            }
            else
            {
                if (!puedeModificarPrecios && request.PrecioUnitario.HasValue && request.PrecioUnitario.Value != servicio.PrecioSugerido)
                {
                    return ServiceResult<DetalleServicioResponse>.Forbidden("El rol Técnico no está autorizado a modificar precios sugeridos de servicios.");
                }
                precioFinal = servicio.PrecioSugerido;
            }

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                descripcion = servicio.Nombre;
            }

            costoHistorico = 0m;

            var (subtotalGravado, igvCalculado, totalCalculado, porcIgv) =
                CalcularFinanzasLinea(request.Cantidad, precioFinal, tipoAfectacion, porcentajeIgvVigente);

            var detalle = new DetalleServicio
            {
                OrdenServicioId        = orden.Id,
                TipoItem               = tipoItem,
                ServicioId             = servicio.Id,
                Servicio               = servicio,
                Descripcion            = descripcion,
                Cantidad               = request.Cantidad,
                PrecioUnitario         = precioFinal,
                CostoUnitarioHistorico = costoHistorico,
                TipoAfectacionIgv      = tipoAfectacion,
                SubtotalGravado        = subtotalGravado,
                PorcentajeIgvAplicado  = porcIgv,
                MontoIgv               = igvCalculado,
                Total                  = totalCalculado,
                FechaCreacion          = DateTime.UtcNow,
                Activo                 = true
            };

            _context.DetallesServicio.Add(detalle);
            if (!orden.Detalles.Contains(detalle))
            {
                orden.Detalles.Add(detalle);
            }
            RecalcularTotalesOrden(orden);
            orden.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ServiceResult<DetalleServicioResponse>.Success(MapToDetalleServicioResponse(detalle));
        }
        else // ManoDeObra o Terceros
        {
            if (!puedeModificarPrecios)
            {
                return ServiceResult<DetalleServicioResponse>.Forbidden(
                    "El rol Técnico no está autorizado a fijar o modificar precios de mano de obra o terceros.");
            }

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                return ServiceResult<DetalleServicioResponse>.Invalid("La descripción de la línea es obligatoria.");
            }

            if (!request.PrecioUnitario.HasValue || request.PrecioUnitario.Value < 0)
            {
                return ServiceResult<DetalleServicioResponse>.Invalid("El precio unitario es obligatorio y no puede ser negativo.");
            }

            precioFinal = request.PrecioUnitario.Value;
            costoHistorico = 0m;

            var (subtotalGravado, igvCalculado, totalCalculado, porcIgv) =
                CalcularFinanzasLinea(request.Cantidad, precioFinal, tipoAfectacion, porcentajeIgvVigente);

            var detalle = new DetalleServicio
            {
                OrdenServicioId        = orden.Id,
                TipoItem               = tipoItem,
                Descripcion            = descripcion,
                Cantidad               = request.Cantidad,
                PrecioUnitario         = precioFinal,
                CostoUnitarioHistorico = costoHistorico,
                TipoAfectacionIgv      = tipoAfectacion,
                SubtotalGravado        = subtotalGravado,
                PorcentajeIgvAplicado  = porcIgv,
                MontoIgv               = igvCalculado,
                Total                  = totalCalculado,
                FechaCreacion          = DateTime.UtcNow,
                Activo                 = true
            };

            _context.DetallesServicio.Add(detalle);
            if (!orden.Detalles.Contains(detalle))
            {
                orden.Detalles.Add(detalle);
            }
            RecalcularTotalesOrden(orden);
            orden.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ServiceResult<DetalleServicioResponse>.Success(MapToDetalleServicioResponse(detalle));
        }
    }

    public async Task<ServiceResult<bool>> EliminarDetalleAsync(Guid ordenServicioId, Guid detalleId)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Detalles)
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

        var detalle = orden.Detalles.FirstOrDefault(d => d.Id == detalleId && d.Activo);

        if (detalle is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        if (detalle.ProductoId.HasValue && detalle.TipoItem == TipoItemServicio.Repuesto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
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

                RecalcularTotalesOrden(orden);
                orden.FechaModificacion = DateTime.UtcNow;

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

        RecalcularTotalesOrden(orden);
        orden.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<OrdenServicioResponse>> CambiarEstadoAsync(
        Guid ordenServicioId,
        CambiarEstadoOrdenServicioRequest request,
        bool esTecnico,
        Guid? usuarioId = null)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoAsignado)
            .Include(o => o.UsuarioAprobacionGerencia)
            .Include(o => o.Ventas)
                .ThenInclude(v => v.Comprobante)
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo);

        if (orden is null)
        {
            return ServiceResult<OrdenServicioResponse>.NotFound();
        }

        if (esTecnico && usuarioId.HasValue && orden.TecnicoAsignadoId != usuarioId.Value)
        {
            return ServiceResult<OrdenServicioResponse>.Forbidden(
                "No tiene autorización para cambiar el estado de órdenes asignadas a otro técnico.");
        }

        if (esTecnico && (request.NuevoEstado == EstadoOrdenServicio.Aprobada 
                       || request.NuevoEstado == EstadoOrdenServicio.Entregada 
                       || request.NuevoEstado == EstadoOrdenServicio.Cancelada))
        {
            return ServiceResult<OrdenServicioResponse>.Forbidden(
                "El rol Técnico no está autorizado a realizar la aprobación final, entrega ni cancelación de la orden.");
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

        if (request.NuevoEstado == EstadoOrdenServicio.Aprobada)
        {
            if (orden.EstadoPresupuestoCliente == EstadoPresupuestoCliente.Rechazado)
            {
                return ServiceResult<OrdenServicioResponse>.Invalid(
                    "No se puede aprobar la orden de servicio porque el presupuesto fue rechazado por el cliente.");
            }

            if (orden.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Pendiente)
            {
                return ServiceResult<OrdenServicioResponse>.Invalid(
                    "No se puede aprobar la orden de servicio porque requiere aprobación de Gerencia previa.");
            }

            if (orden.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Rechazado)
            {
                return ServiceResult<OrdenServicioResponse>.Invalid(
                    "No se puede aprobar la orden de servicio porque la aprobación de Gerencia fue rechazada.");
            }
        }

        var estadoAnterior = orden.Estado;

        // Registrar hito en el historial de estados
        _context.HistorialEstadosOrden.Add(new HistorialEstadoOrden
        {
            OrdenServicioId = orden.Id,
            EstadoAnterior  = estadoAnterior,
            EstadoNuevo     = request.NuevoEstado,
            UsuarioId       = usuarioId,
            FechaCambio     = DateTime.UtcNow,
            Observaciones   = request.Observaciones?.Trim(),
            FechaCreacion   = DateTime.UtcNow,
            Activo          = true
        });

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

        // --- Transición a Entregada: fijar fecha de cierre y fecha de salida ---
        if (request.NuevoEstado == EstadoOrdenServicio.Entregada)
        {
            orden.Estado            = EstadoOrdenServicio.Entregada;
            orden.FechaCierre       = DateTime.UtcNow;
            orden.FechaSalida       = DateTime.UtcNow;
            orden.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
        }

        // --- Demás transiciones intermedias (Aprobada, EnProceso, Lista) ---
        orden.Estado            = request.NuevoEstado;
        orden.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
    }

    public async Task<ServiceResult<OrdenServicioResponse>> AsignarTecnicoAsync(
        Guid id,
        Guid tecnicoId,
        string? observaciones = null,
        Guid? usuarioId = null)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoAsignado)
            .FirstOrDefaultAsync(o => o.Id == id && o.Activo);

        if (orden is null)
        {
            return ServiceResult<OrdenServicioResponse>.NotFound();
        }

        if (orden.Estado == EstadoOrdenServicio.Entregada || orden.Estado == EstadoOrdenServicio.Cancelada)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(
                $"No se puede asignar técnico a una orden que se encuentra en estado '{orden.Estado}'.");
        }

        var tecnico = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == tecnicoId && u.Activo);

        if (tecnico is null)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid("El técnico indicado no existe o está inactivo.");
        }

        var esTecnicoRol = await _context.UsuarioRoles
            .Where(ur => ur.UsuarioId == tecnicoId && ur.Rol.Activo)
            .AnyAsync(ur => ur.Rol.Nombre == RolesDefinidos.Tecnico);

        if (!esTecnicoRol)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid("El usuario seleccionado no cuenta con el rol de Técnico.");
        }

        orden.TecnicoAsignadoId = tecnicoId;
        orden.TecnicoAsignado = tecnico;
        orden.FechaModificacion = DateTime.UtcNow;

        var textoAuto = $"Asignación de técnico a {tecnico.NombreCompleto}";
        var observacionHistorial = string.IsNullOrWhiteSpace(observaciones)
            ? textoAuto
            : $"{textoAuto}. {observaciones.Trim()}";

        _context.HistorialEstadosOrden.Add(new HistorialEstadoOrden
        {
            OrdenServicioId = orden.Id,
            EstadoAnterior  = orden.Estado,
            EstadoNuevo     = orden.Estado,
            UsuarioId       = usuarioId,
            FechaCambio     = DateTime.UtcNow,
            Observaciones   = observacionHistorial,
            FechaCreacion   = DateTime.UtcNow,
            Activo          = true
        });

        await _context.SaveChangesAsync();

        return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
    }

    public async Task<ServiceResult<OrdenServicioResponse>> ResponderPresupuestoClienteAsync(
        Guid id,
        ResponderPresupuestoClienteRequest request,
        Guid? soloClienteId = null,
        Guid? usuarioId = null)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoAsignado)
            .Include(o => o.Detalles)
            .Include(o => o.UsuarioAprobacionGerencia)
            .Include(o => o.Ventas)
                .ThenInclude(v => v.Comprobante)
            .FirstOrDefaultAsync(o => o.Id == id && o.Activo);

        if (orden is null)
        {
            return ServiceResult<OrdenServicioResponse>.NotFound();
        }

        var clienteAsociadoId = orden.ClienteId != Guid.Empty ? orden.ClienteId : orden.Vehiculo?.ClienteId;
        if (soloClienteId.HasValue && clienteAsociadoId != soloClienteId.Value)
        {
            return ServiceResult<OrdenServicioResponse>.Forbidden(
                "No tiene autorización para responder presupuestos de órdenes que no le pertenecen.");
        }

        if (orden.Estado == EstadoOrdenServicio.Entregada || orden.Estado == EstadoOrdenServicio.Cancelada)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(
                $"No se puede modificar la respuesta de presupuesto en una orden en estado terminal '{orden.Estado}'.");
        }

        orden.EstadoPresupuestoCliente = request.Estado;
        orden.FechaRespuestaCliente = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Observaciones))
        {
            orden.ObservacionesPresupuestoCliente = request.Observaciones.Trim();
        }
        orden.FechaModificacion = DateTime.UtcNow;

        _context.HistorialEstadosOrden.Add(new HistorialEstadoOrden
        {
            OrdenServicioId = orden.Id,
            EstadoAnterior  = orden.Estado,
            EstadoNuevo     = orden.Estado,
            UsuarioId       = usuarioId,
            FechaCambio     = DateTime.UtcNow,
            Observaciones   = $"Respuesta de presupuesto del cliente: {request.Estado}. {request.Observaciones}".Trim(),
            FechaCreacion   = DateTime.UtcNow,
            Activo          = true
        });

        await _context.SaveChangesAsync();
        return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
    }

    public async Task<ServiceResult<OrdenServicioResponse>> AprobacionGerenciaAsync(
        Guid id,
        AprobacionGerenciaRequest request,
        Guid? usuarioId = null)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
                .ThenInclude(v => v.Cliente)
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoAsignado)
            .Include(o => o.Detalles)
            .Include(o => o.UsuarioAprobacionGerencia)
            .Include(o => o.Ventas)
                .ThenInclude(v => v.Comprobante)
            .FirstOrDefaultAsync(o => o.Id == id && o.Activo);

        if (orden is null)
        {
            return ServiceResult<OrdenServicioResponse>.NotFound();
        }

        if (orden.Estado == EstadoOrdenServicio.Entregada || orden.Estado == EstadoOrdenServicio.Cancelada)
        {
            return ServiceResult<OrdenServicioResponse>.Invalid(
                $"No se puede modificar la aprobación de gerencia en una orden en estado terminal '{orden.Estado}'.");
        }

        Usuario? usuario = null;
        if (usuarioId.HasValue)
        {
            usuario = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == usuarioId.Value && u.Activo);
        }

        orden.EstadoAprobacionGerencia = request.Estado;
        orden.FechaAprobacionGerencia = DateTime.UtcNow;
        orden.UsuarioAprobacionGerenciaId = usuarioId;
        orden.UsuarioAprobacionGerencia = usuario;
        if (!string.IsNullOrWhiteSpace(request.Observaciones))
        {
            orden.ObservacionesAprobacionGerencia = request.Observaciones.Trim();
        }
        orden.FechaModificacion = DateTime.UtcNow;

        _context.HistorialEstadosOrden.Add(new HistorialEstadoOrden
        {
            OrdenServicioId = orden.Id,
            EstadoAnterior  = orden.Estado,
            EstadoNuevo     = orden.Estado,
            UsuarioId       = usuarioId,
            FechaCambio     = DateTime.UtcNow,
            Observaciones   = $"Aprobación de Gerencia: {request.Estado}. {request.Observaciones}".Trim(),
            FechaCreacion   = DateTime.UtcNow,
            Activo          = true
        });

        await _context.SaveChangesAsync();
        return ServiceResult<OrdenServicioResponse>.Success(MapToResponse(orden));
    }

    // --- Lectura del medidor y fechas de la recepción ---

    private static readonly System.Globalization.CultureInfo CulturaPeru = System.Globalization.CultureInfo.GetCultureInfo("es-PE");

    /// <summary>Lectura con que ingresa la unidad, en km u horas según su medidor.</summary>
    private static decimal? LecturaSegunMedidor(TipoMedidor medidor, int? kilometraje, decimal? horasUso, decimal? lecturaGenerica)
    {
        var propia = medidor == TipoMedidor.Kilometraje ? (decimal?)kilometraje : horasUso;
        return propia ?? lecturaGenerica;
    }

    private static decimal? LecturaActualDeUnidad(Vehiculo vehiculo) =>
        vehiculo.LecturaMedidorActual
        ?? (vehiculo.TipoMedidor == TipoMedidor.Kilometraje ? (decimal?)vehiculo.Kilometraje : vehiculo.HorasUso);

    private static string TextoLectura(decimal lectura, TipoMedidor medidor) =>
        medidor == TipoMedidor.Kilometraje
            ? $"{lectura.ToString("N0", CulturaPeru)} km"
            : $"{lectura.ToString("0.#", CulturaPeru)} h";

    /// <summary>
    /// Mayor lectura registrada en las órdenes de la unidad. Con <paramref name="antesDe"/>
    /// solo cuentan las abiertas antes de esa fecha.
    /// </summary>
    private async Task<decimal?> UltimaLecturaDeOrdenesAsync(
        Guid vehiculoId,
        TipoMedidor medidor,
        Guid? excluirOrdenId = null,
        DateTime? antesDe = null)
    {
        var consulta = _context.OrdenesServicio
            .AsNoTracking()
            .Where(o => o.VehiculoId == vehiculoId && o.Activo);

        if (excluirOrdenId.HasValue) consulta = consulta.Where(o => o.Id != excluirOrdenId.Value);
        if (antesDe.HasValue) consulta = consulta.Where(o => o.FechaApertura < antesDe.Value);

        var lecturas = await consulta
            .Select(o => new { o.KilometrajeIngreso, o.HorasUsoIngreso, o.LecturaMedidorIngreso })
            .ToListAsync();

        return lecturas
            .Select(l => LecturaSegunMedidor(medidor, l.KilometrajeIngreso, l.HorasUsoIngreso, l.LecturaMedidorIngreso))
            .Where(l => l.HasValue)
            .Max();
    }

    private static string? MotivoLecturaMenor(decimal lectura, decimal? minima, TipoMedidor medidor, string referencia) =>
        minima.HasValue && lectura < minima.Value
            ? $"La lectura del medidor ({TextoLectura(lectura, medidor)}) no puede ser menor que {referencia} ({TextoLectura(minima.Value, medidor)})."
            : null;

    /// <summary>La lectura de la unidad solo avanza: un medidor no retrocede.</summary>
    private static void AvanzarLecturaDeUnidad(Vehiculo vehiculo, decimal lectura)
    {
        var actual = LecturaActualDeUnidad(vehiculo);
        if (actual.HasValue && lectura <= actual.Value) return;

        vehiculo.LecturaMedidorActual = lectura;
        if (vehiculo.TipoMedidor == TipoMedidor.Kilometraje)
            vehiculo.Kilometraje = (int)Math.Round(lectura);
        else
            vehiculo.HorasUso = lectura;
        vehiculo.FechaModificacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Ingreso de la unidad al taller. Las órdenes anteriores al formato de
    /// atención pueden no tenerlo: para esas vale la apertura.
    /// </summary>
    private static DateTime IngresoDeOrden(OrdenServicio orden) =>
        orden.FechaIngreso.Year > 1900 ? orden.FechaIngreso : orden.FechaApertura;

    /// <summary>
    /// La entrega estimada no puede quedar antes del ingreso. Se compara por
    /// minuto porque la web y la app eligen la hora sin segundos.
    /// </summary>
    private static string? MotivoEntregaInvalida(DateTime? entrega, DateTime ingreso)
    {
        if (!entrega.HasValue) return null;
        var ingresoAlMinuto = new DateTime(ingreso.Ticks - (ingreso.Ticks % TimeSpan.TicksPerMinute), ingreso.Kind);
        return entrega.Value.ToUniversalTime() < ingresoAlMinuto.ToUniversalTime()
            ? "La fecha estimada de entrega no puede ser anterior al ingreso de la unidad."
            : null;
    }

    private static void RecalcularTotalesOrden(OrdenServicio orden)
    {
        var detallesActivos = orden.Detalles
            .Where(d => d.Activo)
            .DistinctBy(d => d.Id)
            .ToList();
        orden.SubtotalGravado = decimal.Round(
            detallesActivos.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Gravado).Sum(d => d.SubtotalGravado),
            2, MidpointRounding.AwayFromZero);
        orden.SubtotalExonerado = decimal.Round(
            detallesActivos.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Exonerado).Sum(d => d.Total),
            2, MidpointRounding.AwayFromZero);
        orden.SubtotalInafecto = decimal.Round(
            detallesActivos.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Inafecto).Sum(d => d.Total),
            2, MidpointRounding.AwayFromZero);
        orden.MontoIgv = decimal.Round(
            detallesActivos.Sum(d => d.MontoIgv),
            2, MidpointRounding.AwayFromZero);
        orden.Total = decimal.Round(
            detallesActivos.Sum(d => d.Total),
            2, MidpointRounding.AwayFromZero);
    }

    private static (decimal SubtotalGravado, decimal MontoIgv, decimal Total, decimal PorcentajeIgvAplicado) CalcularFinanzasLinea(
        int cantidad, decimal precioUnitario, TipoAfectacionIgv tipoAfectacion, decimal porcentajeIgvVigente)
    {
        decimal subtotalBruto = decimal.Round(cantidad * precioUnitario, 2, MidpointRounding.AwayFromZero);

        if (tipoAfectacion == TipoAfectacionIgv.Gravado)
        {
            decimal montoIgv = decimal.Round(subtotalBruto * (porcentajeIgvVigente / 100m), 2, MidpointRounding.AwayFromZero);
            decimal total = decimal.Round(subtotalBruto + montoIgv, 2, MidpointRounding.AwayFromZero);
            return (subtotalBruto, montoIgv, total, porcentajeIgvVigente);
        }
        else
        {
            return (0m, 0m, subtotalBruto, 0m);
        }
    }

    private static DetalleServicioResponse MapToDetalleServicioResponse(DetalleServicio d)
    {
        return new DetalleServicioResponse(
            d.Id,
            d.ProductoId,
            d.Producto?.Codigo,
            d.Descripcion,
            d.Cantidad,
            d.PrecioUnitario,
            d.Cantidad * d.PrecioUnitario,
            d.TipoItem == TipoItemServicio.Repuesto || d.ProductoId.HasValue,
            d.ServicioId,
            d.Servicio?.Nombre,
            d.TipoItem,
            d.TipoItem.ToString(),
            d.CostoUnitarioHistorico,
            d.TipoAfectacionIgv,
            d.TipoAfectacionIgv.ToString(),
            d.SubtotalGravado,
            d.PorcentajeIgvAplicado,
            d.MontoIgv,
            d.Total);
    }

    private static string DeterminarTipoMedidor(Vehiculo? vehiculo)
    {
        if (vehiculo is null) return "Km";
        if (vehiculo.TipoMedidor == TipoMedidor.Horas || vehiculo.TipoUnidad == TipoUnidad.MotoAcuatica || vehiculo.TipoUnidad == TipoUnidad.Generador)
            return "Horas";
        return "Km";
    }

    private static OrdenServicioResponse MapToResponse(OrdenServicio orden)
    {
        var primeraVenta = orden.Ventas?.FirstOrDefault(v => v.Activo);
        var comprobanteTexto = primeraVenta?.Comprobante != null
            ? $"{primeraVenta.Comprobante.Serie}-{primeraVenta.Comprobante.Numero}"
            : null;

        var clienteNombre = orden.Cliente != null
            ? orden.Cliente.NombreCompleto
            : (orden.Vehiculo?.Cliente?.NombreCompleto ?? string.Empty);

        var clienteId = orden.ClienteId != Guid.Empty
            ? orden.ClienteId
            : (orden.Vehiculo?.ClienteId ?? Guid.Empty);

        return new OrdenServicioResponse(
            orden.Id,
            orden.VehiculoId,
            orden.Vehiculo?.Placa,
            orden.Vehiculo?.Marca ?? string.Empty,
            orden.Vehiculo?.Modelo ?? string.Empty,
            clienteId,
            clienteNombre,
            orden.TecnicoAsignadoId,
            orden.TecnicoAsignado?.NombreCompleto,
            orden.Estado.ToString(),
            (int)orden.Estado,
            orden.FechaApertura,
            orden.FechaCierre,
            orden.Diagnostico,
            orden.Observaciones,
            orden.Activo,
            orden.NumeroOrden,
            orden.FechaIngreso,
            orden.FechaEstimadaEntrega,
            orden.FechaSalida,
            orden.MotivoFalla,
            orden.Solucion,
            orden.TipoAtencion.ToString(),
            (int)orden.TipoAtencion,
            orden.ModalidadAtencion.ToString(),
            (int)orden.ModalidadAtencion,
            orden.TipoFalla?.ToString(),
            orden.TipoFalla.HasValue ? (int)orden.TipoFalla.Value : null,
            orden.KilometrajeIngreso,
            orden.HorasUsoIngreso,
            orden.LecturaMedidorIngreso,
            orden.SubtotalGravado,
            orden.SubtotalExonerado,
            orden.SubtotalInafecto,
            orden.MontoIgv,
            orden.Total,
            (int)orden.EstadoPresupuestoCliente,
            orden.EstadoPresupuestoCliente.ToString(),
            orden.FechaRespuestaCliente,
            orden.ObservacionesPresupuestoCliente,
            (int)orden.EstadoAprobacionGerencia,
            orden.EstadoAprobacionGerencia.ToString(),
            orden.FechaAprobacionGerencia,
            orden.UsuarioAprobacionGerenciaId,
            orden.UsuarioAprobacionGerencia?.NombreCompleto,
            orden.ObservacionesAprobacionGerencia,
            primeraVenta?.Id,
            comprobanteTexto,
            DeterminarTipoMedidor(orden.Vehiculo));
    }

    private static OrdenServicioDetalleResponse MapToDetalleResponse(OrdenServicio orden)
    {
        var detalles = orden.Detalles
            .Where(d => d.Activo)
            .OrderBy(d => d.FechaCreacion)
            .Select(MapToDetalleServicioResponse)
            .ToList();

        var historial = orden.HistorialEstados?
            .Where(h => h.Activo)
            .OrderBy(h => h.FechaCambio)
            .Select(h => new HistorialEstadoOrdenResponse(
                h.Id,
                h.OrdenServicioId,
                h.EstadoAnterior != null ? h.EstadoAnterior.ToString() : null,
                h.EstadoAnterior.HasValue ? (int)h.EstadoAnterior.Value : null,
                h.EstadoNuevo.ToString(),
                (int)h.EstadoNuevo,
                h.UsuarioId,
                h.Usuario != null ? h.Usuario.NombreCompleto : null,
                h.FechaCambio,
                h.Observaciones))
            .ToList() ?? new List<HistorialEstadoOrdenResponse>();

        var total = orden.Total > 0 ? orden.Total : detalles.Sum(d => d.Total);

        var primeraVenta = orden.Ventas?.FirstOrDefault(v => v.Activo);
        var comprobanteTexto = primeraVenta?.Comprobante != null
            ? $"{primeraVenta.Comprobante.Serie}-{primeraVenta.Comprobante.Numero}"
            : null;

        var clienteNombre = orden.Cliente != null
            ? orden.Cliente.NombreCompleto
            : (orden.Vehiculo?.Cliente?.NombreCompleto ?? string.Empty);

        var clienteId = orden.ClienteId != Guid.Empty
            ? orden.ClienteId
            : (orden.Vehiculo?.ClienteId ?? Guid.Empty);

        var clienteTelefono = orden.Cliente?.Telefono ?? orden.Vehiculo?.Cliente?.Telefono;
        var clienteDocumento = orden.Cliente?.DocumentoIdentidad ?? orden.Vehiculo?.Cliente?.DocumentoIdentidad;

        return new OrdenServicioDetalleResponse(
            orden.Id,
            orden.VehiculoId,
            orden.Vehiculo?.Placa,
            orden.Vehiculo?.Marca ?? string.Empty,
            orden.Vehiculo?.Modelo ?? string.Empty,
            orden.Vehiculo?.Anio,
            orden.Vehiculo?.Kilometraje,
            orden.Vehiculo?.Color,
            clienteId,
            clienteNombre,
            clienteTelefono,
            clienteDocumento,
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
            total,
            orden.NumeroOrden,
            orden.FechaIngreso,
            orden.FechaEstimadaEntrega,
            orden.FechaSalida,
            orden.MotivoFalla,
            orden.Solucion,
            orden.TipoAtencion.ToString(),
            (int)orden.TipoAtencion,
            orden.ModalidadAtencion.ToString(),
            (int)orden.ModalidadAtencion,
            orden.TipoFalla?.ToString(),
            orden.TipoFalla.HasValue ? (int)orden.TipoFalla.Value : null,
            orden.KilometrajeIngreso,
            orden.HorasUsoIngreso,
            orden.LecturaMedidorIngreso,
            orden.SubtotalGravado,
            orden.SubtotalExonerado,
            orden.SubtotalInafecto,
            orden.MontoIgv,
            (int)orden.EstadoPresupuestoCliente,
            orden.EstadoPresupuestoCliente.ToString(),
            orden.FechaRespuestaCliente,
            orden.ObservacionesPresupuestoCliente,
            (int)orden.EstadoAprobacionGerencia,
            orden.EstadoAprobacionGerencia.ToString(),
            orden.FechaAprobacionGerencia,
            orden.UsuarioAprobacionGerenciaId,
            orden.UsuarioAprobacionGerencia?.NombreCompleto,
            orden.ObservacionesAprobacionGerencia,
            orden.Vehiculo?.TipoUnidad.ToString(),
            orden.Vehiculo?.NumeroSerieVIN,
            orden.Vehiculo?.NumeroMotor,
            historial,
            primeraVenta?.Id,
            comprobanteTexto,
            DeterminarTipoMedidor(orden.Vehiculo));
    }
}
