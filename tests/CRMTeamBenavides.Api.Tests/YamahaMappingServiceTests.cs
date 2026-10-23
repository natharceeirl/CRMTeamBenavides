using System.Globalization;
using System.Text.Json;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Domain.Entities;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Suite de pruebas unitarias exhaustivas para la Fase B de Yamaha v3:
/// traductores deterministas (<see cref="YamahaCatalogMapper"/>) y servicio de proyección pura
/// (<see cref="YamahaMappingService"/>) en memoria, sin efectos secundarios ni llamadas externas.
/// </summary>
public class YamahaMappingServiceTests
{
    private readonly IYamahaMappingService _mapper = new YamahaMappingService();

    private static YamahaPuntoVentaContexto CrearContextoPrueba() =>
        new(
            RazonSocial: "Team Benavides S.R.L.",
            Ruc: "20123456789",
            CodigoPuntoVenta: "PE0001",
            NombreComercialPuntoVenta: "Team Benavides - Arequipa",
            TipoPuntoVenta: "3S",
            CiudadSede: "Arequipa",
            DepartamentoSede: "Arequipa",
            DistritoSede: "Arequipa");

    // =========================================================================
    // 1. Traductores Deterministas: Tipo de Comprobante
    // =========================================================================

    [Theory]
    [InlineData(TipoComprobante.Factura, "FACTURA")]
    [InlineData(TipoComprobante.Boleta, "BOLETA DE VENTA")]
    public void TraducirTipoComprobante_ConEnum_DebeMapearExacto(TipoComprobante tipo, string esperado)
    {
        var res = YamahaCatalogMapper.TraducirTipoComprobante(tipo);
        Assert.True(res.EsValido);
        Assert.Equal(esperado, res.Valor);
    }

    [Fact]
    public void TraducirTipoComprobante_ConEnumInvalido_DebeFallarExplicitamente()
    {
        var res = YamahaCatalogMapper.TraducirTipoComprobante((TipoComprobante)999);
        Assert.False(res.EsValido);
        Assert.NotNull(res.Error);
    }

    [Theory]
    [InlineData("Factura", "FACTURA")]
    [InlineData("factura", "FACTURA")]
    [InlineData("01", "FACTURA")]
    [InlineData("Boleta", "BOLETA DE VENTA")]
    [InlineData("boleta", "BOLETA DE VENTA")]
    [InlineData("03", "BOLETA DE VENTA")]
    [InlineData("NotaDeCredito", "NOTA DE CREDITO")]
    [InlineData("Nota de Credito", "NOTA DE CREDITO")]
    [InlineData("07", "NOTA DE CREDITO")]
    [InlineData("NotaDeDebito", "NOTA DE DEBITO")]
    [InlineData("08", "NOTA DE DEBITO")]
    public void TraducirTipoComprobante_ConCadena_DebeMapearExacto(string tipoStr, string esperado)
    {
        var res = YamahaCatalogMapper.TraducirTipoComprobante(tipoStr);
        Assert.True(res.EsValido);
        Assert.Equal(esperado, res.Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ticket")]
    [InlineData("Recibo")]
    public void TraducirTipoComprobante_ConCadenaInvalida_DebeFallarExplicitamente(string? tipoStr)
    {
        var res = YamahaCatalogMapper.TraducirTipoComprobante(tipoStr);
        Assert.False(res.EsValido);
        Assert.NotNull(res.Error);
    }

    // =========================================================================
    // 2. Traductores Deterministas: Tipo de Documento de Cliente
    // =========================================================================

    [Fact]
    public void TraducirTipoDocumentoCliente_DniYRuc_DebeMapearDirecto()
    {
        var resDni = YamahaCatalogMapper.TraducirTipoDocumentoCliente(TipoDocumentoCliente.DNI, "71234567");
        Assert.True(resDni.EsValido);
        Assert.Equal("DNI", resDni.Valor);

        var resRuc = YamahaCatalogMapper.TraducirTipoDocumentoCliente(TipoDocumentoCliente.RUC, "20123456789");
        Assert.True(resRuc.EsValido);
        Assert.Equal("RUC", resRuc.Valor);
    }

    [Theory]
    [InlineData("71234567", "DOC NO DOMICILIADO")]    // 8 dígitos con tipo Otro no debe inferir DNI
    [InlineData("20123456789", "DOC NO DOMICILIADO")] // 11 dígitos con tipo Otro no debe inferir RUC
    [InlineData("ABC123456", "CE")]                    // 9 caracteres alfanuméricos corresponde a Carné de Extranjería
    [InlineData("PAS12345678", "DOC NO DOMICILIADO")] // Pasaporte / extranjero
    public void TraducirTipoDocumentoCliente_CuandoEsOtro_DebeMapearCeONoDomiciliado(string numero, string esperado)
    {
        var res = YamahaCatalogMapper.TraducirTipoDocumentoCliente(TipoDocumentoCliente.Otro, numero);
        Assert.True(res.EsValido);
        Assert.Equal(esperado, res.Valor);
    }

    [Theory]
    [InlineData("71234567", "DNI")]       // 8 dígitos inferido a DNI si tipo es nulo
    [InlineData("20123456789", "RUC")]     // 11 dígitos inferido a RUC si tipo es nulo
    [InlineData("ABC123456", "CE")]        // 9 caracteres inferido a CE si tipo es nulo
    public void TraducirTipoDocumentoCliente_CuandoTipoEsNulo_DebeInferirSegunLongitud(string numero, string esperado)
    {
        var res = YamahaCatalogMapper.TraducirTipoDocumentoCliente(null, numero);
        Assert.True(res.EsValido);
        Assert.Equal(esperado, res.Valor);
    }

    [Fact]
    public void TraducirTipoDocumentoCliente_SinDatos_DebeFallarExplicitamente()
    {
        var res = YamahaCatalogMapper.TraducirTipoDocumentoCliente(null, null);
        Assert.False(res.EsValido);
        Assert.NotNull(res.Error);
    }

    // =========================================================================
    // 3. Traductores Deterministas: TipoAtencion a TipoServicio (D-09)
    // =========================================================================

    [Theory]
    [InlineData(TipoAtencion.MantenimientoPreventivo, "MP")]
    [InlineData(TipoAtencion.MantenimientoCorrectivo, "R")]
    [InlineData(TipoAtencion.ReclamoGarantia, "G")]
    [InlineData(TipoAtencion.Gratuito, "SG")]
    public void TraducirTipoAtencion_ConEnumsExistentes_DebeMapearExacto(TipoAtencion tipo, string esperado)
    {
        var res = YamahaCatalogMapper.TraducirTipoAtencion(tipo);
        Assert.True(res.EsValido);
        Assert.Equal(esperado, res.Valor);
    }

    [Fact]
    public void TraducirTipoAtencion_ValorNoContemplado_DebeFallarSinInventarCodigo()
    {
        var res = YamahaCatalogMapper.TraducirTipoAtencion((TipoAtencion)999);
        Assert.False(res.EsValido);
        Assert.NotNull(res.Error);
        Assert.Contains("999", res.Error);
    }

    [Fact]
    public void ObtenerTipoServicioParaRepuesto_DebeSerGuionFijoSegunSeccion63()
    {
        Assert.Equal("-", YamahaCatalogMapper.ObtenerTipoServicioParaRepuesto());
    }

    // =========================================================================
    // 4. Traductores Deterministas: Clasificación SubLínea de Negocio
    // =========================================================================

    [Fact]
    public void ClasificarSubLineaServicio_RepuestoEsSp_ServiciosSonSte()
    {
        Assert.Equal("SP", YamahaCatalogMapper.ClasificarSubLineaServicio(TipoItemServicio.Repuesto).Valor);
        Assert.Equal("STE", YamahaCatalogMapper.ClasificarSubLineaServicio(TipoItemServicio.Servicio).Valor);
        Assert.Equal("STE", YamahaCatalogMapper.ClasificarSubLineaServicio(TipoItemServicio.ManoDeObra).Valor);
        Assert.Equal("STE", YamahaCatalogMapper.ClasificarSubLineaServicio(TipoItemServicio.Terceros).Valor);
    }

    [Fact]
    public void ClasificarSubLineaServicio_Invalido_DebeFallar()
    {
        var res = YamahaCatalogMapper.ClasificarSubLineaServicio((TipoItemServicio)999);
        Assert.False(res.EsValido);
    }

    // =========================================================================
    // 5. Proyección de Correlativo OS- a OT- sin mutar fuente (D-08)
    // =========================================================================

    [Theory]
    [InlineData("OS-000001", "OT-000001")]
    [InlineData("os-000001", "OT-000001")]
    [InlineData("OS-2026-0042", "OT-2026-0042")]
    [InlineData("OT-000123", "OT-000123")]
    [InlineData("12345", "OT-12345")]
    public void ProyectarNumeroOrden_CasosValidos_DebeProyectarFormatoOt(string original, string esperado)
    {
        var res = YamahaCatalogMapper.ProyectarNumeroOrden(original);
        Assert.True(res.EsValido);
        Assert.Equal(esperado, res.Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("OS-")]
    [InlineData("OT-")]
    [InlineData("ORDEN-001")]
    [InlineData("OS-ABC-DEF")]
    public void ProyectarNumeroOrden_CasosInvalidos_DebeFallarExplicitamente(string? original)
    {
        var res = YamahaCatalogMapper.ProyectarNumeroOrden(original);
        Assert.False(res.EsValido);
        Assert.NotNull(res.Error);
    }

    [Fact]
    public void ProyectarNumeroOrden_NoDebeMutarEntidadOrdenServicio()
    {
        var orden = new OrdenServicio { NumeroOrden = "OS-000045" };

        var res = YamahaCatalogMapper.ProyectarNumeroOrden(orden.NumeroOrden);

        Assert.True(res.EsValido);
        Assert.Equal("OT-000045", res.Valor);
        Assert.Equal("OS-000045", orden.NumeroOrden); // La entidad no fue mutada
    }

    // =========================================================================
    // 6. Proyección de Placa y Chasis
    // =========================================================================

    [Theory]
    [InlineData("ABC-123", "ABC-123")]
    [InlineData("1234-5A", "1234-5A")]
    public void ProyectarPlaca_Valida_DebeAceptar(string original, string esperado)
    {
        var res = YamahaCatalogMapper.ProyectarPlaca(original);
        Assert.True(res.EsValido);
        Assert.Equal(esperado, res.Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("--")]
    [InlineData("ABC 123")]
    [InlineData("ABC123")]
    [InlineData("-ABC123-")]
    public void ProyectarPlaca_Invalida_DebeFallar(string? original)
    {
        var res = YamahaCatalogMapper.ProyectarPlaca(original);
        Assert.False(res.EsValido);
    }

    [Fact]
    public void ProyectarChasis_LongitudCorrecta_DebeAceptar()
    {
        var res = YamahaCatalogMapper.ProyectarChasis("3VWFE21C04M000001");
        Assert.True(res.EsValido);
        Assert.Equal("3VWFE21C04M000001", res.Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CHASIS CORTO")] // espacios y muy corto
    [InlineData("12345")]        // menos de 15 caracteres
    [InlineData("123456789012345678901")] // 21 caracteres (más de 20)
    public void ProyectarChasis_Invalido_DebeFallar(string? original)
    {
        var res = YamahaCatalogMapper.ProyectarChasis(original);
        Assert.False(res.EsValido);
    }

    // =========================================================================
    // 7. Servicio de Mapeo: Ventas (Sección 4)
    // =========================================================================

    [Fact]
    public void MapearVenta_ConDatosCompletos_DebeProyectarYPasarValidadorYamahaV3()
    {
        var contexto = CrearContextoPrueba();
        var cliente = new Cliente
        {
            NombreCompleto = "Carlos Mendoza Pérez",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "45678901",
            Telefono = "987654321",
            Email = "carlos.mendoza@example.com",
            Direccion = "Av. Cayma 456"
        };

        var producto = new Producto
        {
            Codigo = "90793-AJ401",
            Nombre = "Aceite Yamalube 4T 10W-40 Semisintético 1L",
            Costo = 28.00m,
            PrecioVenta = 38.00m,
            StockActual = 24
        };

        var detalle = new DetalleVenta
        {
            Id = Guid.NewGuid(),
            Producto = producto,
            ProductoId = producto.Id,
            Cantidad = 2,
            PrecioUnitario = 38.00m,
            SubtotalGravado = 64.41m,
            MontoIgv = 11.59m,
            Total = 76.00m,
            Activo = true
        };

        var comprobante = new Comprobante
        {
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "00004521",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 10, 10, 15, 30, 0, DateTimeKind.Utc)
        };

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            ClienteId = cliente.Id,
            Comprobante = comprobante,
            Estado = EstadoVenta.Confirmada,
            Fecha = new DateTime(2026, 10, 10, 15, 30, 0, DateTimeKind.Utc),
            Detalles = new List<DetalleVenta> { detalle }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);

        Assert.True(loteRes.EsLoteCompletamenteValido);
        Assert.Single(loteRes.DtosProyectados);

        var dto = loteRes.DtosProyectados[0];
        Assert.Equal("Team Benavides S.R.L.", dto.RazonSocial);
        Assert.Equal("20123456789", dto.RUC);
        Assert.Equal("PE0001", dto.CodigoPuntoVenta);
        Assert.Equal("10/10/2026", dto.FechaFacturacion);
        Assert.Equal("B001-00004521", dto.NumeroComprobante);
        Assert.Equal("BOLETA DE VENTA", dto.TipoDocumentoVenta);
        Assert.Equal("CONTADO", dto.TipoPago);
        Assert.Equal("CONTADO", dto.EntidadFinanciera);
        Assert.Equal("SP", dto.LineaNegocio);
        Assert.Equal("MOSTRADOR", dto.Canal);
        Assert.Equal(2, dto.CantidadVendida);
        Assert.Equal(76.00m, dto.PrecioFinal);
        Assert.Equal("90793-AJ401", dto.CodigoYamaha);

        // Validamos contra el motor oficial YamahaV3Validator
        var validacion = YamahaV3Validator.ValidarLoteVentas(loteRes.DtosProyectados);
        Assert.True(validacion.EsLoteValido, string.Join("; ", validacion.Hallazgos.Select(h => $"{h.Campo}: {h.Mensaje}")));
        Assert.Equal(0, validacion.TotalErroresBloqueantes);
    }

    [Fact]
    public void MapearVenta_VinculadaAOrdenServicio_DebeRetornarNoProyectableExplicandoServicio3()
    {
        var contexto = CrearContextoPrueba();
        var ordenId = Guid.NewGuid();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Confirmada,
            OrdenServicioId = ordenId,
            Cliente = new Cliente { NombreCompleto = "Juan Perez", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI },
            Comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000001", Estado = "Emitido", FechaCreacion = DateTime.UtcNow },
            Detalles = new List<DetalleVenta>
            {
                new()
                {
                    Producto = new Producto { Codigo = "2DP-E4451-00", Nombre = "Elemento Filtro de Aire NMAX" },
                    Cantidad = 1,
                    PrecioUnitario = 45.00m,
                    Total = 45.00m,
                    SubtotalGravado = 38.14m,
                    Activo = true
                }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);

        Assert.False(loteRes.EsLoteCompletamenteValido);
        Assert.Equal(0, loteRes.TotalValidos);
        Assert.Equal(1, loteRes.TotalNoProyectables);
        Assert.Empty(loteRes.DtosProyectados);
        var reg = Assert.Single(loteRes.Registros);
        Assert.False(reg.EsValido);
        Assert.Contains(reg.ErroresBloqueantes, err => err.Contains(ordenId.ToString()) && err.Contains("Servicio 3"));
    }

    [Fact]
    public void MapearDetalleVenta_VinculadoAOrdenServicio_DebeRetornarNoProyectableExplicandoServicio3()
    {
        var contexto = CrearContextoPrueba();
        var ordenId = Guid.NewGuid();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Confirmada,
            OrdenServicioId = ordenId,
            Cliente = new Cliente { NombreCompleto = "Juan Perez", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI },
            Comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000001", Estado = "Emitido", FechaCreacion = DateTime.UtcNow }
        };
        var detalle = new DetalleVenta
        {
            Producto = new Producto { Codigo = "2DP-E4451-00", Nombre = "Elemento Filtro de Aire NMAX" },
            Cantidad = 1,
            PrecioUnitario = 45.00m,
            Total = 45.00m,
            SubtotalGravado = 38.14m,
            Activo = true
        };

        var res = _mapper.MapearDetalleVenta(venta, detalle, contexto);

        Assert.False(res.EsValido);
        Assert.Null(res.Dto);
        Assert.Contains(res.ErroresBloqueantes, err => err.Contains(ordenId.ToString()) && err.Contains("Servicio 3"));
    }

    [Fact]
    public void MapearProductoStock_CostoDeAdquisicion_DebeUsarProductoCosto()
    {
        var contexto = CrearContextoPrueba();
        var producto = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "90793-AJ421",
            Nombre = "Aceite Yamalube 4T 10W-40",
            Costo = 28.50m,
            PrecioVenta = 42.00m,
            StockActual = 15,
            Marca = "YAMAHA",
            FechaCreacion = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Activo = true
        };

        var res = _mapper.MapearProductoStock(producto, contexto);

        Assert.True(res.EsValido);
        Assert.NotNull(res.Dto);
        Assert.Equal(28.50m, res.Dto.CostoVentaSinIGV);
        Assert.Equal(33.63m, res.Dto.CostoVentaConIGV);
        Assert.Equal(15, res.Dto.StockDisponible);
        Assert.Contains(res.Observaciones, o => o.Contains("D-03") && o.Contains("costo de adquisición"));
    }

    [Fact]
    public void MapearVenta_SinComprobanteEmitido_DebeRetornarNoProyectable()
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Confirmada,
            Cliente = new Cliente { NombreCompleto = "Cliente Sin Factura", NumeroDocumento = "71234567" },
            Comprobante = null, // Venta no facturada aún
            Detalles = new List<DetalleVenta>
            {
                new() { Producto = new Producto { Codigo = "SKU-001", Nombre = "Repuesto" }, Cantidad = 1, Total = 50.00m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);

        Assert.False(loteRes.EsLoteCompletamenteValido);
        Assert.Empty(loteRes.DtosProyectados);
        Assert.Equal(1, loteRes.TotalNoProyectables);
        Assert.Contains("comprobante", loteRes.Registros[0].ErroresBloqueantes[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapearVenta_ComprobanteAnulado_DebeRetornarNoProyectable()
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Confirmada,
            Cliente = new Cliente { NombreCompleto = "Cliente", NumeroDocumento = "71234567" },
            Comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000002", Estado = "Anulado" },
            Detalles = new List<DetalleVenta>
            {
                new() { Producto = new Producto { Codigo = "SKU-001", Nombre = "Repuesto" }, Cantidad = 1, Total = 50.00m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);
        Assert.Empty(loteRes.DtosProyectados);
        Assert.Contains("Anulado", loteRes.Registros[0].ErroresBloqueantes[0]);
    }

    // --- Pruebas de Regresión Fix 1: Estado de la Venta (Solo Confirmada y Activa) ---

    [Theory]
    [InlineData(EstadoVenta.Cotizacion)]
    [InlineData(EstadoVenta.Anulada)]
    public void MapearVenta_EnEstadoNoConfirmado_DebeRetornarNoProyectable(EstadoVenta estadoInvalido)
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = estadoInvalido,
            Cliente = new Cliente { NombreCompleto = "Cliente Estado", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI },
            Comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000001", Estado = "Emitido", FechaCreacion = DateTime.UtcNow },
            Detalles = new List<DetalleVenta>
            {
                new() { Producto = new Producto { Codigo = "SKU-001", Nombre = "Repuesto" }, Cantidad = 1, Total = 50.00m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);
        Assert.False(loteRes.EsLoteCompletamenteValido);
        Assert.Empty(loteRes.DtosProyectados);
        Assert.Contains(loteRes.Registros[0].ErroresBloqueantes, e => e.Contains(estadoInvalido.ToString()) && e.Contains("Confirmada"));
    }

    [Fact]
    public void MapearVenta_Inactiva_DebeRetornarNoProyectable()
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Activo = false,
            Estado = EstadoVenta.Confirmada,
            Cliente = new Cliente { NombreCompleto = "Cliente Inactivo", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI },
            Comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000001", Estado = "Emitido", FechaCreacion = DateTime.UtcNow },
            Detalles = new List<DetalleVenta>
            {
                new() { Producto = new Producto { Codigo = "SKU-001", Nombre = "Repuesto" }, Cantidad = 1, Total = 50.00m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);
        Assert.False(loteRes.EsLoteCompletamenteValido);
        Assert.Empty(loteRes.DtosProyectados);
        Assert.Contains(loteRes.Registros[0].ErroresBloqueantes, e => e.Contains("Activo=False"));
    }

    [Fact]
    public void MapearDetalleVenta_EnEstadoCotizacion_DebeRetornarNoProyectable()
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Cotizacion,
            Cliente = new Cliente { NombreCompleto = "Cliente Cotizacion", NumeroDocumento = "71234567" },
            Comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000001", Estado = "Emitido", FechaCreacion = DateTime.UtcNow }
        };
        var detalle = new DetalleVenta
        {
            Producto = new Producto { Codigo = "SKU-001", Nombre = "Repuesto" },
            Cantidad = 1,
            Total = 50.00m,
            Activo = true
        };

        var res = _mapper.MapearDetalleVenta(venta, detalle, contexto);
        Assert.False(res.EsValido);
        Assert.Contains(res.ErroresBloqueantes, e => e.Contains("Cotizacion") && e.Contains("Confirmada"));
    }

    // --- Pruebas de Regresión Fix 2: Bloqueo de Notas de Crédito (D-04) ---

    [Theory]
    [InlineData("NotaDeCredito")]
    [InlineData("Nota de Credito")]
    [InlineData("Nota de Crédito")]
    [InlineData("NOTA DE CREDITO")]
    [InlineData("07")]
    public void MapearVenta_ConNotaDeCredito_DebeRetornarNoProyectableConReferenciaD04(string tipoNotaCredito)
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Confirmada,
            Cliente = new Cliente { NombreCompleto = "Cliente Devolucion", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI },
            Comprobante = new Comprobante { Tipo = tipoNotaCredito, Serie = "FC01", Numero = "00000050", Estado = "Emitido", FechaCreacion = DateTime.UtcNow },
            Detalles = new List<DetalleVenta>
            {
                new() { Producto = new Producto { Codigo = "SKU-001", Nombre = "Repuesto Devuelto" }, Cantidad = 1, Total = 50.00m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);
        Assert.False(loteRes.EsLoteCompletamenteValido);
        Assert.Empty(loteRes.DtosProyectados);
        Assert.Contains(loteRes.Registros[0].ErroresBloqueantes, e => e.Contains("D-04") && e.Contains("NOTA DE CREDITO"));
    }

    [Theory]
    [InlineData("Factura", "FACTURA")]
    [InlineData("Boleta", "BOLETA DE VENTA")]
    public void MapearVenta_ConFacturaYBoleta_DebeContinuarOperando(string tipoComprobante, string esperadoTipoDocVenta)
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Confirmada,
            Cliente = new Cliente { NombreCompleto = "Cliente Regular", NumeroDocumento = "20123456789", TipoDocumento = TipoDocumentoCliente.RUC },
            Comprobante = new Comprobante { Tipo = tipoComprobante, Serie = "F001", Numero = "00000100", Estado = "Emitido", FechaCreacion = DateTime.UtcNow },
            Detalles = new List<DetalleVenta>
            {
                new() { Producto = new Producto { Codigo = "SKU-001", Nombre = "Repuesto" }, Cantidad = 1, Total = 118.00m, SubtotalGravado = 100.00m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);
        Assert.True(loteRes.EsLoteCompletamenteValido);
        Assert.Single(loteRes.DtosProyectados);
        Assert.Equal(esperadoTipoDocVenta, loteRes.DtosProyectados[0].TipoDocumentoVenta);
    }

    // --- Pruebas de Regresión Fix 4: No convertir TipoDocumentoCliente.Otro a DNI ---

    [Fact]
    public void MapearVenta_ClienteConDocumentoOtroDe8Digitos_DebeProyectarDocNoDomiciliado()
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Confirmada,
            Cliente = new Cliente
            {
                NombreCompleto = "John Foreigner Doe",
                TipoDocumento = TipoDocumentoCliente.Otro,
                NumeroDocumento = "12345678" // 8 dígitos pero explícitamente extranjero/Otro
            },
            Comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000101", Estado = "Emitido", FechaCreacion = DateTime.UtcNow },
            Detalles = new List<DetalleVenta>
            {
                new() { Producto = new Producto { Codigo = "SKU-001", Nombre = "Repuesto" }, Cantidad = 1, Total = 50.00m, SubtotalGravado = 42.37m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);
        Assert.True(loteRes.EsLoteCompletamenteValido);
        Assert.Equal("DOC NO DOMICILIADO", loteRes.DtosProyectados[0].TipoDocumentoCliente);
        Assert.NotEqual("DNI", loteRes.DtosProyectados[0].TipoDocumentoCliente);

        var validacion = YamahaV3Validator.ValidarLoteVentas(loteRes.DtosProyectados);
        Assert.True(validacion.EsLoteValido);
    }

    // --- Pruebas de Regresión Fix 5: Afectación Tributaria (TipoAfectacionIgv) en Ventas ---

    [Fact]
    public void MapearVenta_ConAfectacionGravada_DebeCalcularMontoSinIgvCorrecto()
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Confirmada,
            Cliente = new Cliente { NombreCompleto = "Cliente Gravado", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI },
            Comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000102", Estado = "Emitido", FechaCreacion = DateTime.UtcNow },
            Detalles = new List<DetalleVenta>
            {
                new()
                {
                    Producto = new Producto { Codigo = "SKU-GRAV", Nombre = "Item Gravado" },
                    Cantidad = 1,
                    PrecioUnitario = 118.00m,
                    Total = 118.00m,
                    SubtotalGravado = 100.00m,
                    MontoIgv = 18.00m,
                    TipoAfectacionIgv = TipoAfectacionIgv.Gravado,
                    Activo = true
                }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);
        Assert.True(loteRes.EsLoteCompletamenteValido);
        var dto = loteRes.DtosProyectados[0];
        Assert.Equal(100.00m, dto.MontoSinIGV);
        Assert.Equal(118.00m, dto.MontoConIGV);
        Assert.Equal(118.00m, dto.PrecioFinal);
    }

    [Theory]
    [InlineData(TipoAfectacionIgv.Exonerado)]
    [InlineData(TipoAfectacionIgv.Inafecto)]
    public void MapearVenta_ConAfectacionExoneradaOInafecta_MontoSinIgvDebeSerIgualAMontoConIgv(TipoAfectacionIgv afectacion)
    {
        var contexto = CrearContextoPrueba();
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            Estado = EstadoVenta.Confirmada,
            Cliente = new Cliente { NombreCompleto = "Cliente Exonerado", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI },
            Comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000103", Estado = "Emitido", FechaCreacion = DateTime.UtcNow },
            Detalles = new List<DetalleVenta>
            {
                new()
                {
                    Producto = new Producto { Codigo = "SKU-EXON", Nombre = "Item Sin IGV" },
                    Cantidad = 1,
                    PrecioUnitario = 100.00m,
                    Total = 100.00m,
                    SubtotalGravado = 0.00m,
                    MontoIgv = 0.00m,
                    TipoAfectacionIgv = afectacion,
                    Activo = true
                }
            }
        };

        var loteRes = _mapper.MapearVenta(venta, contexto);
        Assert.True(loteRes.EsLoteCompletamenteValido);
        var dto = loteRes.DtosProyectados[0];
        Assert.Equal(100.00m, dto.MontoSinIGV);
        Assert.Equal(100.00m, dto.MontoConIGV);
        Assert.Equal(100.00m, dto.PrecioFinal);
    }

    // =========================================================================
    // 8. Servicio de Mapeo: Stock (Sección 5)
    // =========================================================================

    [Fact]
    public void MapearProductoStock_ConStockPositivo_DebeProyectarYPasarValidadorYamahaV3()
    {
        var contexto = CrearContextoPrueba();
        var producto = new Producto
        {
            Codigo = "90793-AJ401",
            Nombre = "Aceite Yamalube 4T 10W-40",
            Costo = 28.50m,
            StockActual = 48,
            Marca = "YAMAHA",
            FechaCreacion = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        var res = _mapper.MapearProductoStock(producto, contexto);

        Assert.True(res.EsValido);
        var dto = res.Dto!;
        Assert.Equal("Team Benavides S.R.L.", dto.RazonSocial);
        Assert.Equal("20123456789", dto.RUC);
        Assert.Equal("PE0001", dto.CodigoPuntoVenta);
        Assert.Equal("SP", dto.LineaNegocio);
        Assert.Equal("90793-AJ401", dto.CodigoYamaha);
        Assert.Equal(48, dto.StockDisponible);
        Assert.Equal(28.50m, dto.CostoVentaSinIGV);
        Assert.Equal(33.63m, dto.CostoVentaConIGV); // 28.50 * 1.18 = 33.63
        Assert.Equal("15/09/2026", dto.FechaIngreso);
        Assert.Equal(string.Empty, dto.VIN);

        var validacion = YamahaV3Validator.ValidarLoteStock([dto]);
        Assert.True(validacion.EsLoteValido);
    }

    [Fact]
    public void MapearProductoStock_ConStockCero_EsValidoParaRepuestoSpSegunSeccion52()
    {
        var contexto = CrearContextoPrueba();
        var producto = new Producto
        {
            Codigo = "2DP-E4451-00",
            Nombre = "Filtro de Aire NMAX",
            Costo = 32.00m,
            StockActual = 0, // Stock en cero permitido para SP en Sección 5.2
            Marca = "YAMAHA"
        };

        var res = _mapper.MapearProductoStock(producto, contexto);

        Assert.True(res.EsValido);
        Assert.Equal(0, res.Dto!.StockDisponible);

        var validacion = YamahaV3Validator.ValidarLoteStock([res.Dto]);
        Assert.True(validacion.EsLoteValido);
    }

    [Fact]
    public void MapearProductoStock_ConStockNegativo_DebeRetornarNoProyectable()
    {
        var contexto = CrearContextoPrueba();
        var producto = new Producto
        {
            Codigo = "SKU-NEGATIVO",
            Nombre = "Producto Invalido",
            Costo = 10.00m,
            StockActual = -3
        };

        var res = _mapper.MapearProductoStock(producto, contexto);
        Assert.False(res.EsValido);
        Assert.Contains(res.ErroresBloqueantes, e => e.Contains("negativo"));
    }

    [Fact]
    public void MapearProductoStock_SinCodigo_DebeRetornarNoProyectable()
    {
        var contexto = CrearContextoPrueba();
        var producto = new Producto
        {
            Codigo = "",
            Nombre = "Sin Codigo",
            StockActual = 5
        };

        var res = _mapper.MapearProductoStock(producto, contexto);
        Assert.False(res.EsValido);
    }

    // =========================================================================
    // 9. Servicio de Mapeo: Servicio Técnico (Sección 6)
    // =========================================================================

    [Fact]
    public void MapearOrdenServicio_ConManoDeObraYRepuesto_DebeProyectarAmbasLineasYPasarValidador()
    {
        var contexto = CrearContextoPrueba();
        var cliente = new Cliente
        {
            NombreCompleto = "María Elena Flores",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "40123456",
            Telefono = "954123456",
            Email = "maria.flores@example.com"
        };

        var vehiculo = new Vehiculo
        {
            Marca = "YAMAHA",
            Modelo = "NMAX 155",
            Placa = "5432-1A",
            NumeroSerieVIN = "3VWFE21C04M000001",
            Anio = 2024,
            Kilometraje = 8500
        };

        var repuestoProducto = new Producto
        {
            Codigo = "2DP-E4451-00",
            Nombre = "Filtro de Aire NMAX"
        };

        var comprobante = new Comprobante
        {
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "00007890",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 10, 11, 11, 0, 0, DateTimeKind.Utc)
        };

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000125",
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            KilometrajeIngreso = 8500,
            Cliente = cliente,
            Vehiculo = vehiculo,
            Comprobantes = new List<Comprobante> { comprobante },
            Detalles = new List<DetalleServicio>
            {
                // Línea 1: Mano de Obra (STE)
                new()
                {
                    Id = Guid.NewGuid(),
                    TipoItem = TipoItemServicio.ManoDeObra,
                    Descripcion = "Mantenimiento preventivo 8,000 km",
                    Cantidad = 1,
                    PrecioUnitario = 90.00m,
                    SubtotalGravado = 76.27m,
                    Total = 90.00m,
                    Activo = true
                },
                // Línea 2: Repuesto (SP)
                new()
                {
                    Id = Guid.NewGuid(),
                    TipoItem = TipoItemServicio.Repuesto,
                    Producto = repuestoProducto,
                    ProductoId = repuestoProducto.Id,
                    Descripcion = "Filtro de Aire NMAX",
                    Cantidad = 1,
                    PrecioUnitario = 45.00m,
                    SubtotalGravado = 38.14m,
                    Total = 45.00m,
                    Activo = true
                }
            }
        };

        var loteRes = _mapper.MapearOrdenServicio(orden, contexto);

        Assert.True(loteRes.EsLoteCompletamenteValido);
        Assert.Equal(2, loteRes.DtosProyectados.Count);

        var lineaSte = loteRes.DtosProyectados[0];
        Assert.Equal("STE", lineaSte.LineaNegocio);
        Assert.Equal("STE", lineaSte.SubLineaNegocio);
        Assert.Equal("OT-000125", lineaSte.NumeroOrdenTrabajo); // Proyección OS- a OT-
        Assert.Equal("11/10/2026", lineaSte.FechaFacturacion);
        Assert.Equal("FACTURADO", lineaSte.Estado);
        Assert.Equal("YAMAHA", lineaSte.Marca);
        Assert.Equal("3VWFE21C04M000001", lineaSte.Chasis);
        Assert.Equal("5432-1A", lineaSte.Placa);
        Assert.Equal("MP", lineaSte.TipoServicio); // Traducido desde MantenimientoPreventivo
        Assert.Null(lineaSte.Cantidad);             // null para STE
        Assert.Null(lineaSte.PrecioUnit);            // null para STE
        Assert.Equal(string.Empty, lineaSte.CodigoYamaha);

        var lineaSp = loteRes.DtosProyectados[1];
        Assert.Equal("STE", lineaSp.LineaNegocio);
        Assert.Equal("SP", lineaSp.SubLineaNegocio);
        Assert.Equal("OT-000125", lineaSp.NumeroOrdenTrabajo);
        Assert.Equal("-", lineaSp.TipoServicio);    // Sección 6.3: '-' para repuestos
        Assert.Equal(1, lineaSp.Cantidad);
        Assert.Equal(45.00m, lineaSp.PrecioUnit);
        Assert.Equal("2DP-E4451-00", lineaSp.CodigoYamaha);

        // Validación estricta con el validador oficial Yamaha v3
        var validacion = YamahaV3Validator.ValidarLoteServicioTecnico(loteRes.DtosProyectados);
        Assert.True(validacion.EsLoteValido, string.Join("; ", validacion.Hallazgos.Select(h => $"{h.Campo}: {h.Mensaje}")));
        Assert.Equal(0, validacion.TotalErroresBloqueantes);
    }

    [Fact]
    public void MapearOrdenServicio_SinComprobanteEmitido_DebeRetornarNoProyectableSinInventarFecha()
    {
        var contexto = CrearContextoPrueba();
        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000199",
            Estado = EstadoOrdenServicio.Entregada, // Cerrada pero sin facturar
            Cliente = new Cliente { NombreCompleto = "Cliente Sin Facturar", NumeroDocumento = "71234567" },
            Vehiculo = new Vehiculo { Marca = "YAMAHA", Placa = "ABC-123", NumeroSerieVIN = "3VWFE21C04M000001" },
            Comprobantes = new List<Comprobante>(), // Sin comprobantes
            Detalles = new List<DetalleServicio>
            {
                new() { TipoItem = TipoItemServicio.ManoDeObra, Descripcion = "Servicio", Cantidad = 1, PrecioUnitario = 50.00m, Total = 50.00m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearOrdenServicio(orden, contexto);

        Assert.False(loteRes.EsLoteCompletamenteValido);
        Assert.Empty(loteRes.DtosProyectados);
        Assert.Equal(1, loteRes.TotalNoProyectables);
        Assert.Contains("comprobante", loteRes.Registros[0].ErroresBloqueantes[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapearOrdenServicio_UnidadNoYamaha_DebeRetornarNoProyectable()
    {
        var contexto = CrearContextoPrueba();
        var comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000001", Estado = "Emitido", FechaCreacion = DateTime.UtcNow };
        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000200",
            Cliente = new Cliente { NombreCompleto = "Cliente Moto Honda", NumeroDocumento = "71234567" },
            Vehiculo = new Vehiculo { Marca = "HONDA", Placa = "ABC-123", NumeroSerieVIN = "3VWFE21C04M000001" },
            Comprobantes = new List<Comprobante> { comprobante },
            Detalles = new List<DetalleServicio>
            {
                new() { TipoItem = TipoItemServicio.ManoDeObra, Descripcion = "Servicio", Cantidad = 1, PrecioUnitario = 50.00m, Total = 50.00m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearOrdenServicio(orden, contexto);

        Assert.False(loteRes.EsLoteCompletamenteValido);
        Assert.Contains(loteRes.Registros[0].ErroresBloqueantes, e => e.Contains("YAMAHA"));
    }

    // --- Pruebas de Regresión Fix 3: Selección determinista de Comprobante en Taller ---

    [Fact]
    public void MapearOrdenServicio_ConMultiplesComprobantes_DebeSeleccionarElMasRecienteDeterminista()
    {
        var contexto = CrearContextoPrueba();
        var cliente = new Cliente { NombreCompleto = "Cliente Taller Multiple", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI };
        var vehiculo = new Vehiculo { Marca = "YAMAHA", Modelo = "FZ 25", Placa = "1234-5B", NumeroSerieVIN = "3VWFE21C04M000002", Anio = 2023, Kilometraje = 5000 };

        var comprobanteAnticipo = new Comprobante
        {
            Id = Guid.NewGuid(),
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "00000010",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
            Activo = true
        };

        var comprobanteLiquidacionFinal = new Comprobante
        {
            Id = Guid.NewGuid(),
            Tipo = "Factura",
            Serie = "F001",
            Numero = "00000088",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 10, 10, 16, 0, 0, DateTimeKind.Utc),
            Activo = true
        };

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000450",
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            KilometrajeIngreso = 5000,
            Cliente = cliente,
            Vehiculo = vehiculo,
            Comprobantes = new List<Comprobante> { comprobanteAnticipo, comprobanteLiquidacionFinal },
            Detalles = new List<DetalleServicio>
            {
                new() { TipoItem = TipoItemServicio.ManoDeObra, Descripcion = "Servicio Final", Cantidad = 1, PrecioUnitario = 150.00m, Total = 150.00m, SubtotalGravado = 127.12m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearOrdenServicio(orden, contexto);

        Assert.True(loteRes.EsLoteCompletamenteValido);
        // Debe tomar la fecha del comprobante más reciente (comprobanteLiquidacionFinal: 10/10/2026)
        Assert.Equal("10/10/2026", loteRes.DtosProyectados[0].FechaFacturacion);
    }

    [Fact]
    public void MapearOrdenServicio_ConComprobanteRecienteAnulado_DebeSeleccionarComprobanteActivoValido()
    {
        var contexto = CrearContextoPrueba();
        var cliente = new Cliente { NombreCompleto = "Cliente Taller", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI };
        var vehiculo = new Vehiculo { Marca = "YAMAHA", Modelo = "FZ 25", Placa = "1234-5B", NumeroSerieVIN = "3VWFE21C04M000002", Anio = 2023, Kilometraje = 5000 };

        var comprobanteValido = new Comprobante
        {
            Id = Guid.NewGuid(),
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "00000020",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc),
            Activo = true
        };

        var comprobanteAnuladoReciente = new Comprobante
        {
            Id = Guid.NewGuid(),
            Tipo = "Factura",
            Serie = "F001",
            Numero = "00000099",
            Estado = "Anulado",
            FechaCreacion = new DateTime(2026, 10, 12, 16, 0, 0, DateTimeKind.Utc),
            Activo = true
        };

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000451",
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            KilometrajeIngreso = 5000,
            Cliente = cliente,
            Vehiculo = vehiculo,
            Comprobantes = new List<Comprobante> { comprobanteValido, comprobanteAnuladoReciente },
            Detalles = new List<DetalleServicio>
            {
                new() { TipoItem = TipoItemServicio.ManoDeObra, Descripcion = "Servicio", Cantidad = 1, PrecioUnitario = 100.00m, Total = 100.00m, SubtotalGravado = 84.75m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearOrdenServicio(orden, contexto);

        Assert.True(loteRes.EsLoteCompletamenteValido);
        // Debe ignorar el anulado y tomar el comprobante activo válido (05/10/2026)
        Assert.Equal("05/10/2026", loteRes.DtosProyectados[0].FechaFacturacion);
    }

    [Fact]
    public void MapearOrdenServicio_ConComprobanteEnVentaVinculada_DebeSeleccionarElMasRecienteDeterminista()
    {
        var contexto = CrearContextoPrueba();
        var cliente = new Cliente { NombreCompleto = "Cliente Venta Vinculada", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI };
        var vehiculo = new Vehiculo { Marca = "YAMAHA", Modelo = "FZ 25", Placa = "1234-5B", NumeroSerieVIN = "3VWFE21C04M000002" };

        var compVentaAntigua = new Comprobante
        {
            Id = Guid.NewGuid(),
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "00000001",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
            Activo = true
        };

        var compVentaReciente = new Comprobante
        {
            Id = Guid.NewGuid(),
            Tipo = "Factura",
            Serie = "F001",
            Numero = "00000005",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 10, 9, 14, 0, 0, DateTimeKind.Utc),
            Activo = true
        };

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000453",
            TipoAtencion = TipoAtencion.MantenimientoCorrectivo,
            Cliente = cliente,
            Vehiculo = vehiculo,
            Comprobantes = new List<Comprobante>(), // Sin comprobantes directos en la orden
            Ventas = new List<Venta>
            {
                new() { Id = Guid.NewGuid(), Estado = EstadoVenta.Confirmada, Activo = true, Comprobante = compVentaAntigua },
                new() { Id = Guid.NewGuid(), Estado = EstadoVenta.Confirmada, Activo = true, Comprobante = compVentaReciente }
            },
            Detalles = new List<DetalleServicio>
            {
                new() { TipoItem = TipoItemServicio.ManoDeObra, Descripcion = "Reparación", Cantidad = 1, PrecioUnitario = 200.00m, Total = 200.00m, SubtotalGravado = 169.49m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearOrdenServicio(orden, contexto);

        Assert.True(loteRes.EsLoteCompletamenteValido);
        Assert.Equal("09/10/2026", loteRes.DtosProyectados[0].FechaFacturacion);
    }

    // --- Pruebas de Regresión Fix 5: Afectación Tributaria en Servicio Técnico ---

    [Theory]
    [InlineData(TipoAfectacionIgv.Exonerado)]
    [InlineData(TipoAfectacionIgv.Inafecto)]
    public void MapearOrdenServicio_ConAfectacionExoneradaOInafecta_MontoSinIgvDebeSerIgualAMontoConIgv(TipoAfectacionIgv afectacion)
    {
        var contexto = CrearContextoPrueba();
        var cliente = new Cliente { NombreCompleto = "Cliente Taller Exonerado", NumeroDocumento = "71234567", TipoDocumento = TipoDocumentoCliente.DNI };
        var vehiculo = new Vehiculo { Marca = "YAMAHA", Modelo = "NMAX", Placa = "5432-1A", NumeroSerieVIN = "3VWFE21C04M000001" };
        var comprobante = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "00000030", Estado = "Emitido", FechaCreacion = DateTime.UtcNow };

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000452",
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            Cliente = cliente,
            Vehiculo = vehiculo,
            Comprobantes = new List<Comprobante> { comprobante },
            Detalles = new List<DetalleServicio>
            {
                new()
                {
                    TipoItem = TipoItemServicio.ManoDeObra,
                    Descripcion = "Servicio Exonerado",
                    Cantidad = 1,
                    PrecioUnitario = 120.00m,
                    Total = 120.00m,
                    SubtotalGravado = 0.00m,
                    MontoIgv = 0.00m,
                    TipoAfectacionIgv = afectacion,
                    Activo = true
                }
            }
        };

        var loteRes = _mapper.MapearOrdenServicio(orden, contexto);
        Assert.True(loteRes.EsLoteCompletamenteValido);
        var dto = loteRes.DtosProyectados[0];
        Assert.Equal(120.00m, dto.MontoSinIGV);
        Assert.Equal(120.00m, dto.MontoConIGV);
        Assert.Equal(120.00m, dto.TotalConIGV);
    }

    // =========================================================================
    // 10. Cultura Invariante y Serialización Limpia sin Campos Extintos
    // =========================================================================

    [Fact]
    public void Mapeo_BajoCulturaConComaDecimal_DebeMantenerFechasYDecimalesConPunto()
    {
        var culturaOriginal = Thread.CurrentThread.CurrentCulture;
        try
        {
            // Simulamos cultura europea con coma decimal y formato de fecha variable
            Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");

            var contexto = CrearContextoPrueba();
            var producto = new Producto
            {
                Codigo = "90793-AJ401",
                Nombre = "Aceite Yamalube 4T",
                Costo = 28.50m,
                StockActual = 10,
                FechaCreacion = new DateTime(2026, 10, 5, 8, 30, 0, DateTimeKind.Utc)
            };

            var res = _mapper.MapearProductoStock(producto, contexto);
            Assert.True(res.EsValido);

            var json = YamahaJsonSerializer.SerializarStock([res.Dto!]);

            // Debe contener punto decimal y no coma decimal en los números
            Assert.Contains("\"CostoVentaSinIGV\":28.50", json, StringComparison.Ordinal);
            Assert.Contains("\"FechaIngreso\":\"05/10/2026\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("28,50", json);
            Assert.DoesNotContain("33,63", json);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culturaOriginal;
        }
    }

    [Fact]
    public void Serializacion_ServicioTecnico_JamasDebeIncluirCamposEliminadosEnV3()
    {
        var contexto = CrearContextoPrueba();
        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000300",
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            Cliente = new Cliente { NombreCompleto = "Cliente Prueba", NumeroDocumento = "71234567" },
            Vehiculo = new Vehiculo { Marca = "YAMAHA", Placa = "ABC-123", NumeroSerieVIN = "3VWFE21C04M000001" },
            Comprobantes = new List<Comprobante>
            {
                new() { Tipo = "Boleta", Serie = "B001", Numero = "00000001", Estado = "Emitido", FechaCreacion = DateTime.UtcNow }
            },
            Detalles = new List<DetalleServicio>
            {
                new() { TipoItem = TipoItemServicio.ManoDeObra, Descripcion = "Mano de obra", Cantidad = 1, PrecioUnitario = 100.00m, Total = 100.00m, SubtotalGravado = 84.75m, Activo = true }
            }
        };

        var loteRes = _mapper.MapearOrdenServicio(orden, contexto);
        Assert.True(loteRes.EsLoteCompletamenteValido);

        var json = YamahaJsonSerializer.SerializarServicioTecnico(loteRes.DtosProyectados);

        using var doc = JsonDocument.Parse(json);
        var primerElemento = doc.RootElement[0];

        // Verificar que los 5 campos extintos jamás aparezcan
        Assert.False(primerElemento.TryGetProperty("FechaApertura", out _));
        Assert.False(primerElemento.TryGetProperty("FechaCierre", out _));
        Assert.False(primerElemento.TryGetProperty("MOSinIGV", out _));
        Assert.False(primerElemento.TryGetProperty("RepuestosSinIGV", out _));
        Assert.False(primerElemento.TryGetProperty("IGV", out _));
    }

    [Fact]
    public void Serializacion_Stock_JamasDebeIncluirCamposEliminadosEnV3()
    {
        var contexto = CrearContextoPrueba();
        var producto = new Producto { Codigo = "SKU-001", Nombre = "Repuesto", Costo = 10.00m, StockActual = 5 };

        var res = _mapper.MapearProductoStock(producto, contexto);
        Assert.True(res.EsValido);

        var json = YamahaJsonSerializer.SerializarStock([res.Dto!]);

        using var doc = JsonDocument.Parse(json);
        var primerElemento = doc.RootElement[0];

        // Verificar que los campos extintos en Stock v3 no existan
        Assert.False(primerElemento.TryGetProperty("SitioDealer", out _));
        Assert.False(primerElemento.TryGetProperty("FechaVenta", out _));
    }
}
