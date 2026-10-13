using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Suite de pruebas unitarias para la primera entrega segura de la integración Yamaha Motor del Perú v3:
/// contratos JSON exactos, serialización determinista, validaciones previas al envío, Dry-Run sin efectos
/// secundarios y protección de credenciales (<c>Function_Key</c>) en logs.
/// </summary>
public class YamahaV3FirstDeliveryTests
{
    // =========================================================================
    // 1. Pruebas de Contratos JSON y Serialización (Secciones 3.2, 4, 5.3, 6.2 y 8)
    // =========================================================================

    [Fact]
    public void SerializarVentas_DebeIncluirExactamenteLos39CamposConNombresExactos()
    {
        var venta = CrearVentaSpValida();
        var json = YamahaJsonSerializer.SerializarVentas([venta]);

        using var doc = JsonDocument.Parse(json);
        var elemento = doc.RootElement[0];

        string[] camposEsperados =
        [
            "RazonSocial", "RUC", "CodigoPuntoVenta", "NombreComercialPuntoVenta",
            "VIN", "Modelo", "Color", "AnioModelo", "FechaFacturacion",
            "NumeroComprobante", "TipoDocumentoVenta", "TipoPago", "EntidadFinanciera",
            "MontoSinIGV", "MontoConIGV", "Canal", "LineaNegocio", "TipoPuntoVenta",
            "NumeroMotor", "Moneda", "PrecioFinal", "CodigoYamaha", "Descripcion",
            "CantidadVendida", "DescuentoPorcentaje", "DescuentoSoles", "PrecioUnitarioConIGV",
            "TipoCliente", "NombreCompletoCliente", "TipoDocumentoCliente", "NumeroDocumentoCliente",
            "CanalContactoCliente", "DistritoCliente", "CiudadCliente", "DepartamentoCliente",
            "DireccionCliente", "MotoActualCliente", "CelularCliente", "CorreoCliente"
        ];

        var propiedadesReales = elemento.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(39, propiedadesReales.Length);
        Assert.Equal(camposEsperados, propiedadesReales);

        // Para SP, AnioModelo no aplica y se serializa como cadena vacía según Sección 3.4
        Assert.Equal(JsonValueKind.String, elemento.GetProperty("AnioModelo").ValueKind);
        Assert.Equal(string.Empty, elemento.GetProperty("AnioModelo").GetString());
    }

    [Fact]
    public void SerializarStock_DebeIncluirExactamenteLos12CamposYExcluirSitioDealerYFechaVenta()
    {
        var stock = CrearStockSpValido();
        var json = YamahaJsonSerializer.SerializarStock([stock]);

        using var doc = JsonDocument.Parse(json);
        var elemento = doc.RootElement[0];

        string[] camposEsperados =
        [
            "RazonSocial", "RUC", "CodigoPuntoVenta", "NombreComercialPuntoVenta",
            "LineaNegocio", "CodigoYamaha", "Descripcion", "StockDisponible",
            "CostoVentaSinIGV", "CostoVentaConIGV", "FechaIngreso", "VIN"
        ];

        var propiedadesReales = elemento.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(12, propiedadesReales.Length);
        Assert.Equal(camposEsperados, propiedadesReales);

        // Campos eliminados en v3 (Sección 5.3) jamás deben existir
        Assert.False(elemento.TryGetProperty("SitioDealer", out _));
        Assert.False(elemento.TryGetProperty("FechaVenta", out _));
    }

    [Fact]
    public void SerializarServicioTecnico_DebeIncluirExactamenteLos30CamposYExcluirLos5CamposEliminadosEnV3()
    {
        var lineaSte = CrearLineaServicioTecnicoManoDeObraValida();
        var json = YamahaJsonSerializer.SerializarServicioTecnico([lineaSte]);

        using var doc = JsonDocument.Parse(json);
        var elemento = doc.RootElement[0];

        string[] camposEsperados =
        [
            "LineaNegocio", "SubLineaNegocio", "RazonSocial", "RUC",
            "NombreComercialPuntoVenta", "CodigoPuntoVenta", "NumeroOrdenTrabajo",
            "FechaFacturacion", "Estado", "TipoDocumentoCliente", "NumeroDocumentoCliente",
            "NombreCliente", "Celular", "Correo", "Placa", "Chasis", "Marca", "Modelo",
            "Version", "Kilometraje", "TipoServicio", "Actividad", "CodigoYamaha",
            "Descripcion", "Cantidad", "MontoSinIGV", "MontoConIGV", "PrecioUnit",
            "Descuento", "TotalConIGV"
        ];

        var propiedadesReales = elemento.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(30, propiedadesReales.Length);
        Assert.Equal(camposEsperados, propiedadesReales);

        // Campos eliminados en v3 (Sección 6.2) jamás deben existir
        Assert.False(elemento.TryGetProperty("FechaApertura", out _));
        Assert.False(elemento.TryGetProperty("FechaCierre", out _));
        Assert.False(elemento.TryGetProperty("MOSinIGV", out _));
        Assert.False(elemento.TryGetProperty("RepuestosSinIGV", out _));
        Assert.False(elemento.TryGetProperty("IGV", out _));
    }

    [Fact]
    public void SerializarDecimales_DebeUsarPuntoDecimalYDosDecimalesIndependientementeDeCulturaLocal()
    {
        var culturaOriginal = Thread.CurrentThread.CurrentCulture;
        try
        {
            // Simulamos una cultura con coma decimal (es-ES)
            Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");

            var stock = CrearStockSpValido() with
            {
                CostoVentaSinIGV = 850m,
                CostoVentaConIGV = 1003m
            };

            var json = YamahaJsonSerializer.SerializarStock([stock]);

            Assert.Contains("\"CostoVentaSinIGV\":850.00", json, StringComparison.Ordinal);
            Assert.Contains("\"CostoVentaConIGV\":1003.00", json, StringComparison.Ordinal);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culturaOriginal;
        }
    }

    // =========================================================================
    // 2. Pruebas de Validación — Servicio 1: Ventas (Sección 4)
    // =========================================================================

    [Fact]
    public void ValidarLoteVentas_RegistroSpValido_NoDebeGenerarErrores()
    {
        var venta = CrearVentaSpValida();
        var resultado = YamahaV3Validator.ValidarLoteVentas([venta]);

        Assert.True(resultado.EsLoteValido);
        Assert.Equal(0, resultado.TotalErroresBloqueantes);
        Assert.Equal(1, resultado.RegistrosValidos);
    }

    [Fact]
    public void ValidarLoteVentas_NotaDeCreditoConMontosYCantidadNegativos_DebeCumplirSeccion42()
    {
        var notaCredito = CrearVentaSpValida() with
        {
            TipoDocumentoVenta = YamahaV3Catalogos.DocumentoVentaNotaCredito,
            CantidadVendida = -2,
            MontoSinIGV = -3500.00m,
            MontoConIGV = -4130.00m,
            PrecioFinal = -4130.00m
        };

        var resultado = YamahaV3Validator.ValidarLoteVentas([notaCredito]);

        Assert.True(resultado.EsLoteValido);
        Assert.Equal(0, resultado.TotalErroresBloqueantes);
    }

    [Fact]
    public void ValidarLoteVentas_NotaDeCreditoConValoresPositivos_DebeReportarLos4ErroresBloqueantes()
    {
        var notaCreditoInvalida = CrearVentaSpValida() with
        {
            TipoDocumentoVenta = YamahaV3Catalogos.DocumentoVentaNotaCredito,
            CantidadVendida = 2,
            MontoSinIGV = 3500.00m,
            MontoConIGV = 4130.00m,
            PrecioFinal = 4130.00m
        };

        var resultado = YamahaV3Validator.ValidarLoteVentas([notaCreditoInvalida]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-V-NC-001" && h.Campo == "MontoSinIGV");
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-V-NC-002" && h.Campo == "MontoConIGV");
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-V-NC-003" && h.Campo == "PrecioFinal");
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-V-NC-004" && h.Campo == "CantidadVendida");
    }

    [Fact]
    public void ValidarLoteVentas_TipoPagoContadoConEntidadDistintaDeContado_DebeRechazarPorSeccion41()
    {
        var ventaInvalida = CrearVentaSpValida() with
        {
            TipoPago = "CONTADO",
            EntidadFinanciera = "BCP"
        };

        var resultado = YamahaV3Validator.ValidarLoteVentas([ventaInvalida]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-V-PAGO-003" && h.Campo == "EntidadFinanciera");
    }

    [Fact]
    public void ValidarLoteVentas_BoletaSinTextoCompletoBoletaDeVenta_DebeRechazarPorSeccion43()
    {
        var ventaInvalida = CrearVentaSpValida() with
        {
            TipoDocumentoVenta = "BOLETA"
        };

        var resultado = YamahaV3Validator.ValidarLoteVentas([ventaInvalida]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-V-TDOCV-002" && h.Campo == "TipoDocumentoVenta");
    }

    // =========================================================================
    // 3. Pruebas de Validación — Servicio 2: Stock (Sección 5)
    // =========================================================================

    [Fact]
    public void ValidarLoteStock_RepuestoSpConStockCero_EsValidoSegunSeccion52()
    {
        var repuestoCero = CrearStockSpValido() with
        {
            LineaNegocio = "SP",
            StockDisponible = 0,
            VIN = string.Empty
        };

        var resultado = YamahaV3Validator.ValidarLoteStock([repuestoCero]);

        Assert.True(resultado.EsLoteValido);
        Assert.Equal(0, resultado.TotalErroresBloqueantes);
    }

    [Theory]
    [InlineData("MC")]
    [InlineData("ATV")]
    [InlineData("OM")]
    public void ValidarLoteStock_UnidadConStockCero_DebeRechazarSegunSeccion52(string lineaNegocio)
    {
        var unidadSinStock = CrearStockSpValido() with
        {
            LineaNegocio = lineaNegocio,
            StockDisponible = 0,
            VIN = "3VWFE21C04M000001"
        };

        var resultado = YamahaV3Validator.ValidarLoteStock([unidadSinStock]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-S-STOCK-001" && h.Campo == "StockDisponible");
    }

    [Fact]
    public void ValidarLoteStock_VinIgualACodigoYamaha_DebeRechazarSegunSeccion8()
    {
        var duplicado = CrearStockSpValido() with
        {
            LineaNegocio = "MC",
            StockDisponible = 1,
            CodigoYamaha = "3VWFE21C04M000001",
            VIN = "3VWFE21C04M000001"
        };

        var resultado = YamahaV3Validator.ValidarLoteStock([duplicado]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-S-VIN-005" && h.Campo == "VIN");
    }

    // =========================================================================
    // 4. Pruebas de Validación — Servicio 3: Servicio Técnico (Sección 6)
    // =========================================================================

    [Fact]
    public void ValidarLoteServicioTecnico_MultiplesLineasConCabeceraConsistente_DebeSerValido()
    {
        var lineaRepuesto = CrearLineaServicioTecnicoRepuestoValida();
        var lineaManoObra = CrearLineaServicioTecnicoManoDeObraValida();

        var resultado = YamahaV3Validator.ValidarLoteServicioTecnico([lineaRepuesto, lineaManoObra]);

        Assert.True(resultado.EsLoteValido);
        Assert.Equal(0, resultado.TotalErroresBloqueantes);
        Assert.Equal(2, resultado.RegistrosValidos);
    }

    [Fact]
    public void ValidarLoteServicioTecnico_InconsistenciaDeCabeceraEnMismaOrden_DebeRechazarPorSeccion61()
    {
        var linea1 = CrearLineaServicioTecnicoRepuestoValida();
        var linea2 = CrearLineaServicioTecnicoManoDeObraValida() with
        {
            Placa = "XYZ-999",
            FechaFacturacion = "16/05/2025"
        };

        var resultado = YamahaV3Validator.ValidarLoteServicioTecnico([linea1, linea2]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-ST-CONSISTENCIA-001" && h.Campo == "Placa");
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-ST-CONSISTENCIA-001" && h.Campo == "FechaFacturacion");
    }

    [Fact]
    public void ValidarLoteServicioTecnico_PrefijoOsEnVezDeOt_DebeRechazarPorSeccion62()
    {
        var ordenConOs = CrearLineaServicioTecnicoManoDeObraValida() with
        {
            NumeroOrdenTrabajo = "OS-000045"
        };

        var resultado = YamahaV3Validator.ValidarLoteServicioTecnico([ordenConOs]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-ST-OT-002" && h.Campo == "NumeroOrdenTrabajo");
    }

    [Theory]
    [InlineData("--")]
    [InlineData("ABC 123")]
    [InlineData("ABC123")]
    [InlineData("A-1")]
    public void ValidarLoteServicioTecnico_PlacaInvalida_DebeRechazarPorSeccion62(string placaInvalida)
    {
        var linea = CrearLineaServicioTecnicoManoDeObraValida() with
        {
            Placa = placaInvalida
        };

        var resultado = YamahaV3Validator.ValidarLoteServicioTecnico([linea]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.Campo == "Placa" && h.Severidad == SeveridadValidacionYamaha.Error);
    }

    [Fact]
    public void ValidarLoteServicioTecnico_TipoServicioGuionEnManoDeObraSte_DebeRechazarPorSeccion63()
    {
        var lineaSteInvalida = CrearLineaServicioTecnicoManoDeObraValida() with
        {
            SubLineaNegocio = "STE",
            TipoServicio = "-"
        };

        var resultado = YamahaV3Validator.ValidarLoteServicioTecnico([lineaSteInvalida]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-ST-TSERV-003" && h.Campo == "TipoServicio");
    }

    // =========================================================================
    // 5. Pruebas de Modo Dry-Run (Sin Persistencia, Sin Envío y Sin Alterar Datos)
    // =========================================================================

    [Fact]
    public void SimularLoteServicioTecnico_NoDebeModificarDatosOriginalesNiMarcarComoEnviado()
    {
        var dryRunService = new YamahaDryRunService();
        var registroOriginal = CrearLineaServicioTecnicoManoDeObraValida() with
        {
            NumeroOrdenTrabajo = "OS-000123"
        };

        var resultado = dryRunService.SimularLoteServicioTecnico([registroOriginal]);

        Assert.True(resultado.EsSimulacionDryRun);
        Assert.False(resultado.EnviadoAYamaha);
        Assert.False(resultado.PersistidoEnBaseDeDatos);
        Assert.Equal(YamahaDryRunService.EstadoRechazadoLocalmente, resultado.EstadoSimulacion);
        Assert.NotEmpty(resultado.DecisionesPendientes);
        Assert.Contains(resultado.DecisionesPendientes, d => d.CodigoDecision == "D-08");

        // Verifica que el objeto original no fue mutado para forzar el paso de la validación
        Assert.Equal("OS-000123", resultado.RegistrosEvaluados[0].RegistroOriginalSinAlterar.NumeroOrdenTrabajo);
        Assert.False(resultado.RegistrosEvaluados[0].EsValidoLocalmente);
    }

    // =========================================================================
    // 6. Pruebas de Seguridad — Cero Exposición de Function_Key en Logs y Excepciones
    // =========================================================================

    [Fact]
    public void SanitizarUri_ConFunctionKeyEnQueryString_DebeOcultarElSecreto()
    {
        const string functionKeySecreta = "AzFuncSecretKey_9988776655_Confidencial";
        var uri = new Uri($"https://yamaha-data-api-gwcjcgabesdzc8fw.brazilsouth01.azurewebsites.net/api/ReceiveRetailVentas?code={functionKeySecreta}");

        var sanitizada = YamahaSecurityHelper.SanitizarUri(uri);

        Assert.DoesNotContain(functionKeySecreta, sanitizada, StringComparison.Ordinal);
        Assert.Contains($"code={YamahaSecurityHelper.MascaraSecreto}", sanitizada, StringComparison.Ordinal);
    }

    [Fact]
    public async Task YamahaApiClient_AnteErrorHttpOTimeout_NuncaDebeEscribirFunctionKeyEnLogsNiEnExcepcion()
    {
        const string functionKeySecreta = "ClaveSecretaYamahaProd_2026_NoExponer";
        var urlConSecreto = $"https://yamaha-data-api-gwcjcgabesdzc8fw.brazilsouth01.azurewebsites.net/api/ReceiveRetailStock?code={functionKeySecreta}";

        var logger = new InMemoryTestLogger<YamahaApiClient>();
        var settings = Options.Create(new YamahaSettings
        {
            FunctionKey = functionKeySecreta
        });

        // Caso 1: Respuesta HTTP 401 Unauthorized cuyo body también repite la clave
        var handlerErrorHttp = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent($"Invalid key: {functionKeySecreta}", Encoding.UTF8, "text/plain")
            }));

        using var httpClient1 = new HttpClient(handlerErrorHttp);
        var apiClient1 = new YamahaApiClient(httpClient1, logger, settings);

        using var request1 = new HttpRequestMessage(HttpMethod.Post, urlConSecreto);
        var response = await apiClient1.SendAsync(request1, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEmpty(logger.Mensajes);
        Assert.All(logger.Mensajes, msg => Assert.DoesNotContain(functionKeySecreta, msg, StringComparison.Ordinal));

        // Caso 2: Timeout de red (OperationCanceledException interna)
        var handlerTimeout = new StubHttpMessageHandler((_, _) =>
            throw new TaskCanceledException("Simulated HTTP timeout"));

        using var httpClient2 = new HttpClient(handlerTimeout);
        var apiClient2 = new YamahaApiClient(httpClient2, logger, settings);

        using var request2 = new HttpRequestMessage(HttpMethod.Post, urlConSecreto);
        var timeoutEx = await Assert.ThrowsAsync<TimeoutException>(() =>
            apiClient2.SendAsync(request2, CancellationToken.None));

        Assert.DoesNotContain(functionKeySecreta, timeoutEx.Message, StringComparison.Ordinal);
        Assert.All(logger.Mensajes, msg => Assert.DoesNotContain(functionKeySecreta, msg, StringComparison.Ordinal));

        // Caso 3: HttpRequestException cuyo mensaje incluye la URL cruda con ?code=...
        var handlerNetworkEx = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException($"Connection refused while calling {urlConSecreto}"));

        using var httpClient3 = new HttpClient(handlerNetworkEx);
        var apiClient3 = new YamahaApiClient(httpClient3, logger, settings);

        using var request3 = new HttpRequestMessage(HttpMethod.Post, urlConSecreto);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            apiClient3.SendAsync(request3, CancellationToken.None));

        Assert.All(logger.Mensajes, msg => Assert.DoesNotContain(functionKeySecreta, msg, StringComparison.Ordinal));
    }

    [Fact]
    public void SanitizarTexto_ConDelimitadoresUrlEncoded_DebeOcultarFunctionKey()
    {
        const string functionKeySecreta = "SecretKey_UrlEncoded_778899";
        var textoLog = $"Error calling https://api.yamaha.pe/ReceiveRetailVentas%3Fcode%3D{functionKeySecreta}%26sig%3DOtraFirma999";

        var sanitizado = YamahaSecurityHelper.SanitizarTexto(textoLog);

        Assert.DoesNotContain(functionKeySecreta, sanitizado, StringComparison.Ordinal);
        Assert.DoesNotContain("OtraFirma999", sanitizado, StringComparison.Ordinal);
        Assert.Contains(YamahaSecurityHelper.MascaraSecreto, sanitizado, StringComparison.Ordinal);
    }

    // =========================================================================
    // 7. Pruebas Negativas Adicionales (Serialización, Precisión Decimal, Nulos y Permisos)
    // =========================================================================

    [Fact]
    public void SerializarStringsNulos_NuncaDebeEmitirNullEnJsonSinoCadenaVacia()
    {
        var ventaConNulos = CrearVentaSpValida() with
        {
            VIN = null!,
            Modelo = null!,
            Color = null!,
            DistritoCliente = null!
        };

        var json = YamahaJsonSerializer.SerializarVentas([ventaConNulos]);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement[0];

        Assert.Equal(JsonValueKind.String, root.GetProperty("VIN").ValueKind);
        Assert.Equal(string.Empty, root.GetProperty("VIN").GetString());
        Assert.Equal(JsonValueKind.String, root.GetProperty("DistritoCliente").ValueKind);
        Assert.Equal(string.Empty, root.GetProperty("DistritoCliente").GetString());
    }

    [Fact]
    public void DeserializarDecimalInvalido_DebeLanzarJsonException()
    {
        const string jsonInvalido = """[{"RazonSocial":"TB","RUC":"20601234567","MontoSinIGV":"NO-NUMERICO"}]""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<List<YamahaVentaV3Dto>>(jsonInvalido, YamahaJsonSerializer.Options));
    }

    [Fact]
    public void ValidarLotes_ConLoteVacioOElementoNulo_NoDebeLanzarNullReferenceExceptionYDebeMarcarInvalido()
    {
        var dryRun = new YamahaDryRunService();

        // Lote vacío []
        var vacio = dryRun.SimularLoteVentas([]);
        Assert.False(vacio.ResumenValidacion.EsLoteValido);
        Assert.Contains(vacio.ResumenValidacion.Hallazgos, h => h.CodigoRegla == "YAM-GEN-LOTE-001");

        // Lote con elemento null [null]
        var conNulo = dryRun.SimularLoteVentas([null!]);
        Assert.False(conNulo.ResumenValidacion.EsLoteValido);
        Assert.Single(conNulo.RegistrosEvaluados);
        Assert.False(conNulo.RegistrosEvaluados[0].EsValidoLocalmente);
        Assert.Contains(conNulo.ResumenValidacion.Hallazgos, h => h.CodigoRegla == "YAM-GEN-NULL-001");
    }

    [Fact]
    public void ValidarLoteVentas_DecimalConMasDeDosDecimalesOExcesoDeRangoDecimal10_2_DebeRechazar()
    {
        var ventaInvalida = CrearVentaSpValida() with
        {
            MontoSinIGV = 100.126m,            // Más de 2 decimales
            MontoConIGV = 100_000_000.00m      // Excede DECIMAL(10,2) > 99,999,999.99
        };

        var resultado = YamahaV3Validator.ValidarLoteVentas([ventaInvalida]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-GEN-DEC-ESCALA" && h.Campo == "MontoSinIGV");
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-GEN-DEC-10-2" && h.Campo == "MontoConIGV");
    }

    [Fact]
    public void ValidarLoteVentas_DocumentoExtranjeroSinTipoDocNoDomiciliadoExplicito_DebeRechazarPorSeccion44()
    {
        var ventaExtranjeroSinTipo = CrearVentaSpValida() with
        {
            TipoDocumentoCliente = string.Empty,
            NumeroDocumentoCliente = "PASS-99887766" // 13 caracteres (no es 8, 9 ni 11)
        };

        var resultado = YamahaV3Validator.ValidarLoteVentas([ventaExtranjeroSinTipo]);

        Assert.False(resultado.EsLoteValido);
        Assert.Contains(resultado.Hallazgos, h => h.CodigoRegla == "YAM-V-DOCCLI-006" && h.Campo == "TipoDocumentoCliente");
    }

    [Fact]
    public async Task EjecutarDryRunAsync_SeguridadYPermisos_DebeRechazarNoAutenticadoSinPermisoYRolCliente()
    {
        var dryRunService = new YamahaDryRunService();
        var lote = new[] { CrearVentaSpValida() };

        // 1. Usuario no autenticado -> 401 Unauthorized
        var anonimo = new ClaimsPrincipal(new ClaimsIdentity());
        var resAnonimo = await YamahaEndpoints.EjecutarDryRunAsync(
            lote,
            anonimo,
            dbContext: null,
            r => dryRunService.SimularLoteVentas(r));
        Assert.IsType<UnauthorizedHttpResult>(resAnonimo);

        // 2. Usuario Staff autenticado pero SIN permiso configuracion.editar -> 403 Forbid
        var vendedorSinPermiso = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, RolesDefinidos.Vendedor),
            new Claim("permission", PermisosDefinidos.VentasVer)
        ], "TestAuth"));
        var resSinPermiso = await YamahaEndpoints.EjecutarDryRunAsync(
            lote,
            vendedorSinPermiso,
            dbContext: null,
            r => dryRunService.SimularLoteVentas(r));
        Assert.IsType<ForbidHttpResult>(resSinPermiso);

        // 3. Usuario con rol Cliente (incluso si tuviera claim inyectado) -> 403 Forbid
        var usuarioCliente = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, RolesDefinidos.Cliente),
            new Claim("permission", PermisosDefinidos.ConfiguracionEditar)
        ], "TestAuth"));
        var resCliente = await YamahaEndpoints.EjecutarDryRunAsync(
            lote,
            usuarioCliente,
            dbContext: null,
            r => dryRunService.SimularLoteVentas(r));
        Assert.IsType<ForbidHttpResult>(resCliente);

        // 4. Usuario Gerencia/Admin con permiso configuracion.editar -> 200 OK
        var adminAutorizado = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, RolesDefinidos.GerenciaAdmin),
            new Claim("permission", PermisosDefinidos.ConfiguracionEditar)
        ], "TestAuth"));
        var resOk = await YamahaEndpoints.EjecutarDryRunAsync(
            lote,
            adminAutorizado,
            dbContext: null,
            r => dryRunService.SimularLoteVentas(r));
        var okResult = Assert.IsType<Ok<YamahaDryRunResultadoDto<YamahaVentaV3Dto>>>(resOk);
        Assert.NotNull(okResult.Value);
        Assert.True(okResult.Value.EsSimulacionDryRun);
        Assert.False(okResult.Value.EnviadoAYamaha);
        Assert.False(okResult.Value.PersistidoEnBaseDeDatos);

        // 5. Payload nulo -> 400 BadRequest
        var resNullPayload = await YamahaEndpoints.EjecutarDryRunAsync<YamahaVentaV3Dto>(
            registros: null,
            adminAutorizado,
            dbContext: null,
            r => dryRunService.SimularLoteVentas(r));
        Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(resNullPayload);
        Assert.Equal(400, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)resNullPayload).StatusCode);
    }

    // =========================================================================
    // Helpers de datos de prueba
    // =========================================================================

    private static YamahaVentaV3Dto CrearVentaSpValida() => new()
    {
        RazonSocial = "Team Benavides S.R.L.",
        RUC = "20601234567",
        CodigoPuntoVenta = "PV-AQP-01",
        NombreComercialPuntoVenta = "Team Benavides - Arequipa",
        VIN = string.Empty,
        Modelo = string.Empty,
        Color = string.Empty,
        AnioModelo = null,
        FechaFacturacion = "15/05/2025",
        NumeroComprobante = "B001-000123",
        TipoDocumentoVenta = "BOLETA DE VENTA",
        TipoPago = "CONTADO",
        EntidadFinanciera = "CONTADO",
        MontoSinIGV = 100.00m,
        MontoConIGV = 118.00m,
        Canal = "MOSTRADOR",
        LineaNegocio = "SP",
        TipoPuntoVenta = "3S",
        NumeroMotor = string.Empty,
        Moneda = "PEN",
        PrecioFinal = 118.00m,
        CodigoYamaha = "688-44352-03",
        Descripcion = "Filtro de aceite YZF",
        CantidadVendida = 1,
        DescuentoPorcentaje = 0.00m,
        DescuentoSoles = 0.00m,
        PrecioUnitarioConIGV = 118.00m,
        TipoCliente = "CLIENTE FINAL",
        NombreCompletoCliente = "Juan Pérez López",
        TipoDocumentoCliente = "DNI",
        NumeroDocumentoCliente = "45678912",
        CanalContactoCliente = "PRESENCIAL",
        DistritoCliente = string.Empty,
        CiudadCliente = string.Empty,
        DepartamentoCliente = string.Empty,
        DireccionCliente = "Av. Ejército 500",
        MotoActualCliente = string.Empty,
        CelularCliente = "987654321",
        CorreoCliente = "juan.perez@email.com"
    };

    private static YamahaStockV3Dto CrearStockSpValido() => new()
    {
        RazonSocial = "Team Benavides S.R.L.",
        RUC = "20601234567",
        CodigoPuntoVenta = "PV-AQP-01",
        NombreComercialPuntoVenta = "Team Benavides - Arequipa",
        LineaNegocio = "SP",
        CodigoYamaha = "688-44352-03",
        Descripcion = "Filtro de aceite YZF",
        StockDisponible = 15,
        CostoVentaSinIGV = 850.00m,
        CostoVentaConIGV = 1003.00m,
        FechaIngreso = "10/05/2025",
        VIN = string.Empty
    };

    private static YamahaServicioTecnicoV3Dto CrearLineaServicioTecnicoRepuestoValida() => new()
    {
        LineaNegocio = "STE",
        SubLineaNegocio = "SP",
        RazonSocial = "Team Benavides S.R.L.",
        RUC = "20601234567",
        NombreComercialPuntoVenta = "Team Benavides - Arequipa",
        CodigoPuntoVenta = "PV-AQP-01",
        NumeroOrdenTrabajo = "OT-2026-001",
        FechaFacturacion = "15/05/2025",
        Estado = "FACTURADO",
        TipoDocumentoCliente = "DNI",
        NumeroDocumentoCliente = "45678912",
        NombreCliente = "Juan Pérez López",
        Celular = "987654321",
        Correo = "juan.perez@email.com",
        Placa = "ABC-123",
        Chasis = "3VWFE21C04M000001",
        Marca = "YAMAHA",
        Modelo = "YBR 125",
        Version = "STD",
        Kilometraje = 10122,
        TipoServicio = "MP",
        Actividad = "SERV. MANTENIMIENTO 10,000 KM",
        CodigoYamaha = "688-44352-03",
        Descripcion = "Filtro de aceite YZF",
        Cantidad = 1,
        MontoSinIGV = 50.00m,
        MontoConIGV = 59.00m,
        PrecioUnit = 50.00m,
        Descuento = 0.00m,
        TotalConIGV = 59.00m
    };

    private static YamahaServicioTecnicoV3Dto CrearLineaServicioTecnicoManoDeObraValida() => new()
    {
        LineaNegocio = "STE",
        SubLineaNegocio = "STE",
        RazonSocial = "Team Benavides S.R.L.",
        RUC = "20601234567",
        NombreComercialPuntoVenta = "Team Benavides - Arequipa",
        CodigoPuntoVenta = "PV-AQP-01",
        NumeroOrdenTrabajo = "OT-2026-001",
        FechaFacturacion = "15/05/2025",
        Estado = "FACTURADO",
        TipoDocumentoCliente = "DNI",
        NumeroDocumentoCliente = "45678912",
        NombreCliente = "Juan Pérez López",
        Celular = "987654321",
        Correo = "juan.perez@email.com",
        Placa = "ABC-123",
        Chasis = "3VWFE21C04M000001",
        Marca = "YAMAHA",
        Modelo = "YBR 125",
        Version = "STD",
        Kilometraje = 10122,
        TipoServicio = "MP",
        Actividad = "SERV. MANTENIMIENTO 10,000 KM",
        CodigoYamaha = string.Empty,
        Descripcion = string.Empty,
        Cantidad = null,
        MontoSinIGV = 100.00m,
        MontoConIGV = 118.00m,
        PrecioUnit = null,
        Descuento = 0.00m,
        TotalConIGV = 118.00m
    };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _responder(request, cancellationToken);
    }

    private sealed class InMemoryTestLogger<T> : ILogger<T>
    {
        public List<string> Mensajes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Mensajes.Add(formatter(state, exception));
            if (exception is not null)
            {
                Mensajes.Add(exception.ToString());
            }
        }
    }
}
