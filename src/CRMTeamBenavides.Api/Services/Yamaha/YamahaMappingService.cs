using System.Globalization;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Implementación pura y en memoria del servicio de proyección desde el modelo de datos
/// del CRM Team Benavides hacia los contratos de datos de Yamaha Motor del Perú v3.
///
/// Principios de diseño e integridad:
/// 1. Cero I/O: No inyecta ApplicationDbContext ni realiza llamadas HTTP o lecturas en disco.
/// 2. Inmutabilidad: No muta ni altera ninguna entidad recibida como argumento.
/// 3. Sin suposiciones: Si una relación requerida (Cliente, Vehículo, Comprobante, Producto) no está
///    cargada ni se pasa explícitamente, retorna un resultado NoProyectable con el detalle exacto.
/// 4. Cumplimiento de la Sección 6.2: Una orden de servicio sin comprobante emitido no se considera
///    facturada y es rechazada para proyección; nunca se inventan fechas ni correlativos.
/// 5. Trazabilidad de decisiones pendientes: Cada regla condicionada por D-01 a D-09 se adjunta
///    como observación explícita en el resultado de mapeo.
/// </summary>
public sealed class YamahaMappingService : IYamahaMappingService
{
    // -------------------------------------------------------------------------
    // 1. Servicio 1 — Ventas (Sección 4 de Yamaha v3)
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public YamahaMapeoRegistroResultado<YamahaVentaV3Dto> MapearDetalleVenta(
        Venta venta,
        DetalleVenta detalle,
        YamahaPuntoVentaContexto contexto,
        Comprobante? comprobante = null,
        Cliente? cliente = null,
        Vehiculo? vehiculo = null)
    {
        ArgumentNullException.ThrowIfNull(venta);
        ArgumentNullException.ThrowIfNull(detalle);
        ArgumentNullException.ThrowIfNull(contexto);

        var idOrigen = $"Venta-{venta.Id}/Detalle-{detalle.Id}";
        var errores = new List<string>();
        var observaciones = new List<string>();

        // Validación de estado de la venta
        if (!venta.Activo || venta.Estado != EstadoVenta.Confirmada)
        {
            errores.Add($"La venta se encuentra en estado '{venta.Estado}' (Activo={venta.Activo}). Solo se pueden proyectar ventas activas en estado Confirmada.");
        }

        // Validación de comprobante de facturación
        comprobante ??= venta.Comprobante;
        if (comprobante is null)
        {
            errores.Add("La venta no cuenta con comprobante de facturación emitido. Yamaha v3 Sección 4 exige NumeroComprobante y FechaFacturacion obligatorios.");
        }
        else if (string.Equals(comprobante.Estado, "Anulado", StringComparison.OrdinalIgnoreCase))
        {
            errores.Add("El comprobante asociado a la venta se encuentra en estado 'Anulado'.");
        }
        else if (string.IsNullOrWhiteSpace(comprobante.Serie) || string.IsNullOrWhiteSpace(comprobante.Numero))
        {
            errores.Add("El comprobante no tiene serie o número correlativo asignado.");
        }

        // Tipo de comprobante de venta (Sección 4.3)
        var tipoDocVenta = string.Empty;
        if (comprobante is not null && !string.IsNullOrWhiteSpace(comprobante.Tipo))
        {
            var resTipoDoc = YamahaCatalogMapper.TraducirTipoComprobante(comprobante.Tipo);
            if (!resTipoDoc.EsValido)
            {
                errores.Add(resTipoDoc.Error!);
            }
            else
            {
                tipoDocVenta = resTipoDoc.Valor!;
                if (string.Equals(tipoDocVenta, YamahaV3Catalogos.DocumentoVentaNotaCredito, StringComparison.Ordinal))
                {
                    errores.Add("La proyección de comprobantes tipo 'NOTA DE CREDITO' está bloqueada (D-04): el flujo contable y la regla de signos negativos de la Sección 4.2 están pendientes de definición.");
                }
            }
        }

        // Validación y datos del cliente
        cliente ??= venta.Cliente;
        if (cliente is null)
        {
            errores.Add("La venta no tiene la entidad Cliente cargada ni suministrada.");
        }

        var numDocCliente = cliente?.NumeroDocumento?.Trim();
        if (string.IsNullOrWhiteSpace(numDocCliente))
        {
            numDocCliente = cliente?.DocumentoIdentidad?.Trim() ?? string.Empty;
        }

        if (cliente is not null && string.IsNullOrWhiteSpace(numDocCliente))
        {
            errores.Add("El cliente asociado no cuenta con número de documento de identidad registrado.");
        }

        var tipoDocCliente = string.Empty;
        if (cliente is not null && !string.IsNullOrWhiteSpace(numDocCliente))
        {
            var resTipoDocCli = YamahaCatalogMapper.TraducirTipoDocumentoCliente(cliente.TipoDocumento, numDocCliente);
            if (!resTipoDocCli.EsValido)
            {
                errores.Add(resTipoDocCli.Error!);
            }
            else
            {
                tipoDocCliente = resTipoDocCli.Valor!;
            }
        }

        // Producto y SKU de la línea vendida
        var producto = detalle.Producto;
        var codigoYamaha = producto?.Codigo?.Trim() ?? string.Empty;
        var descripcionProducto = producto?.Nombre?.Trim() ?? string.Empty;

        if (producto is null)
        {
            errores.Add("La línea de detalle de venta no cuenta con el Producto asociado cargado para obtener el código SKU.");
        }
        else if (string.IsNullOrWhiteSpace(codigoYamaha))
        {
            errores.Add("El producto asociado a la línea de venta no cuenta con código SKU (CodigoYamaha).");
        }

        if (detalle.Cantidad <= 0)
        {
            errores.Add($"La cantidad vendida debe ser mayor a 0 (recibido: {detalle.Cantidad}).");
        }

        // Si hay errores bloqueantes estructurales, no proyectar el DTO
        if (errores.Count > 0)
        {
            return YamahaMapeoRegistroResultado<YamahaVentaV3Dto>.NoProyectable(idOrigen, errores, observaciones);
        }

        // Cálculos monetarios e importes con redondeo seguro
        var montoConIgv = detalle.Total > 0
            ? Math.Round(detalle.Total, 2, MidpointRounding.AwayFromZero)
            : Math.Round(detalle.Cantidad * detalle.PrecioUnitario, 2, MidpointRounding.AwayFromZero);

        decimal montoSinIgv;
        if (detalle.TipoAfectacionIgv == TipoAfectacionIgv.Gravado)
        {
            montoSinIgv = detalle.SubtotalGravado > 0
                ? Math.Round(detalle.SubtotalGravado, 2, MidpointRounding.AwayFromZero)
                : Math.Round(montoConIgv / 1.18m, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            // Exonerado o Inafecto: no hay IGV; MontoSinIGV es igual al monto total
            montoSinIgv = montoConIgv;
        }

        var precioFinal = montoConIgv;
        var precioUnitarioConIgv = detalle.Cantidad > 0
            ? Math.Round(montoConIgv / detalle.Cantidad, 2, MidpointRounding.AwayFromZero)
            : 0.00m;

        // Canal de venta (MOSTRADOR vs TALLER)
        var canal = YamahaCatalogMapper.TraducirCanalVenta(venta.OrdenServicioId);

        // Registro de observaciones sobre decisiones de negocio pendientes
        observaciones.Add("D-06: DescuentoPorcentaje y DescuentoSoles asignados en 0.00 debido a que DetalleVenta no desglosa descuentos por ítem.");
        observaciones.Add("D-05: TipoPago asignado como CONTADO y EntidadFinanciera como CONTADO por defecto.");

        if (venta.OrdenServicioId.HasValue)
        {
            observaciones.Add($"D-02: Venta vinculada a Orden de Servicio {venta.OrdenServicioId.Value} (Canal TALLER); sujeta a confirmación sobre duplicidad con Servicio Técnico.");
        }

        // Datos del cliente comercial
        var nombreCliente = !string.IsNullOrWhiteSpace(cliente!.NombreCompleto)
            ? cliente.NombreCompleto.Trim()
            : (cliente.RazonSocial?.Trim() ?? string.Empty);

        var tipoCliente = YamahaCatalogMapper.TraducirTipoCliente(cliente.TipoDocumento, numDocCliente);

        // Número de comprobante concatenado (ej. B001-00012345)
        var numeroComprobante = $"{comprobante!.Serie!.Trim()}-{comprobante.Numero!.Trim()}";
        var fechaFacturacion = YamahaCatalogMapper.FormatearFecha(comprobante.FechaCreacion);

        // Ubicación del cliente con sede de respaldo para campos obligatorios
        var direccionCliente = cliente.Direccion?.Trim() ?? string.Empty;
        var distritoCliente = contexto.DistritoSede;
        var ciudadCliente = contexto.CiudadSede;
        var departamentoCliente = contexto.DepartamentoSede;

        // Celular y correo
        var celularCliente = cliente.Telefono?.Trim() ?? string.Empty;
        var correoCliente = cliente.Email?.Trim() ?? string.Empty;

        // Vehículo (si aplica)
        vehiculo ??= venta.OrdenServicio?.Vehiculo;
        var motoActualCliente = vehiculo?.Modelo?.Trim() ?? string.Empty;

        var dto = new YamahaVentaV3Dto
        {
            RazonSocial = contexto.RazonSocial,
            RUC = contexto.Ruc,
            CodigoPuntoVenta = contexto.CodigoPuntoVenta,
            NombreComercialPuntoVenta = contexto.NombreComercialPuntoVenta ?? string.Empty,
            VIN = string.Empty, // Para repuestos (SP) es cadena vacía conforme a Sección 4
            Modelo = string.Empty,
            Color = string.Empty,
            AnioModelo = null, // Convertidor serializa como "" según Sección 3.4
            FechaFacturacion = fechaFacturacion,
            NumeroComprobante = numeroComprobante,
            TipoDocumentoVenta = tipoDocVenta,
            TipoPago = YamahaV3Catalogos.TipoPagoContado,
            EntidadFinanciera = YamahaV3Catalogos.TipoPagoContado,
            MontoSinIGV = montoSinIgv,
            MontoConIGV = montoConIgv,
            Canal = canal,
            LineaNegocio = YamahaV3Catalogos.LineaSpareParts, // SP
            TipoPuntoVenta = contexto.TipoPuntoVenta,
            NumeroMotor = string.Empty,
            Moneda = "PEN",
            PrecioFinal = precioFinal,
            CodigoYamaha = codigoYamaha,
            Descripcion = descripcionProducto,
            CantidadVendida = detalle.Cantidad,
            DescuentoPorcentaje = 0.00m,
            DescuentoSoles = 0.00m,
            PrecioUnitarioConIGV = precioUnitarioConIgv,
            TipoCliente = tipoCliente,
            NombreCompletoCliente = nombreCliente,
            TipoDocumentoCliente = tipoDocCliente,
            NumeroDocumentoCliente = numDocCliente,
            CanalContactoCliente = "PRESENCIAL",
            DistritoCliente = distritoCliente,
            CiudadCliente = ciudadCliente,
            DepartamentoCliente = departamentoCliente,
            DireccionCliente = direccionCliente,
            MotoActualCliente = motoActualCliente,
            CelularCliente = celularCliente,
            CorreoCliente = correoCliente
        };

        return YamahaMapeoRegistroResultado<YamahaVentaV3Dto>.Exitoso(dto, idOrigen, observaciones);
    }

    /// <inheritdoc />
    public YamahaMapeoLoteResultado<YamahaVentaV3Dto> MapearVenta(
        Venta venta,
        YamahaPuntoVentaContexto contexto,
        Comprobante? comprobante = null,
        Cliente? cliente = null,
        Vehiculo? vehiculo = null)
    {
        ArgumentNullException.ThrowIfNull(venta);
        ArgumentNullException.ThrowIfNull(contexto);

        // Validación de estado de la venta
        if (!venta.Activo || venta.Estado != EstadoVenta.Confirmada)
        {
            return new YamahaMapeoLoteResultado<YamahaVentaV3Dto>(
                Array.Empty<YamahaVentaV3Dto>(),
                new[]
                {
                    YamahaMapeoRegistroResultado<YamahaVentaV3Dto>.NoProyectable(
                        $"Venta-{venta.Id}",
                        $"La venta se encuentra en estado '{venta.Estado}' (Activo={venta.Activo}). Solo se pueden proyectar ventas activas en estado Confirmada.")
                },
                TotalProcesados: 0,
                TotalValidos: 0,
                TotalNoProyectables: 1);
        }

        var detallesActivos = venta.Detalles.Where(d => d.Activo).ToList();
        if (detallesActivos.Count == 0)
        {
            return new YamahaMapeoLoteResultado<YamahaVentaV3Dto>(
                Array.Empty<YamahaVentaV3Dto>(),
                new[]
                {
                    YamahaMapeoRegistroResultado<YamahaVentaV3Dto>.NoProyectable(
                        $"Venta-{venta.Id}",
                        "La venta no contiene líneas de detalle activas para proyectar.")
                },
                TotalProcesados: 0,
                TotalValidos: 0,
                TotalNoProyectables: 1);
        }

        var dtos = new List<YamahaVentaV3Dto>();
        var resultados = new List<YamahaMapeoRegistroResultado<YamahaVentaV3Dto>>();

        foreach (var d in detallesActivos)
        {
            var res = MapearDetalleVenta(venta, d, contexto, comprobante, cliente, vehiculo);
            resultados.Add(res);
            if (res.EsValido && res.Dto is not null)
            {
                dtos.Add(res.Dto);
            }
        }

        return new YamahaMapeoLoteResultado<YamahaVentaV3Dto>(
            dtos,
            resultados,
            TotalProcesados: detallesActivos.Count,
            TotalValidos: dtos.Count,
            TotalNoProyectables: detallesActivos.Count - dtos.Count);
    }

    /// <inheritdoc />
    public YamahaMapeoLoteResultado<YamahaVentaV3Dto> MapearLoteVentas(
        IEnumerable<Venta> ventas,
        YamahaPuntoVentaContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(ventas);
        ArgumentNullException.ThrowIfNull(contexto);

        var dtos = new List<YamahaVentaV3Dto>();
        var resultados = new List<YamahaMapeoRegistroResultado<YamahaVentaV3Dto>>();
        var totalProcesados = 0;

        foreach (var venta in ventas)
        {
            var resVenta = MapearVenta(venta, contexto);
            totalProcesados += resVenta.TotalProcesados;
            dtos.AddRange(resVenta.DtosProyectados);
            resultados.AddRange(resVenta.Registros);
        }

        return new YamahaMapeoLoteResultado<YamahaVentaV3Dto>(
            dtos,
            resultados,
            TotalProcesados: totalProcesados,
            TotalValidos: dtos.Count,
            TotalNoProyectables: totalProcesados - dtos.Count);
    }

    // -------------------------------------------------------------------------
    // 2. Servicio 2 — Stock (Sección 5 de Yamaha v3)
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public YamahaMapeoRegistroResultado<YamahaStockV3Dto> MapearProductoStock(
        Producto producto,
        YamahaPuntoVentaContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(producto);
        ArgumentNullException.ThrowIfNull(contexto);

        var idOrigen = $"Producto-{producto.Id}/{producto.Codigo}";
        var errores = new List<string>();
        var observaciones = new List<string>();

        var codigo = producto.Codigo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(codigo))
        {
            errores.Add("El producto no cuenta con código SKU (CodigoYamaha).");
        }

        var nombre = producto.Nombre?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombre))
        {
            errores.Add("El producto no cuenta con nombre o descripción.");
        }

        if (producto.StockActual < 0)
        {
            errores.Add($"El stock actual no puede ser negativo (recibido: {producto.StockActual}).");
        }

        if (errores.Count > 0)
        {
            return YamahaMapeoRegistroResultado<YamahaStockV3Dto>.NoProyectable(idOrigen, errores, observaciones);
        }

        // Sección 5.2: En repuestos (SP), StockDisponible = 0 es plenamente válido.
        // D-03: Mapeamos CostoVentaSinIGV desde producto.Costo (costo de adquisición)
        // y calculamos CostoVentaConIGV agregando el 18% de IGV.
        var costoSinIgv = Math.Round(producto.Costo, 2, MidpointRounding.AwayFromZero);
        var costoConIgv = Math.Round(costoSinIgv * 1.18m, 2, MidpointRounding.AwayFromZero);

        observaciones.Add("D-03: CostoVenta proyectado desde Producto.Costo; sujeto a confirmación con Yamaha si corresponde a Costo o PrecioVenta.");

        if (!string.Equals(producto.Marca?.Trim(), "YAMAHA", StringComparison.OrdinalIgnoreCase))
        {
            observaciones.Add("D-07: Producto con marca no-Yamaha o sin especificar; sujeto a confirmación del criterio de inclusión en catálogo Yamaha.");
        }

        var fechaIngreso = YamahaCatalogMapper.FormatearFecha(producto.FechaCreacion);

        var dto = new YamahaStockV3Dto
        {
            RazonSocial = contexto.RazonSocial,
            RUC = contexto.Ruc,
            CodigoPuntoVenta = contexto.CodigoPuntoVenta,
            NombreComercialPuntoVenta = contexto.NombreComercialPuntoVenta ?? string.Empty,
            LineaNegocio = YamahaV3Catalogos.LineaSpareParts, // SP
            CodigoYamaha = codigo,
            Descripcion = nombre,
            StockDisponible = producto.StockActual,
            CostoVentaSinIGV = costoSinIgv,
            CostoVentaConIGV = costoConIgv,
            FechaIngreso = fechaIngreso,
            VIN = string.Empty // En SP es cadena vacía conforme a Sección 5.3
        };

        return YamahaMapeoRegistroResultado<YamahaStockV3Dto>.Exitoso(dto, idOrigen, observaciones);
    }

    /// <inheritdoc />
    public YamahaMapeoLoteResultado<YamahaStockV3Dto> MapearLoteStock(
        IEnumerable<Producto> productos,
        YamahaPuntoVentaContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(productos);
        ArgumentNullException.ThrowIfNull(contexto);

        var lista = productos.ToList();
        var dtos = new List<YamahaStockV3Dto>();
        var resultados = new List<YamahaMapeoRegistroResultado<YamahaStockV3Dto>>();

        foreach (var p in lista)
        {
            var res = MapearProductoStock(p, contexto);
            resultados.Add(res);
            if (res.EsValido && res.Dto is not null)
            {
                dtos.Add(res.Dto);
            }
        }

        return new YamahaMapeoLoteResultado<YamahaStockV3Dto>(
            dtos,
            resultados,
            TotalProcesados: lista.Count,
            TotalValidos: dtos.Count,
            TotalNoProyectables: lista.Count - dtos.Count);
    }

    // -------------------------------------------------------------------------
    // 3. Servicio 3 — Servicio Técnico (Sección 6 de Yamaha v3)
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto> MapearDetalleServicio(
        OrdenServicio orden,
        DetalleServicio detalle,
        YamahaPuntoVentaContexto contexto,
        Comprobante comprobante,
        Cliente? cliente = null,
        Vehiculo? vehiculo = null)
    {
        ArgumentNullException.ThrowIfNull(orden);
        ArgumentNullException.ThrowIfNull(detalle);
        ArgumentNullException.ThrowIfNull(contexto);

        var idOrigen = $"Orden-{orden.Id}/Detalle-{detalle.Id}";
        var errores = new List<string>();
        var observaciones = new List<string>();

        // Requisito estricto Sección 6.2: Solo se reportan órdenes facturadas con comprobante emitido
        if (comprobante is null)
        {
            errores.Add("La orden de servicio no cuenta con comprobante de facturación emitido. Según la Sección 6.2 de Yamaha v3, solo se reciben órdenes ya facturadas; no se aceptan órdenes abiertas, pendientes ni cerradas sin facturar.");
            return YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto>.NoProyectable(idOrigen, errores, observaciones);
        }

        if (string.Equals(comprobante.Estado, "Anulado", StringComparison.OrdinalIgnoreCase))
        {
            errores.Add("El comprobante de facturación asociado a la orden está en estado 'Anulado'.");
            return YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto>.NoProyectable(idOrigen, errores, observaciones);
        }

        // Proyección del correlativo de la orden (D-08: OS- a OT-)
        var proyNumeroOrden = YamahaCatalogMapper.ProyectarNumeroOrden(orden.NumeroOrden);
        if (!proyNumeroOrden.EsValido)
        {
            errores.Add($"No se pudo proyectar el número de orden '{orden.NumeroOrden}': {proyNumeroOrden.Error}");
        }

        // Cliente
        cliente ??= orden.Cliente;
        if (cliente is null)
        {
            errores.Add("La orden de servicio no tiene la entidad Cliente cargada ni suministrada.");
        }

        var numDocCliente = cliente?.NumeroDocumento?.Trim();
        if (string.IsNullOrWhiteSpace(numDocCliente))
        {
            numDocCliente = cliente?.DocumentoIdentidad?.Trim() ?? string.Empty;
        }

        if (cliente is not null && string.IsNullOrWhiteSpace(numDocCliente))
        {
            errores.Add("El cliente de la orden no cuenta con número de documento de identidad registrado.");
        }

        var tipoDocCliente = string.Empty;
        if (cliente is not null && !string.IsNullOrWhiteSpace(numDocCliente))
        {
            var resTipoDocCli = YamahaCatalogMapper.TraducirTipoDocumentoCliente(cliente.TipoDocumento, numDocCliente);
            if (!resTipoDocCli.EsValido)
            {
                errores.Add(resTipoDocCli.Error!);
            }
            else
            {
                tipoDocCliente = resTipoDocCli.Valor!;
            }
        }

        // Vehículo y validación estricta de Chasis / VIN y Placa
        vehiculo ??= orden.Vehiculo;
        if (vehiculo is null)
        {
            errores.Add("La orden de servicio no tiene la entidad Vehículo cargada ni suministrada.");
        }

        var proyPlaca = YamahaCatalogMapper.ProyectarPlaca(vehiculo?.Placa);
        if (!proyPlaca.EsValido)
        {
            errores.Add($"Placa vehicular inválida: {proyPlaca.Error}");
        }

        var proyChasis = YamahaCatalogMapper.ProyectarChasis(vehiculo?.NumeroSerieVIN);
        if (!proyChasis.EsValido)
        {
            errores.Add($"Chasis/VIN vehicular inválido: {proyChasis.Error}");
        }

        var marcaVehiculo = vehiculo?.Marca?.Trim() ?? string.Empty;
        if (!string.Equals(marcaVehiculo, YamahaV3Catalogos.MarcaFijaYamaha, StringComparison.OrdinalIgnoreCase))
        {
            errores.Add($"La unidad atendida es marca '{marcaVehiculo}'. La Sección 6.2 exige que la marca sea fija 'YAMAHA'.");
        }

        // Clasificación de la línea: Repuesto (SP) vs Mano de Obra / Servicio (STE)
        var clasifSubLinea = YamahaCatalogMapper.ClasificarSubLineaServicio(detalle.TipoItem);
        if (!clasifSubLinea.EsValido)
        {
            errores.Add(clasifSubLinea.Error!);
        }

        var subLinea = clasifSubLinea.Valor ?? string.Empty;

        // Variables condicionales según SubLineaNegocio
        string codigoYamaha;
        string descripcion;
        int? cantidad;
        decimal? precioUnit;
        string tipoServicio;
        string actividad;

        if (string.Equals(subLinea, YamahaV3Catalogos.LineaSpareParts, StringComparison.Ordinal))
        {
            // Línea de Repuesto (SP)
            var producto = detalle.Producto;
            codigoYamaha = producto?.Codigo?.Trim() ?? string.Empty;
            descripcion = producto?.Nombre?.Trim() ?? detalle.Descripcion.Trim();

            if (producto is null)
            {
                errores.Add("La línea de repuesto en la orden no cuenta con el Producto asociado cargado para obtener el código SKU.");
            }
            else if (string.IsNullOrWhiteSpace(codigoYamaha))
            {
                errores.Add("El repuesto utilizado no cuenta con código SKU (CodigoYamaha).");
            }

            if (detalle.Cantidad <= 0)
            {
                errores.Add($"La cantidad del repuesto debe ser mayor a 0 (recibido: {detalle.Cantidad}).");
            }

            cantidad = detalle.Cantidad;
            precioUnit = detalle.PrecioUnitario > 0
                ? Math.Round(detalle.PrecioUnitario, 2, MidpointRounding.AwayFromZero)
                : 0.00m;

            // Sección 6.3: El valor '-' es el único permitido para TipoServicio en líneas SP
            tipoServicio = YamahaCatalogMapper.ObtenerTipoServicioParaRepuesto();
            actividad = !string.IsNullOrWhiteSpace(detalle.Descripcion)
                ? detalle.Descripcion.Trim()
                : (producto?.Nombre?.Trim() ?? "REPUESTO");
        }
        else
        {
            // Línea de Servicio / Mano de Obra (STE)
            codigoYamaha = string.Empty;
            descripcion = !string.IsNullOrWhiteSpace(detalle.Descripcion)
                ? detalle.Descripcion.Trim()
                : "SERVICIO TECNICO";

            // En STE, Cantidad y PrecioUnit no aplican (se serializan como "" según Sección 3.4 y 6.2)
            cantidad = null;
            precioUnit = null;

            // Traducción del TipoAtencion del CRM a código de Yamaha v3 (D-09)
            var resTipoAtencion = YamahaCatalogMapper.TraducirTipoAtencion(orden.TipoAtencion);
            if (!resTipoAtencion.EsValido)
            {
                errores.Add(resTipoAtencion.Error!);
                tipoServicio = string.Empty;
            }
            else
            {
                tipoServicio = resTipoAtencion.Valor!;
            }

            actividad = !string.IsNullOrWhiteSpace(detalle.Descripcion)
                ? detalle.Descripcion.Trim()
                : "MANO DE OBRA";
        }

        if (errores.Count > 0)
        {
            return YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto>.NoProyectable(idOrigen, errores, observaciones);
        }

        // Totales monetarios de la línea
        var montoConIgv = detalle.Total > 0
            ? Math.Round(detalle.Total, 2, MidpointRounding.AwayFromZero)
            : Math.Round(detalle.Cantidad * detalle.PrecioUnitario, 2, MidpointRounding.AwayFromZero);

        decimal montoSinIgv;
        if (detalle.TipoAfectacionIgv == TipoAfectacionIgv.Gravado)
        {
            montoSinIgv = detalle.SubtotalGravado > 0
                ? Math.Round(detalle.SubtotalGravado, 2, MidpointRounding.AwayFromZero)
                : Math.Round(montoConIgv / 1.18m, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            // Exonerado o Inafecto: no hay IGV; MontoSinIGV es igual al monto total
            montoSinIgv = montoConIgv;
        }

        var totalConIgv = montoConIgv;

        observaciones.Add("D-06: Descuento asignado en 0.00 debido a que DetalleServicio no desglosa descuentos por ítem.");

        var nombreCliente = !string.IsNullOrWhiteSpace(cliente!.NombreCompleto)
            ? cliente.NombreCompleto.Trim()
            : (cliente.RazonSocial?.Trim() ?? string.Empty);

        var fechaFacturacion = YamahaCatalogMapper.FormatearFecha(comprobante.FechaCreacion);
        var kilometraje = orden.KilometrajeIngreso ?? (vehiculo?.Kilometraje ?? 0);
        if (kilometraje < 0)
        {
            kilometraje = 0;
        }

        var celular = cliente.Telefono?.Trim() ?? string.Empty;
        var correo = cliente.Email?.Trim() ?? string.Empty;
        var modelo = vehiculo?.Modelo?.Trim() ?? string.Empty;
        var version = vehiculo?.Anio?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        var dto = new YamahaServicioTecnicoV3Dto
        {
            LineaNegocio = YamahaV3Catalogos.LineaServicioTecnico, // STE
            SubLineaNegocio = subLinea, // SP o STE
            RazonSocial = contexto.RazonSocial,
            RUC = contexto.Ruc,
            NombreComercialPuntoVenta = contexto.NombreComercialPuntoVenta ?? string.Empty,
            CodigoPuntoVenta = contexto.CodigoPuntoVenta,
            NumeroOrdenTrabajo = proyNumeroOrden.Valor!,
            FechaFacturacion = fechaFacturacion,
            Estado = YamahaV3Catalogos.EstadoOrdenFacturado, // Fijo FACTURADO (Sección 6.2)
            TipoDocumentoCliente = tipoDocCliente,
            NumeroDocumentoCliente = numDocCliente!,
            NombreCliente = nombreCliente,
            Celular = celular,
            Correo = correo,
            Placa = proyPlaca.Valor!,
            Chasis = proyChasis.Valor!,
            Marca = YamahaV3Catalogos.MarcaFijaYamaha, // Fijo YAMAHA
            Modelo = modelo,
            Version = version,
            Kilometraje = kilometraje,
            TipoServicio = tipoServicio,
            Actividad = actividad,
            CodigoYamaha = codigoYamaha,
            Descripcion = descripcion,
            Cantidad = cantidad,
            MontoSinIGV = montoSinIgv,
            MontoConIGV = montoConIgv,
            PrecioUnit = precioUnit,
            Descuento = 0.00m,
            TotalConIGV = totalConIgv
        };

        return YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto>.Exitoso(dto, idOrigen, observaciones);
    }

    /// <inheritdoc />
    public YamahaMapeoLoteResultado<YamahaServicioTecnicoV3Dto> MapearOrdenServicio(
        OrdenServicio orden,
        YamahaPuntoVentaContexto contexto,
        Comprobante? comprobante = null,
        Cliente? cliente = null,
        Vehiculo? vehiculo = null)
    {
        ArgumentNullException.ThrowIfNull(orden);
        ArgumentNullException.ThrowIfNull(contexto);

        var idOrigen = $"Orden-{orden.Id}";

        // Sección 6.2: Solo se reportan órdenes facturadas con comprobante emitido.
        // Selección determinista de comprobante final de liquidación:
        // Se excluyen comprobantes inactivos y anulados. Si existen múltiples comprobantes
        // (por ejemplo, anticipos previos y factura final de liquidación), se selecciona
        // el más reciente por FechaCreacion.
        comprobante ??= orden.Comprobantes
            .Where(c => c.Activo && !string.Equals(c.Estado, "Anulado", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.FechaCreacion)
            .FirstOrDefault();

        if (comprobante is null)
        {
            comprobante = orden.Ventas
                .Where(v => v.Activo && v.Estado == EstadoVenta.Confirmada && v.Comprobante is not null && v.Comprobante.Activo && !string.Equals(v.Comprobante.Estado, "Anulado", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.Comprobante!.FechaCreacion)
                .Select(v => v.Comprobante!)
                .FirstOrDefault();
        }

        if (comprobante is null)
        {
            return new YamahaMapeoLoteResultado<YamahaServicioTecnicoV3Dto>(
                Array.Empty<YamahaServicioTecnicoV3Dto>(),
                new[]
                {
                    YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto>.NoProyectable(
                        idOrigen,
                        "La orden de servicio no cuenta con comprobante de facturación emitido. Según la Sección 6.2 de Yamaha v3, solo se reciben órdenes ya facturadas; no se aceptan órdenes abiertas, pendientes ni cerradas sin facturar.")
                },
                TotalProcesados: 0,
                TotalValidos: 0,
                TotalNoProyectables: 1);
        }

        var detallesActivos = orden.Detalles.Where(d => d.Activo).ToList();
        if (detallesActivos.Count == 0)
        {
            return new YamahaMapeoLoteResultado<YamahaServicioTecnicoV3Dto>(
                Array.Empty<YamahaServicioTecnicoV3Dto>(),
                new[]
                {
                    YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto>.NoProyectable(
                        idOrigen,
                        "La orden de servicio no cuenta con líneas de detalle activas (repuestos o mano de obra) para reportar.")
                },
                TotalProcesados: 0,
                TotalValidos: 0,
                TotalNoProyectables: 1);
        }

        var dtos = new List<YamahaServicioTecnicoV3Dto>();
        var resultados = new List<YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto>>();

        foreach (var detalle in detallesActivos)
        {
            var res = MapearDetalleServicio(orden, detalle, contexto, comprobante, cliente, vehiculo);
            resultados.Add(res);
            if (res.EsValido && res.Dto is not null)
            {
                dtos.Add(res.Dto);
            }
        }

        return new YamahaMapeoLoteResultado<YamahaServicioTecnicoV3Dto>(
            dtos,
            resultados,
            TotalProcesados: detallesActivos.Count,
            TotalValidos: dtos.Count,
            TotalNoProyectables: detallesActivos.Count - dtos.Count);
    }

    /// <inheritdoc />
    public YamahaMapeoLoteResultado<YamahaServicioTecnicoV3Dto> MapearLoteOrdenesServicio(
        IEnumerable<OrdenServicio> ordenes,
        YamahaPuntoVentaContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(ordenes);
        ArgumentNullException.ThrowIfNull(contexto);

        var dtos = new List<YamahaServicioTecnicoV3Dto>();
        var resultados = new List<YamahaMapeoRegistroResultado<YamahaServicioTecnicoV3Dto>>();
        var totalProcesados = 0;

        foreach (var orden in ordenes)
        {
            var resOrden = MapearOrdenServicio(orden, contexto);
            totalProcesados += resOrden.TotalProcesados;
            dtos.AddRange(resOrden.DtosProyectados);
            resultados.AddRange(resOrden.Registros);
        }

        return new YamahaMapeoLoteResultado<YamahaServicioTecnicoV3Dto>(
            dtos,
            resultados,
            TotalProcesados: totalProcesados,
            TotalValidos: dtos.Count,
            TotalNoProyectables: totalProcesados - dtos.Count);
    }
}
