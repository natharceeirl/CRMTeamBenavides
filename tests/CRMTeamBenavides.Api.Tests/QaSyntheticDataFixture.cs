using CRMTeamBenavides.Api.Configuration;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Data.Seed;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Fixture controlado de datos sintéticos para pruebas automatizadas y validación de QA.
/// - NUNCA se ejecuta automáticamente al iniciar la aplicación (vive únicamente en el proyecto de pruebas).
/// - Incluye una guarda estricta (QA-FIN-MED-04) que bloquea su ejecución contra bases relacionales
///   de producción (crm_teambenavides), QA compartido (crm_teambenavides_qa) o cualquier base
///   relacional que no tenga prefijo explícito de pruebas aisladas (crm_qa_isolated_*) con autorización.
/// - Genera hashes PBKDF2 reales de ASP.NET Core Identity con una contraseña sintética fuerte.
/// - Es idempotente y libre de PII real.
/// </summary>
public static class QaSyntheticDataFixture
{
    public const string PrefijoBaseRelacionalAisladaAutorizada = "crm_qa_isolated_";
    public const string ClaveSinteticaQa = "Qa.Sintetico#2026!Segura9";

    public const string EmailGerenciaQa = "gerencia.qa@sintetico.teambenavides.test";
    public const string EmailRecepcionQa = "recepcion.qa@sintetico.teambenavides.test";
    public const string EmailTecnicoQa = "tecnico.qa@sintetico.teambenavides.test";
    public const string EmailVendedorQa = "vendedor.qa@sintetico.teambenavides.test";

    public const string TelefonoClienteUnoE164 = "+51900000101";
    public const string TelefonoClienteDosE164 = "+51900000102";

    public const string DniClienteUno = "00000101";
    public const string RucClienteDos = "20000000101";

    public const string SkuProductoLubricante = "SKU-QA-LUB-001";
    public const string SkuProductoFiltro = "SKU-QA-FIL-002";

    public const string PlacaVehiculoUno = "QA-1001";
    public const string PlacaVehiculoDos = "QA-1002";

    public const string NumeroOrdenQa = "OS-QA-000001";

    public const string ExternalIdMensajeEntranteBot = "wamid.qa.synth.in.001";
    public const string ExternalIdMensajeSalienteMock = "wamid.qa.mock.out.001";
    public const string ExternalIdMensajeEntranteHumano = "wamid.qa.synth.in.002";

    private static readonly PasswordHasher<Usuario> HasherReal = new();

    public sealed record ResultadoSeedSinteticoQa(
        Guid GerenciaUsuarioId,
        Guid RecepcionUsuarioId,
        Guid TecnicoUsuarioId,
        Guid VendedorUsuarioId,
        Guid ClienteUnoId,
        Guid ClienteDosId,
        Guid ProductoLubricanteId,
        Guid ProductoFiltroId,
        Guid VehiculoUnoId,
        Guid VehiculoDosId,
        Guid OrdenServicioId,
        Guid VentaId,
        Guid ConversacionBotId,
        Guid ConversacionHumanaId,
        Guid MensajeEntranteBotId,
        Guid MensajeSalienteMockId,
        Guid MensajeEntranteHumanoId);

    /// <summary>
    /// Verifica que el contexto apunte a una base InMemory o a una base PostgreSQL efímera
    /// explícitamente autorizada con prefijo <c>crm_qa_isolated_</c>.
    /// Rechaza categóricamente <c>crm_teambenavides</c> (producción) y <c>crm_teambenavides_qa</c> (QA compartido).
    /// </summary>
    public static void ValidarDestinoAutorizado(
        ApplicationDbContext db,
        bool autorizarBaseRelacionalAisladaQa = false)
    {
        ArgumentNullException.ThrowIfNull(db);

        var providerName = db.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!autorizarBaseRelacionalAisladaQa)
        {
            throw new InvalidOperationException(
                "Operación bloqueada: QaSyntheticDataFixture no puede ejecutarse sobre un proveedor relacional " +
                "sin autorización explícita (autorizarBaseRelacionalAisladaQa: true).");
        }

        var connString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connString))
        {
            throw new InvalidOperationException(
                "Operación bloqueada: no se pudo determinar la base de datos relacional de destino.");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connString);
        }
        catch (Exception)
        {
            throw new InvalidOperationException(
                "Operación bloqueada: cadena de conexión relacional inválida para QaSyntheticDataFixture.");
        }

        var dbName = builder.Database?.Trim() ?? string.Empty;
        if (string.Equals(dbName, ValidacionConfiguracionEntorno.BaseDatosProduccion, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dbName, ValidacionConfiguracionEntorno.BaseDatosQaEsperada, StringComparison.OrdinalIgnoreCase) ||
            !dbName.StartsWith(PrefijoBaseRelacionalAisladaAutorizada, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Operación bloqueada: QaSyntheticDataFixture rechazó la base relacional «{dbName}». " +
                $"Solo se permiten bases efímeras aisladas con prefijo «{PrefijoBaseRelacionalAisladaAutorizada}» " +
                "(nunca producción ni crm_teambenavides_qa compartida).");
        }
    }

    public static async Task<ResultadoSeedSinteticoQa> SembrarAsync(
        ApplicationDbContext db,
        UserManager<Usuario>? userManager = null,
        bool autorizarBaseRelacionalAisladaQa = false,
        CancellationToken ct = default)
    {
        ValidarDestinoAutorizado(db, autorizarBaseRelacionalAisladaQa);

        await RolSeeder.SeedAsync(db);
        await MetodosPagoSeeder.SeedAsync(db);
        await FaqSeeder.SeedAsync(db);

        var gerencia = await AsegurarUsuarioConRolAsync(
            db, userManager, EmailGerenciaQa, "Gerencia Sintética QA", RolesDefinidos.GerenciaAdmin, ct);
        var recepcion = await AsegurarUsuarioConRolAsync(
            db, userManager, EmailRecepcionQa, "Recepción Sintética QA", RolesDefinidos.Recepcion, ct);
        var tecnico = await AsegurarUsuarioConRolAsync(
            db, userManager, EmailTecnicoQa, "Técnico Sintético QA", RolesDefinidos.Tecnico, ct);
        var vendedor = await AsegurarUsuarioConRolAsync(
            db, userManager, EmailVendedorQa, "Vendedor Sintético QA", RolesDefinidos.Vendedor, ct);

        var clienteUno = await db.Clientes.FirstOrDefaultAsync(c => c.NumeroDocumento == DniClienteUno, ct);
        if (clienteUno is null)
        {
            clienteUno = new Cliente
            {
                NombreCompleto = "Cliente Sintético QA Uno",
                TipoDocumento = TipoDocumentoCliente.DNI,
                NumeroDocumento = DniClienteUno,
                Telefono = TelefonoClienteUnoE164,
                Email = "cliente1.qa@sintetico.teambenavides.test",
                Direccion = "Av. Ficticia QA 101 - Entorno Pruebas",
                Activo = true
            };
            db.Clientes.Add(clienteUno);
        }

        var clienteDos = await db.Clientes.FirstOrDefaultAsync(c => c.NumeroDocumento == RucClienteDos, ct);
        if (clienteDos is null)
        {
            clienteDos = new Cliente
            {
                NombreCompleto = "Empresa Sintética QA Dos SAC",
                TipoDocumento = TipoDocumentoCliente.RUC,
                NumeroDocumento = RucClienteDos,
                Telefono = TelefonoClienteDosE164,
                Email = "cliente2.qa@sintetico.teambenavides.test",
                Direccion = "Jr. Simulación QA 202 - Entorno Pruebas",
                Activo = true
            };
            db.Clientes.Add(clienteDos);
        }
        await db.SaveChangesAsync(ct);

        var categoria = await db.CategoriasProducto.FirstOrDefaultAsync(c => c.Nombre == "Lubricantes Sintéticos QA", ct);
        if (categoria is null)
        {
            categoria = new CategoriaProducto
            {
                Nombre = "Lubricantes Sintéticos QA",
                StockMinimoDefault = 4,
                Activo = true
            };
            db.CategoriasProducto.Add(categoria);
            await db.SaveChangesAsync(ct);
        }

        var prodLubricante = await db.Productos.FirstOrDefaultAsync(p => p.Codigo == SkuProductoLubricante, ct);
        if (prodLubricante is null)
        {
            prodLubricante = new Producto
            {
                CategoriaId = categoria.Id,
                Codigo = SkuProductoLubricante,
                Nombre = "Aceite Sintético QA 10W-40 1L",
                Descripcion = "Producto sintético exclusivo para pruebas funcionales en QA",
                Unidad = "unidad",
                Marca = "Yamalube QA",
                PrecioVenta = 45.00m,
                MonedaPrecio = MonedaPrecio.PEN,
                PrecioIncluyeIgv = true,
                Costo = 28.00m,
                StockActual = 25,
                StockMinimo = 5,
                Tipo = TipoProducto.Repuesto,
                Activo = true
            };
            db.Productos.Add(prodLubricante);
        }

        var prodFiltro = await db.Productos.FirstOrDefaultAsync(p => p.Codigo == SkuProductoFiltro, ct);
        if (prodFiltro is null)
        {
            prodFiltro = new Producto
            {
                CategoriaId = categoria.Id,
                Codigo = SkuProductoFiltro,
                Nombre = "Filtro de Aceite Sintético QA FZ-25",
                Descripcion = "Filtro sintético exclusivo para pruebas funcionales en QA",
                Unidad = "unidad",
                Marca = "Yamaha QA",
                PrecioVenta = 35.00m,
                MonedaPrecio = MonedaPrecio.PEN,
                PrecioIncluyeIgv = true,
                Costo = 20.00m,
                StockActual = 15,
                StockMinimo = 4,
                Tipo = TipoProducto.Repuesto,
                Activo = true
            };
            db.Productos.Add(prodFiltro);
        }
        await db.SaveChangesAsync(ct);

        var vehiculoUno = await db.Vehiculos.FirstOrDefaultAsync(v => v.Placa == PlacaVehiculoUno, ct);
        if (vehiculoUno is null)
        {
            vehiculoUno = new Vehiculo
            {
                ClienteId = clienteUno.Id,
                Marca = "Yamaha",
                Modelo = "FZ-25",
                Placa = PlacaVehiculoUno,
                NumeroSerieVIN = "SYNTHQA0000001001",
                Anio = 2025,
                Color = "Azul QA",
                Kilometraje = 1500,
                Activo = true
            };
            db.Vehiculos.Add(vehiculoUno);
        }

        var vehiculoDos = await db.Vehiculos.FirstOrDefaultAsync(v => v.Placa == PlacaVehiculoDos, ct);
        if (vehiculoDos is null)
        {
            vehiculoDos = new Vehiculo
            {
                ClienteId = clienteDos.Id,
                Marca = "Yamaha",
                Modelo = "MT-03",
                Placa = PlacaVehiculoDos,
                NumeroSerieVIN = "SYNTHQA0000001002",
                Anio = 2025,
                Color = "Negro QA",
                Kilometraje = 3200,
                Activo = true
            };
            db.Vehiculos.Add(vehiculoDos);
        }
        await db.SaveChangesAsync(ct);

        var orden = await db.OrdenesServicio.FirstOrDefaultAsync(o => o.NumeroOrden == NumeroOrdenQa, ct);
        if (orden is null)
        {
            orden = new OrdenServicio
            {
                NumeroOrden = NumeroOrdenQa,
                ClienteId = clienteUno.Id,
                VehiculoId = vehiculoUno.Id,
                TecnicoAsignadoId = tecnico.Id,
                Estado = EstadoOrdenServicio.EnProceso,
                MotivoFalla = "Mantenimiento preventivo sintético QA 1,500 km",
                Diagnostico = "Unidad de prueba QA en óptimas condiciones",
                SubtotalGravado = 152.54m,
                MontoIgv = 27.46m,
                Total = 180.00m,
                FechaIngreso = DateTime.UtcNow.AddHours(-4),
                Activo = true
            };
            db.OrdenesServicio.Add(orden);
            await db.SaveChangesAsync(ct);
        }

        var venta = await db.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.ClienteId == clienteUno.Id && v.Total == 80.00m, ct);
        if (venta is null)
        {
            venta = new Venta
            {
                ClienteId = clienteUno.Id,
                Estado = EstadoVenta.Confirmada,
                Fecha = DateTime.UtcNow.AddHours(-2),
                Declarada = false,
                ReportarAYamaha = false,
                SubtotalGravado = 67.80m,
                MontoIgv = 12.20m,
                Total = 80.00m,
                Activo = true,
                Detalles =
                [
                    new DetalleVenta
                    {
                        ProductoId = prodLubricante.Id,
                        Cantidad = 1,
                        PrecioUnitario = 45.00m,
                        PrecioUnitarioConIgv = 45.00m,
                        Total = 45.00m,
                        TipoAfectacionIgv = TipoAfectacionIgv.Gravado,
                        Activo = true
                    },
                    new DetalleVenta
                    {
                        ProductoId = prodFiltro.Id,
                        Cantidad = 1,
                        PrecioUnitario = 35.00m,
                        PrecioUnitarioConIgv = 35.00m,
                        Total = 35.00m,
                        TipoAfectacionIgv = TipoAfectacionIgv.Gravado,
                        Activo = true
                    }
                ]
            };
            db.Ventas.Add(venta);
            await db.SaveChangesAsync(ct);
        }

        var ahora = DateTime.UtcNow;

        var convBot = await db.ConversacionesWhatsApp
            .FirstOrDefaultAsync(c => c.TelefonoE164 == TelefonoClienteUnoE164, ct);
        if (convBot is null)
        {
            convBot = new ConversacionWhatsApp
            {
                TelefonoE164 = TelefonoClienteUnoE164,
                WhatsAppId = "51900000101",
                NombrePerfilWhatsApp = "Cliente Sintético QA 1",
                ClienteId = clienteUno.Id,
                AsociacionClienteAmbigua = false,
                Estado = EstadoConversacionWhatsApp.Abierta,
                ModoAtencion = ModoAtencionWhatsApp.Bot,
                FechaUltimoMensaje = ahora.AddMinutes(-10),
                FechaUltimoMensajeEntrante = ahora.AddMinutes(-11),
                Activo = true
            };
            db.ConversacionesWhatsApp.Add(convBot);
            await db.SaveChangesAsync(ct);
        }

        var msgEntranteBot = await db.MensajesWhatsApp
            .FirstOrDefaultAsync(m => m.ExternalMessageId == ExternalIdMensajeEntranteBot, ct);
        if (msgEntranteBot is null)
        {
            msgEntranteBot = new MensajeWhatsApp
            {
                ConversacionId = convBot.Id,
                ExternalMessageId = ExternalIdMensajeEntranteBot,
                Direccion = DireccionMensajeWhatsApp.Entrante,
                Tipo = TipoMensajeWhatsApp.Texto,
                ContenidoTexto = "Hola, ¿cuál es el horario de atención del taller?",
                Estado = EstadoMensajeWhatsApp.Recibido,
                FechaEventoUtc = ahora.AddMinutes(-11),
                FechaRecepcionUtc = ahora.AddMinutes(-11),
                MetadataTecnicaJson = "{\"fixture\":\"qa-synthetic\",\"channel\":\"mock\"}",
                Activo = true
            };
            db.MensajesWhatsApp.Add(msgEntranteBot);
            await db.SaveChangesAsync(ct);
        }

        var msgSalienteMock = await db.MensajesWhatsApp
            .FirstOrDefaultAsync(m => m.ExternalMessageId == ExternalIdMensajeSalienteMock, ct);
        if (msgSalienteMock is null)
        {
            const string idempotencyKey = "qa-synth-mock-out-001";
            msgSalienteMock = new MensajeWhatsApp
            {
                ConversacionId = convBot.Id,
                ExternalMessageId = ExternalIdMensajeSalienteMock,
                IdempotencyKey = idempotencyKey,
                Direccion = DireccionMensajeWhatsApp.Saliente,
                Tipo = TipoMensajeWhatsApp.Texto,
                ContenidoTexto = "[MOCK QA] Nuestro horario de atención es de lunes a viernes de 8:00 a 18:00 y sábados de 8:00 a 13:00.",
                Estado = EstadoMensajeWhatsApp.Enviado,
                FechaEventoUtc = ahora.AddMinutes(-10),
                FechaRecepcionUtc = ahora.AddMinutes(-10),
                FechaUltimoEstadoUtc = ahora.AddMinutes(-10),
                MetadataTecnicaJson = "{\"provider\":\"Mock\",\"environment\":\"QA\",\"synthetic\":true}",
                Activo = true
            };
            db.MensajesWhatsApp.Add(msgSalienteMock);
            await db.SaveChangesAsync(ct);

            var outboxMock = new OutboxMensajeWhatsApp
            {
                MensajeWhatsAppId = msgSalienteMock.Id,
                ConversacionId = convBot.Id,
                DestinatarioE164 = TelefonoClienteUnoE164,
                ContenidoTexto = msgSalienteMock.ContenidoTexto,
                IdempotencyKey = idempotencyKey,
                Estado = EstadoOutboxWhatsApp.Enviado,
                Intentos = 1,
                MaxIntentos = 3,
                FechaProcesadoUtc = ahora.AddMinutes(-10),
                UltimoCodigoHttp = 200,
                HistorialIntentosJson = "[{\"intento\":1,\"proveedor\":\"Mock\",\"estado\":\"Enviado\"}]",
                Activo = true
            };
            db.OutboxMensajesWhatsApp.Add(outboxMock);
            await db.SaveChangesAsync(ct);
        }

        var convHumana = await db.ConversacionesWhatsApp
            .FirstOrDefaultAsync(c => c.TelefonoE164 == TelefonoClienteDosE164, ct);
        if (convHumana is null)
        {
            convHumana = new ConversacionWhatsApp
            {
                TelefonoE164 = TelefonoClienteDosE164,
                WhatsAppId = "51900000102",
                NombrePerfilWhatsApp = "Empresa Sintética QA 2",
                ClienteId = clienteDos.Id,
                AsociacionClienteAmbigua = false,
                Estado = EstadoConversacionWhatsApp.EnAtencion,
                ModoAtencion = ModoAtencionWhatsApp.Humano,
                AgenteAsignadoId = recepcion.Id,
                FechaUltimoMensaje = ahora.AddMinutes(-5),
                FechaUltimoMensajeEntrante = ahora.AddMinutes(-5),
                FechaCambioModoHumano = ahora.AddMinutes(-5),
                Activo = true
            };
            db.ConversacionesWhatsApp.Add(convHumana);
            await db.SaveChangesAsync(ct);
        }

        var msgEntranteHumano = await db.MensajesWhatsApp
            .FirstOrDefaultAsync(m => m.ExternalMessageId == ExternalIdMensajeEntranteHumano, ct);
        if (msgEntranteHumano is null)
        {
            msgEntranteHumano = new MensajeWhatsApp
            {
                ConversacionId = convHumana.Id,
                ExternalMessageId = ExternalIdMensajeEntranteHumano,
                Direccion = DireccionMensajeWhatsApp.Entrante,
                Tipo = TipoMensajeWhatsApp.Texto,
                ContenidoTexto = "Solicito atención de un asesor humano para cotización de flota QA.",
                Estado = EstadoMensajeWhatsApp.Recibido,
                FechaEventoUtc = ahora.AddMinutes(-5),
                FechaRecepcionUtc = ahora.AddMinutes(-5),
                MetadataTecnicaJson = "{\"fixture\":\"qa-synthetic\",\"escalated\":true}",
                Activo = true
            };
            db.MensajesWhatsApp.Add(msgEntranteHumano);
            await db.SaveChangesAsync(ct);
        }

        return new ResultadoSeedSinteticoQa(
            gerencia.Id,
            recepcion.Id,
            tecnico.Id,
            vendedor.Id,
            clienteUno.Id,
            clienteDos.Id,
            prodLubricante.Id,
            prodFiltro.Id,
            vehiculoUno.Id,
            vehiculoDos.Id,
            orden.Id,
            venta.Id,
            convBot.Id,
            convHumana.Id,
            msgEntranteBot.Id,
            msgSalienteMock.Id,
            msgEntranteHumano.Id);
    }

    private static async Task<Usuario> AsegurarUsuarioConRolAsync(
        ApplicationDbContext db,
        UserManager<Usuario>? userManager,
        string email,
        string nombreCompleto,
        string nombreRol,
        CancellationToken ct)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (usuario is null)
        {
            usuario = new Usuario
            {
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NombreCompleto = nombreCompleto,
                EmailConfirmed = true,
                Activo = true,
                SecurityStamp = Guid.NewGuid().ToString("N")
            };

            if (userManager is not null)
            {
                var resultado = await userManager.CreateAsync(usuario, ClaveSinteticaQa);
                if (!resultado.Succeeded)
                {
                    var errores = string.Join("; ", resultado.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"No se pudo crear el usuario sintético {email}: {errores}");
                }
            }
            else
            {
                // Generar un hash PBKDF2 real y verificable de ASP.NET Core Identity (nunca un texto marcador)
                usuario.PasswordHash = HasherReal.HashPassword(usuario, ClaveSinteticaQa);
                db.Usuarios.Add(usuario);
                await db.SaveChangesAsync(ct);
            }
        }

        var rol = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == nombreRol && r.Activo, ct)
            ?? throw new InvalidOperationException($"No se encontró el rol activo «{nombreRol}».");

        var tieneRol = await db.UsuarioRoles.AnyAsync(ur => ur.UsuarioId == usuario.Id && ur.RolId == rol.Id, ct);
        if (!tieneRol)
        {
            db.UsuarioRoles.Add(new UsuarioRol
            {
                UsuarioId = usuario.Id,
                RolId = rol.Id
            });
            await db.SaveChangesAsync(ct);
        }

        return usuario;
    }
}
