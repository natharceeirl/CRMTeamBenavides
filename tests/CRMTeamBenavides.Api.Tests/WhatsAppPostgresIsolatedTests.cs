using System.Text;
using System.Text.Json;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.WhatsApp;
using CRMTeamBenavides.Api.Services.WhatsApp.IA;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas de integración reales contra una base de datos PostgreSQL local EFÍMERA y AISLADA
/// (<c>crm_whatsapp_ephemeral_&lt;id&gt;</c>), creada al iniciar la clase y eliminada al terminar.
/// Jamás toca <c>crm_teambenavides_qa</c> ni <c>crm_teambenavides</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class WhatsAppPostgresIsolatedTests : IAsyncLifetime
{
    private const string PrefijoDbEfimera = "crm_whatsapp_ephemeral_";
    private const string MigracionAnteriorAWhatsApp = "20261113043000_AgregarDeclaracionVentas";
    private const string MigracionWhatsAppInicial = "20261121014809_AgregarInfraestructuraWhatsApp";
    private const string MigracionWhatsAppHistorial = "20261121044745_AgregarHistorialIntentosOutboxWhatsApp";

    private string _adminConnectionString = string.Empty;
    private string _ephemeralConnectionString = string.Empty;
    private string _ephemeralDbName = string.Empty;

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; set; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class AuditoriaNoOp : IAuditoriaService
    {
        public Task RegistrarEventoAsync(Guid? usuarioId, string accion, string entidad, string entidadId, object? detalle = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class CountingDelayProvider(int delayMs = 60, Queue<WhatsAppSendResult>? resultados = null) : IWhatsAppProvider
    {
        private int _llamadas;
        public string NombreProveedor => "MockPgTest";
        public int TotalLlamadas => _llamadas;

        public async Task<WhatsAppSendResult> EnviarTextoAsync(WhatsAppSendTextRequest request, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _llamadas);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, ct);
            }

            if (resultados is not null)
            {
                lock (resultados)
                {
                    if (resultados.Count > 0)
                    {
                        return resultados.Dequeue();
                    }
                }
            }

            return new WhatsAppSendResult(
                true,
                $"wamid.pg.{Guid.NewGuid():N}",
                200,
                null,
                TipoResultadoEnvioWhatsApp.Exitoso,
                NombreProveedor);
        }
    }

    public async Task InitializeAsync()
    {
        var baseConn = ObtenerCadenaBaseLocal();
        var baseBuilder = new NpgsqlConnectionStringBuilder(baseConn);
        var host = baseBuilder.Host?.Trim().ToLowerInvariant();
        if (host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            throw new InvalidOperationException($"[SEGURIDAD] Host no permitido para prueba aislada: '{host}'.");
        }

        _ephemeralDbName = $"{PrefijoDbEfimera}{Guid.NewGuid():N}"[..38];
        if (!_ephemeralDbName.StartsWith(PrefijoDbEfimera, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("[SEGURIDAD] Nombre de BD efímera inválido.");
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(baseConn) { Database = "postgres" };
        _adminConnectionString = adminBuilder.ConnectionString;

        var ephBuilder = new NpgsqlConnectionStringBuilder(baseConn) { Database = _ephemeralDbName };
        _ephemeralConnectionString = ephBuilder.ConnectionString;

        await using (var adminConn = new NpgsqlConnection(_adminConnectionString))
        {
            await adminConn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_ephemeralDbName}\";", adminConn);
            await cmd.ExecuteNonQueryAsync();
        }

        await using var ctx = CrearContexto();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(_ephemeralDbName) || !_ephemeralDbName.StartsWith(PrefijoDbEfimera, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            NpgsqlConnection.ClearAllPools();
            await using var adminConn = new NpgsqlConnection(_adminConnectionString);
            await adminConn.OpenAsync();
            await using var terminate = new NpgsqlCommand(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @db AND pid <> pg_backend_pid();",
                adminConn);
            terminate.Parameters.AddWithValue("db", _ephemeralDbName);
            await terminate.ExecuteNonQueryAsync();

            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_ephemeralDbName}\";", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // No bloquear finalización de pruebas si falló la creación inicial
        }
    }

    private ApplicationDbContext CrearContexto()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_ephemeralConnectionString)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static string ObtenerCadenaBaseLocal()
    {
        var envConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envConn))
        {
            return envConn;
        }

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets(typeof(ApplicationDbContext).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        return config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=";
    }

    [Fact]
    public async Task PG01_MigracionWhatsApp_AplicaRevierteYReaplicaSinAlterarChatbotExistente()
    {
        await using var ctx = CrearContexto();

        // Insertar un FAQ y una consulta del chatbot web existentes para verificar que nunca se alteran
        ctx.FaqItems.Add(new FaqItem
        {
            Categoria = "Horarios",
            Pregunta = "¿Horario de prueba?",
            Respuesta = "Lun a Sab",
            Activo = true
        });
        ctx.ConsultasChatbot.Add(new ConsultaChatbot
        {
            Canal = "Web",
            MensajeConsulta = "Consulta previa web",
            Activo = true
        });
        await ctx.SaveChangesAsync();

        var migrator = ctx.Database.GetService<IMigrator>();

        // 1. Revertir ambas migraciones de WhatsApp (Down) hasta AgregarDeclaracionVentas
        await migrator.MigrateAsync(MigracionAnteriorAWhatsApp);

        await using (var conn = new NpgsqlConnection(_ephemeralConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('ConversacionesWhatsApp', 'MensajesWhatsApp', 'OutboxMensajesWhatsApp', 'EventosWebhookWhatsApp');",
                conn);
            Assert.Equal(0, Convert.ToInt32(await cmd.ExecuteScalarAsync()));
        }

        // Tablas originales del chatbot web intactas tras Down()
        Assert.Equal(1, await ctx.FaqItems.CountAsync());
        Assert.Equal(1, await ctx.ConsultasChatbot.CountAsync());

        // 2. Aplicar SOLO la primera migración (20261121014809_AgregarInfraestructuraWhatsApp)
        await migrator.MigrateAsync(MigracionWhatsAppInicial);

        await using (var conn = new NpgsqlConnection(_ephemeralConnectionString))
        {
            await conn.OpenAsync();
            await using var cmdTablas = new NpgsqlCommand(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('ConversacionesWhatsApp', 'MensajesWhatsApp', 'OutboxMensajesWhatsApp', 'EventosWebhookWhatsApp');",
                conn);
            Assert.Equal(4, Convert.ToInt32(await cmdTablas.ExecuteScalarAsync()));

            await using var cmdCol = new NpgsqlCommand(
                "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'OutboxMensajesWhatsApp' AND column_name = 'HistorialIntentosJson';",
                conn);
            Assert.Equal(0, Convert.ToInt32(await cmdCol.ExecuteScalarAsync()));
        }

        // 3. Aplicar la segunda migración (AgregarHistorialIntentosOutboxWhatsApp)
        await migrator.MigrateAsync(MigracionWhatsAppHistorial);

        await using (var conn = new NpgsqlConnection(_ephemeralConnectionString))
        {
            await conn.OpenAsync();
            await using var cmdCol = new NpgsqlCommand(
                "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'OutboxMensajesWhatsApp' AND column_name = 'HistorialIntentosJson';",
                conn);
            Assert.Equal(1, Convert.ToInt32(await cmdCol.ExecuteScalarAsync()));

            // Verificar existencia de los índices únicos críticos en pg_indexes
            await using var cmdIdx = new NpgsqlCommand(
                "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND indexname IN ('IX_ConversacionesWhatsApp_TelefonoE164', 'IX_MensajesWhatsApp_ExternalMessageId', 'IX_MensajesWhatsApp_IdempotencyKey', 'IX_OutboxMensajesWhatsApp_IdempotencyKey', 'IX_OutboxMensajesWhatsApp_MensajeWhatsAppId');",
                conn);
            Assert.Equal(5, Convert.ToInt32(await cmdIdx.ExecuteScalarAsync()));
        }

        // 4. Comprobar persistencia de los nuevos valores de enum Indeterminado y conservación de chatbot web
        Assert.Equal(1, await ctx.FaqItems.CountAsync());
        Assert.Equal(1, await ctx.ConsultasChatbot.CountAsync());
    }

    [Fact]
    public async Task PG02_DosWorkersConcurrentes_ForUpdateSkipLocked_NoReservanNiDuplicanEnvios()
    {
        await using (var seed = CrearContexto())
        {
            var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987000111", Activo = true };
            seed.ConversacionesWhatsApp.Add(conv);
            for (int i = 1; i <= 4; i++)
            {
                var msg = new MensajeWhatsApp
                {
                    Conversacion = conv,
                    IdempotencyKey = $"pg-skip-lock-{i}",
                    Direccion = DireccionMensajeWhatsApp.Saliente,
                    Tipo = TipoMensajeWhatsApp.Texto,
                    ContenidoTexto = $"Mensaje concurrente {i}",
                    Estado = EstadoMensajeWhatsApp.Pendiente,
                    Activo = true
                };
                seed.MensajesWhatsApp.Add(msg);
                seed.OutboxMensajesWhatsApp.Add(new OutboxMensajeWhatsApp
                {
                    MensajeWhatsApp = msg,
                    ConversacionId = conv.Id,
                    DestinatarioE164 = conv.TelefonoE164,
                    ContenidoTexto = msg.ContenidoTexto,
                    IdempotencyKey = $"pg-skip-lock-{i}",
                    Estado = EstadoOutboxWhatsApp.Pendiente,
                    MaxIntentos = 3,
                    ProximoIntentoUtc = DateTime.UtcNow.AddSeconds(-5),
                    Activo = true
                });
            }

            await seed.SaveChangesAsync();
        }

        var provider = new CountingDelayProvider(delayMs: 50);
        var settingsMonitor = new StaticOptionsMonitor<WhatsAppSettings>(new WhatsAppSettings
        {
            Enabled = true,
            Provider = WhatsAppSettings.ProviderMock,
            OutboxBatchSize = 2
        });

        async Task<int> EjecutarWorkerAsync()
        {
            await using var db = CrearContexto();
            var outboxSvc = new WhatsAppOutboxService(db, provider, settingsMonitor, NullLogger<WhatsAppOutboxService>.Instance);
            int total = 0;
            while (true)
            {
                var procesados = await outboxSvc.ProcesarLotePendienteAsync();
                if (procesados == 0) break;
                total += procesados;
            }
            return total;
        }

        // Dos workers independientes con conexiones PostgreSQL separadas compitiendo por los mismos 4 registros
        var resultados = await Task.WhenAll(EjecutarWorkerAsync(), EjecutarWorkerAsync());

        Assert.Equal(4, resultados.Sum());
        Assert.Equal(4, provider.TotalLlamadas); // Exactamente 1 envío por mensaje

        await using var check = CrearContexto();
        Assert.Equal(4, await check.OutboxMensajesWhatsApp.CountAsync(o => o.Estado == EstadoOutboxWhatsApp.Enviado));
        Assert.Equal(4, await check.MensajesWhatsApp.CountAsync(m => m.Estado == EstadoMensajeWhatsApp.Enviado && m.ExternalMessageId != null));
    }

    [Fact]
    public async Task PG03_IndicesUnicosParcialesYReintentoConHistorialYRecuperacionReservaExpirada()
    {
        Guid convId;
        await using (var seed = CrearContexto())
        {
            var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987000222", Activo = true };
            seed.ConversacionesWhatsApp.Add(conv);
            seed.MensajesWhatsApp.Add(new MensajeWhatsApp
            {
                Conversacion = conv,
                ExternalMessageId = "wamid.pg.unique.1",
                Direccion = DireccionMensajeWhatsApp.Entrante,
                Tipo = TipoMensajeWhatsApp.Texto,
                ContenidoTexto = "Primero",
                Estado = EstadoMensajeWhatsApp.Recibido,
                Activo = true
            });
            await seed.SaveChangesAsync();
            convId = conv.Id;
        }

        // 1. Verificar restricción única real de PostgreSQL sobre ExternalMessageId (sin depender de locks en memoria)
        await using (var dupCtx = CrearContexto())
        {
            dupCtx.MensajesWhatsApp.Add(new MensajeWhatsApp
            {
                ConversacionId = convId,
                ExternalMessageId = "wamid.pg.unique.1",
                Direccion = DireccionMensajeWhatsApp.Entrante,
                Tipo = TipoMensajeWhatsApp.Texto,
                ContenidoTexto = "Duplicado directo en BD",
                Estado = EstadoMensajeWhatsApp.Recibido,
                Activo = true
            });

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => dupCtx.SaveChangesAsync());
            var pgEx = Assert.IsType<PostgresException>(ex.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, pgEx.SqlState);
        }

        // 2. Verificar reintento seguro (429 -> éxito en intento 2) registrando historial de ambos intentos en PostgreSQL
        Guid outboxRetryId;
        await using (var seedRetry = CrearContexto())
        {
            var msg = new MensajeWhatsApp
            {
                ConversacionId = convId,
                IdempotencyKey = "pg-retry-429",
                Direccion = DireccionMensajeWhatsApp.Saliente,
                Tipo = TipoMensajeWhatsApp.Texto,
                ContenidoTexto = "Reintento seguro 429",
                Estado = EstadoMensajeWhatsApp.Pendiente,
                Activo = true
            };
            var outbox = new OutboxMensajeWhatsApp
            {
                MensajeWhatsApp = msg,
                ConversacionId = convId,
                DestinatarioE164 = "+51987000222",
                ContenidoTexto = msg.ContenidoTexto,
                IdempotencyKey = "pg-retry-429",
                Estado = EstadoOutboxWhatsApp.Pendiente,
                MaxIntentos = 3,
                ProximoIntentoUtc = DateTime.UtcNow.AddSeconds(-5),
                Activo = true
            };
            seedRetry.MensajesWhatsApp.Add(msg);
            seedRetry.OutboxMensajesWhatsApp.Add(outbox);
            await seedRetry.SaveChangesAsync();
            outboxRetryId = outbox.Id;
        }

        var cola = new Queue<WhatsAppSendResult>();
        cola.Enqueue(new WhatsAppSendResult(false, null, 429, "Rate limit Meta", TipoResultadoEnvioWhatsApp.ErrorReintentableSeguro, "Meta", RetryAfterSegundos: 5));
        cola.Enqueue(new WhatsAppSendResult(true, "wamid.pg.retry.ok", 200, null, TipoResultadoEnvioWhatsApp.Exitoso, "Meta"));

        var retryProvider = new CountingDelayProvider(delayMs: 0, resultados: cola);
        var monitor = new StaticOptionsMonitor<WhatsAppSettings>(new WhatsAppSettings { Enabled = true, Provider = WhatsAppSettings.ProviderMock });

        await using (var db1 = CrearContexto())
        {
            var svc1 = new WhatsAppOutboxService(db1, retryProvider, monitor, NullLogger<WhatsAppOutboxService>.Instance);
            await svc1.ProcesarRegistroAsync(outboxRetryId);
        }

        // Forzar vencimiento de ProximoIntentoUtc para ejecutar el intento 2 inmediatamente
        await using (var bump = CrearContexto())
        {
            var o = await bump.OutboxMensajesWhatsApp.SingleAsync(x => x.Id == outboxRetryId);
            Assert.Equal(EstadoOutboxWhatsApp.Pendiente, o.Estado);
            Assert.Equal(1, o.Intentos);
            o.ProximoIntentoUtc = DateTime.UtcNow.AddSeconds(-1);
            await bump.SaveChangesAsync();
        }

        await using (var db2 = CrearContexto())
        {
            var svc2 = new WhatsAppOutboxService(db2, retryProvider, monitor, NullLogger<WhatsAppOutboxService>.Instance);
            await svc2.ProcesarRegistroAsync(outboxRetryId);
        }

        await using (var check = CrearContexto())
        {
            var o = await check.OutboxMensajesWhatsApp.SingleAsync(x => x.Id == outboxRetryId);
            Assert.Equal(EstadoOutboxWhatsApp.Enviado, o.Estado);
            Assert.Equal(2, o.Intentos);
            var historial = JsonSerializer.Deserialize<List<IntentoOutboxRegistro>>(o.HistorialIntentosJson!);
            Assert.NotNull(historial);
            Assert.Equal(2, historial!.Count);
            Assert.Equal("ErrorReintentableSeguro", historial[0].Clasificacion);
            Assert.Equal("Exitoso", historial[1].Clasificacion);
        }

        // 3. Verificar recuperación de reserva vencida tras caída -> Indeterminado en PostgreSQL real
        Guid outboxCrashId;
        await using (var seedCrash = CrearContexto())
        {
            var msg = new MensajeWhatsApp
            {
                ConversacionId = convId,
                IdempotencyKey = "pg-crash-expired",
                Direccion = DireccionMensajeWhatsApp.Saliente,
                Tipo = TipoMensajeWhatsApp.Texto,
                ContenidoTexto = "Caída durante envío",
                Estado = EstadoMensajeWhatsApp.Pendiente,
                Activo = true
            };
            var outbox = new OutboxMensajeWhatsApp
            {
                MensajeWhatsApp = msg,
                ConversacionId = convId,
                DestinatarioE164 = "+51987000222",
                ContenidoTexto = msg.ContenidoTexto,
                IdempotencyKey = "pg-crash-expired",
                Estado = EstadoOutboxWhatsApp.Procesando,
                Intentos = 1,
                ReservadoHastaUtc = DateTime.UtcNow.AddMinutes(-3),
                Activo = true
            };
            seedCrash.MensajesWhatsApp.Add(msg);
            seedCrash.OutboxMensajesWhatsApp.Add(outbox);
            await seedCrash.SaveChangesAsync();
            outboxCrashId = outbox.Id;
        }

        await using (var dbCrash = CrearContexto())
        {
            var svcCrash = new WhatsAppOutboxService(dbCrash, retryProvider, monitor, NullLogger<WhatsAppOutboxService>.Instance);
            var recuperados = await svcCrash.RecuperarReservasExpiradasAsync();
            Assert.Equal(1, recuperados);
        }

        await using (var checkCrash = CrearContexto())
        {
            var o = await checkCrash.OutboxMensajesWhatsApp.Include(x => x.MensajeWhatsApp).SingleAsync(x => x.Id == outboxCrashId);
            Assert.Equal(EstadoOutboxWhatsApp.Indeterminado, o.Estado);
            Assert.Equal(EstadoMensajeWhatsApp.Indeterminado, o.MensajeWhatsApp.Estado);
        }
    }

    [Fact]
    public async Task PG04_Fase2_IdentificacionVinculacionYConsultasCrmAisladasEnPostgresReal()
    {
        Guid clienteAId;
        Guid ordenBId;
        Guid convAId;

        await using (var seed = CrearContexto())
        {
            var clienteA = new Cliente
            {
                NombreCompleto = "Ana Morales PG",
                TipoDocumento = TipoDocumentoCliente.DNI,
                NumeroDocumento = "41223344",
                Telefono = "987-333-444",
                Activo = true
            };
            var clienteB = new Cliente
            {
                NombreCompleto = "Pedro Rios PG",
                TipoDocumento = TipoDocumentoCliente.DNI,
                NumeroDocumento = "41556677",
                Telefono = "+51 987 555 666",
                Activo = true
            };
            var vehA = new Vehiculo
            {
                Cliente = clienteA,
                Marca = "Yamaha",
                Modelo = "FZ25",
                Placa = "4455-AA",
                NumeroSerieVIN = "ME1RG4287S2011390",
                Activo = true
            };
            var vehB = new Vehiculo
            {
                Cliente = clienteB,
                Marca = "Yamaha",
                Modelo = "MT-07",
                Placa = "7788-BB",
                NumeroSerieVIN = "JYARM04E0GA001234",
                Activo = true
            };
            var ordenA = new OrdenServicio
            {
                Cliente = clienteA,
                Vehiculo = vehA,
                NumeroOrden = "OS-PG-001",
                Estado = EstadoOrdenServicio.EnProceso,
                FechaIngreso = DateTime.UtcNow.AddDays(-1),
                Total = 250m,
                Activo = true
            };
            var ordenB = new OrdenServicio
            {
                Cliente = clienteB,
                Vehiculo = vehB,
                NumeroOrden = "OS-PG-002",
                Estado = EstadoOrdenServicio.Lista,
                FechaIngreso = DateTime.UtcNow.AddDays(-1),
                Total = 800m,
                Activo = true
            };
            var citaA = new Cita
            {
                Cliente = clienteA,
                Vehiculo = vehA,
                NumeroCita = "CIT-PG-001",
                FechaHoraProgramada = DateTime.UtcNow.AddDays(2),
                Motivo = "Mantenimiento 5000km",
                Estado = EstadoCita.Confirmada,
                Activo = true
            };
            var convA = new ConversacionWhatsApp
            {
                TelefonoE164 = "+51987333444",
                Activo = true
            };

            seed.Clientes.AddRange(clienteA, clienteB);
            seed.Vehiculos.AddRange(vehA, vehB);
            seed.OrdenesServicio.AddRange(ordenA, ordenB);
            seed.Citas.Add(citaA);
            seed.ConversacionesWhatsApp.Add(convA);
            await seed.SaveChangesAsync();

            clienteAId = clienteA.Id;
            ordenBId = ordenB.Id;
            convAId = convA.Id;
        }

        var monitor = new StaticOptionsMonitor<WhatsAppSettings>(new WhatsAppSettings { DefaultCountryCode = "51" });

        await using (var db = CrearContexto())
        {
            var identSvc = new WhatsAppClienteIdentificacionService(db, monitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
            var resIdent = await identSvc.IdentificarPorTelefonoAsync("51987333444");
            Assert.Equal(EstadoIdentificacionClienteWhatsApp.IdentificadoUnivoco, resIdent.Estado);
            Assert.Equal(clienteAId, resIdent.ClienteId);

            // Vincular la conversación A al cliente A
            var conv = await db.ConversacionesWhatsApp.SingleAsync(c => c.Id == convAId);
            conv.ClienteId = clienteAId;
            await db.SaveChangesAsync();

            var crm = new WhatsAppCrmConsultaService(
                db,
                monitor,
                WhatsAppVerificadorIdentidadNoImplementado.Instancia,
                NullLogger<WhatsAppCrmConsultaService>.Instance);

            // Modo conversacional sin identidad verificada queda bloqueado (Forbidden) incluso con ClienteId asignado
            var convBloqueado = await crm.ConsultarPerfilClienteAsync(convAId);
            Assert.Equal(ServiceResultStatus.Forbidden, convBloqueado.Status);

            var ctxAdmin = WhatsAppContextoConsultaCrm.Administrativo(permitirDocumentoCompleto: false);

            var perfil = await crm.ConsultarPerfilClienteAsync(convAId, ctxAdmin);
            Assert.True(perfil.IsSuccess);
            Assert.Equal("****3344", perfil.Data!.DocumentoVisible);
            Assert.Equal(1, perfil.Data.CantidadVehiculosActivos);
            Assert.Equal(1, perfil.Data.CantidadOrdenesActivas);
            Assert.Equal(1, perfil.Data.CantidadCitasActivas);

            var ordenesA = await crm.ConsultarOrdenesServicioClienteAsync(convAId, contexto: ctxAdmin);
            Assert.Single(ordenesA.Data!);
            Assert.Equal("OS-PG-001", ordenesA.Data![0].NumeroOrden);

            var citasA = await crm.ConsultarCitasClienteAsync(convAId, contexto: ctxAdmin);
            Assert.Single(citasA.Data!);
            Assert.Equal("CIT-PG-001", citasA.Data![0].NumeroCita);

            // Aislamiento real en PostgreSQL: conversación A jamás ve orden B
            var intentoCruzado = await crm.ConsultarEstadoOrdenServicioAsync(convAId, ordenServicioId: ordenBId, contexto: ctxAdmin);
            Assert.Equal(ServiceResultStatus.NotFound, intentoCruzado.Status);

            // Combinación inconsistente de ordenServicioId + numeroOrden devuelve ValidationError (B-02)
            var ordenAId = ordenesA.Data![0].Id;
            var comboInconsistente = await crm.ConsultarEstadoOrdenServicioAsync(
                convAId,
                ordenServicioId: ordenAId,
                numeroOrden: "OS-PG-999",
                contexto: ctxAdmin);
            Assert.Equal(ServiceResultStatus.ValidationError, comboInconsistente.Status);

            // Búsqueda SQL paginada en PostgreSQL real por teléfono normalizado (M-05)
            var outbox = new WhatsAppOutboxService(
                db,
                new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance),
                monitor,
                NullLogger<WhatsAppOutboxService>.Instance);
            var waSvc = new WhatsAppService(db, outbox, identSvc, new AuditoriaNoOp(), monitor);
            var busq = await waSvc.BuscarClientesParaVincularAsync("+51 987 333 444", pagina: 1, tamanoPagina: 10);
            Assert.True(busq.IsSuccess);
            var candidato = Assert.Single(busq.Data!.Items);
            Assert.Equal(clienteAId, candidato.Id);
            Assert.Equal("****3344", candidato.DocumentoEnmascarado);
        }
    }

    [Fact]
    public async Task PG05_Fase3_OrquestacionIaCatalogoBloqueoPersonalEscalamientoYIdempotenciaEnPostgresReal()
    {
        await using (var seed = CrearContexto())
        {
            seed.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa
            {
                NombreEmpresa = "Team Benavides PG",
                PorcentajeIgv = 18m,
                TipoCambioVigente = 3.70m,
                Activo = true
            });
            var cat = new CategoriaProducto { Nombre = "Aceites PG", Activo = true };
            seed.CategoriasProducto.Add(cat);
            seed.Productos.Add(new Producto
            {
                Categoria = cat,
                Codigo = "YAM-PG-01",
                Nombre = "Aceite Yamalube PG 10W40",
                Marca = "YAMAHA",
                PrecioVenta = 50m,
                MonedaPrecio = MonedaPrecio.PEN,
                PrecioIncluyeIgv = false, // Con IGV = 59.00
                Costo = 21.50m,
                StockActual = 14,
                Activo = true
            });
            var cliente = new Cliente
            {
                NombreCompleto = "Cliente PG Fase3",
                TipoDocumento = TipoDocumentoCliente.DNI,
                NumeroDocumento = "44332211",
                Telefono = "987555666",
                Activo = true
            };
            var veh = new Vehiculo { Cliente = cliente, Marca = "Yamaha", Modelo = "FZ-S", Placa = "5555-PG", Activo = true };
            var orden = new OrdenServicio
            {
                Cliente = cliente,
                Vehiculo = veh,
                NumeroOrden = "OS-PG-900",
                Estado = EstadoOrdenServicio.EnProceso,
                Diagnostico = "SECRETO_DIAGNOSTICO_PG",
                Total = 640m,
                Activo = true
            };
            seed.Clientes.Add(cliente);
            seed.Vehiculos.Add(veh);
            seed.OrdenesServicio.Add(orden);
            await seed.SaveChangesAsync();
        }

        var waMonitor = new StaticOptionsMonitor<WhatsAppSettings>(new WhatsAppSettings
        {
            Enabled = true,
            Provider = WhatsAppSettings.ProviderMock,
            DefaultCountryCode = "51"
        });
        var aiMonitor = new StaticOptionsMonitor<WhatsAppAiSettings>(new WhatsAppAiSettings
        {
            Enabled = true,
            Provider = WhatsAppAiSettings.ProviderMock
        });

        static byte[] CrearPayloadTexto(string wamid, string from, string body) =>
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        changes = new[]
                        {
                            new
                            {
                                field = "messages",
                                value = new
                                {
                                    contacts = new[] { new { wa_id = from, profile = new { name = "Cliente PG3" } } },
                                    messages = new[]
                                    {
                                        new
                                        {
                                            from,
                                            id = wamid,
                                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                                            type = "text",
                                            text = new { body }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }));

        Guid convId;
        Guid msgCatalogoId;

        await using (var db = CrearContexto())
        {
            var identSvc = new WhatsAppClienteIdentificacionService(db, waMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
            var crmConsulta = new WhatsAppCrmConsultaService(db, waMonitor, WhatsAppVerificadorIdentidadNoImplementado.Instancia, NullLogger<WhatsAppCrmConsultaService>.Instance);
            var facade = new WhatsAppAiCrmToolsFacade(db, crmConsulta);
            var outbox = new WhatsAppOutboxService(db, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitor);
            var escalamiento = new WhatsAppEscalamientoService(db, new AuditoriaNoOp(), NullLogger<WhatsAppEscalamientoService>.Instance);
            var orq = new WhatsAppConversacionOrquestador(
                db,
                new WhatsAppIntentClassifier(),
                facade,
                new MockWhatsAppAiProvider(aiMonitor),
                escalamiento,
                outbox,
                aiMonitor,
                waMonitor,
                NullLogger<WhatsAppConversacionOrquestador>.Instance);
            var autoResponder = new WhatsAppAiAutoResponder(orq, aiMonitor, waMonitor, NullLogger<WhatsAppAiAutoResponder>.Instance);
            var webhook = new WhatsAppWebhookService(db, identSvc, autoResponder, waMonitor, NullLogger<WhatsAppWebhookService>.Instance);

            // 1. Mensaje de catálogo público vía webhook -> responde con 59.00 (incl. IGV), sin Costo ni stock exacto
            var r1 = await webhook.ProcesarNotificacionAsync(CrearPayloadTexto("wamid.pg3.cat.1", "51987555666", "¿Cuánto cuesta el aceite Yamalube?"));
            Assert.True(r1.EsValido);
            Assert.Equal(1, r1.MensajesProcesados);

            var conv = await db.ConversacionesWhatsApp.SingleAsync(c => c.TelefonoE164 == "+51987555666");
            convId = conv.Id;
            Assert.NotNull(conv.ClienteId); // Identificado automáticamente por teléfono

            var msgEntranteCat = await db.MensajesWhatsApp.SingleAsync(m => m.ExternalMessageId == "wamid.pg3.cat.1");
            msgCatalogoId = msgEntranteCat.Id;

            var msgSalienteCat = await db.MensajesWhatsApp.SingleAsync(m => m.IdempotencyKey == $"ai:reply:{msgCatalogoId:N}");
            Assert.Equal(EstadoMensajeWhatsApp.Enviado, msgSalienteCat.Estado);
            Assert.Contains("59.00", msgSalienteCat.ContenidoTexto!);
            Assert.DoesNotContain("21.50", msgSalienteCat.ContenidoTexto!);
            Assert.DoesNotContain("14", msgSalienteCat.ContenidoTexto!);

            // Reejecución idempotente sobre el mismo mensaje -> omitido sin duplicar
            var rDup = await orq.ProcesarMensajeEntranteAsync(convId, msgCatalogoId);
            Assert.Equal(EstadoOrquestacionWhatsApp.Omitido, rDup.Estado);
            Assert.Equal("RESPUESTA_DUPLICADA", rDup.MotivoCodigo);

            // 2. Mensaje solicitando orden personal -> bloquea datos personales y escala a Humano en PostgreSQL real
            var r2 = await webhook.ProcesarNotificacionAsync(CrearPayloadTexto("wamid.pg3.ord.2", "51987555666", "¿Cómo va mi orden OS-PG-900?"));
            Assert.True(r2.EsValido);

            var msgEntranteOrd = await db.MensajesWhatsApp.SingleAsync(m => m.ExternalMessageId == "wamid.pg3.ord.2");
            var msgSalienteOrd = await db.MensajesWhatsApp.SingleAsync(m => m.IdempotencyKey == $"ai:reply:{msgEntranteOrd.Id:N}");
            Assert.DoesNotContain("SECRETO_DIAGNOSTICO_PG", msgSalienteOrd.ContenidoTexto!);
            Assert.DoesNotContain("640", msgSalienteOrd.ContenidoTexto!);
            Assert.Contains("verificación de identidad", msgSalienteOrd.ContenidoTexto!, StringComparison.OrdinalIgnoreCase);

            await db.Entry(conv).ReloadAsync();
            Assert.Equal(ModoAtencionWhatsApp.Humano, conv.ModoAtencion);
            Assert.Equal(EstadoConversacionWhatsApp.EnAtencion, conv.Estado);

            // 3. Nuevo mensaje tras escalamiento -> el webhook lo persiste pero la IA NO responde automáticamente
            var salientesAntes = await db.MensajesWhatsApp.CountAsync(m => m.ConversacionId == convId && m.Direccion == DireccionMensajeWhatsApp.Saliente);
            var r3 = await webhook.ProcesarNotificacionAsync(CrearPayloadTexto("wamid.pg3.hum.3", "51987555666", "Gracias, espero al asesor"));
            Assert.True(r3.EsValido);
            Assert.Equal(1, r3.MensajesProcesados);
            Assert.Equal(salientesAntes, await db.MensajesWhatsApp.CountAsync(m => m.ConversacionId == convId && m.Direccion == DireccionMensajeWhatsApp.Saliente));
        }
    }

    private sealed class DelayedPgAiProvider(
        Func<WhatsAppAiSolicitud, CancellationToken, Task<WhatsAppAiRespuesta>> handler) : IWhatsAppAiProvider
    {
        public string NombreProveedor => "MockPgDelay";
        public Task<WhatsAppAiRespuesta> GenerarRespuestaAsync(WhatsAppAiSolicitud solicitud, CancellationToken ct = default) =>
            handler(solicitud, ct);
    }

    [Fact]
    public async Task PG06_Fase3_RateLimitMultiinstanciaAtomicoYCarreraTomaHumanaEnPostgresReal()
    {
        var waMonitor = new StaticOptionsMonitor<WhatsAppSettings>(new WhatsAppSettings
        {
            Enabled = true,
            Provider = WhatsAppSettings.ProviderMock,
            DefaultCountryCode = "51"
        });
        var aiMonitorRateLimit = new StaticOptionsMonitor<WhatsAppAiSettings>(new WhatsAppAiSettings
        {
            Enabled = true,
            Provider = WhatsAppAiSettings.ProviderMock,
            MaxRepliesPerMinutePerConversation = 1
        });

        Guid convRateId;
        Guid msgRate1Id, msgRate2Id, msgRate3Id;
        Guid convCarreraId;
        Guid msgCarreraId;
        Guid agenteId;

        await using (var seed = CrearContexto())
        {
            var agente = new Usuario { NombreCompleto = "Agente PG6", Email = "agente.pg6@t.local", Activo = true };
            var convRate = new ConversacionWhatsApp { TelefonoE164 = "+51987660001", ModoAtencion = ModoAtencionWhatsApp.Bot, Estado = EstadoConversacionWhatsApp.Abierta, Activo = true };
            var m1 = new MensajeWhatsApp { Conversacion = convRate, Direccion = DireccionMensajeWhatsApp.Entrante, ContenidoTexto = "Hola", Activo = true };
            var m2 = new MensajeWhatsApp { Conversacion = convRate, Direccion = DireccionMensajeWhatsApp.Entrante, ContenidoTexto = "Hola", Activo = true };
            var m3 = new MensajeWhatsApp { Conversacion = convRate, Direccion = DireccionMensajeWhatsApp.Entrante, ContenidoTexto = "Hola", Activo = true };

            var convCarrera = new ConversacionWhatsApp { TelefonoE164 = "+51987660002", ModoAtencion = ModoAtencionWhatsApp.Bot, Estado = EstadoConversacionWhatsApp.Abierta, Activo = true };
            var mCarrera = new MensajeWhatsApp { Conversacion = convCarrera, Direccion = DireccionMensajeWhatsApp.Entrante, ContenidoTexto = "Hola", Activo = true };

            seed.Usuarios.Add(agente);
            seed.ConversacionesWhatsApp.AddRange(convRate, convCarrera);
            seed.MensajesWhatsApp.AddRange(m1, m2, m3, mCarrera);
            await seed.SaveChangesAsync();

            agenteId = agente.Id;
            convRateId = convRate.Id;
            msgRate1Id = m1.Id;
            msgRate2Id = m2.Id;
            msgRate3Id = m3.Id;
            convCarreraId = convCarrera.Id;
            msgCarreraId = mCarrera.Id;
        }

        // 1. Rate limit atómico en PostgreSQL (F3-M03): 3 mensajes distintos concurrentes con MaxRepliesPerMinute=1
        //    Resultado esperado bajo FOR UPDATE: 1 Respondido + 1 FallbackYEscalado (RATE_LIMIT_ANTI_BUCLE) + 1 Omitido (MODO_HUMANO)
        async Task<ResultadoOrquestacionWhatsApp> EjecutarInstanciaRateLimitAsync(Guid msgId)
        {
            await using var db = CrearContexto();
            var crmConsulta = new WhatsAppCrmConsultaService(db, waMonitor, WhatsAppVerificadorIdentidadNoImplementado.Instancia, NullLogger<WhatsAppCrmConsultaService>.Instance);
            var facade = new WhatsAppAiCrmToolsFacade(db, crmConsulta);
            var outbox = new WhatsAppOutboxService(db, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitorRateLimit);
            var esc = new WhatsAppEscalamientoService(db, new AuditoriaNoOp(), NullLogger<WhatsAppEscalamientoService>.Instance);
            var orq = new WhatsAppConversacionOrquestador(
                db,
                new WhatsAppIntentClassifier(),
                facade,
                new MockWhatsAppAiProvider(aiMonitorRateLimit),
                esc,
                outbox,
                aiMonitorRateLimit,
                waMonitor,
                NullLogger<WhatsAppConversacionOrquestador>.Instance);

            return await orq.ProcesarMensajeEntranteAsync(convRateId, msgId);
        }

        var resRate = await Task.WhenAll(
            EjecutarInstanciaRateLimitAsync(msgRate1Id),
            EjecutarInstanciaRateLimitAsync(msgRate2Id),
            EjecutarInstanciaRateLimitAsync(msgRate3Id));

        Assert.Equal(1, resRate.Count(r => r.Estado == EstadoOrquestacionWhatsApp.Respondido));
        Assert.Equal(1, resRate.Count(r => r.Estado == EstadoOrquestacionWhatsApp.FallbackYEscalado && r.MotivoCodigo == "RATE_LIMIT_ANTI_BUCLE"));
        Assert.Equal(1, resRate.Count(r => r.Estado == EstadoOrquestacionWhatsApp.Omitido && r.MotivoCodigo == "MODO_HUMANO"));

        await using (var checkRate = CrearContexto())
        {
            var convPost = await checkRate.ConversacionesWhatsApp.SingleAsync(c => c.Id == convRateId);
            Assert.Equal(ModoAtencionWhatsApp.Humano, convPost.ModoAtencion);
            Assert.Equal(2, await checkRate.MensajesWhatsApp.CountAsync(m => m.ConversacionId == convRateId && m.Direccion == DireccionMensajeWhatsApp.Saliente));
        }

        // 2. Carrera real con toma de control humano en PostgreSQL (F3-H02):
        //    El agente asigna la conversación (FOR UPDATE) mientras la IA está generando respuesta -> se descarta sin persistir Outbox.
        var iaEnVuelo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberarIa = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aiMonitorNormal = new StaticOptionsMonitor<WhatsAppAiSettings>(new WhatsAppAiSettings { Enabled = true, Provider = WhatsAppAiSettings.ProviderMock });

        var tareaIa = Task.Run(async () =>
        {
            await using var db = CrearContexto();
            var crmConsulta = new WhatsAppCrmConsultaService(db, waMonitor, WhatsAppVerificadorIdentidadNoImplementado.Instancia, NullLogger<WhatsAppCrmConsultaService>.Instance);
            var facade = new WhatsAppAiCrmToolsFacade(db, crmConsulta);
            var outbox = new WhatsAppOutboxService(db, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitorNormal);
            var esc = new WhatsAppEscalamientoService(db, new AuditoriaNoOp(), NullLogger<WhatsAppEscalamientoService>.Instance);
            var providerLento = new DelayedPgAiProvider(async (_, ct) =>
            {
                iaEnVuelo.TrySetResult();
                await liberarIa.Task.WaitAsync(ct);
                return new WhatsAppAiRespuesta(true, "No debe persistirse", false, "MockPgDelay", "m", 1, 1);
            });
            var orq = new WhatsAppConversacionOrquestador(
                db,
                new WhatsAppIntentClassifier(),
                facade,
                providerLento,
                esc,
                outbox,
                aiMonitorNormal,
                waMonitor,
                NullLogger<WhatsAppConversacionOrquestador>.Instance);

            return await orq.ProcesarMensajeEntranteAsync(convCarreraId, msgCarreraId);
        });

        await iaEnVuelo.Task;

        await using (var dbAgente = CrearContexto())
        {
            var ident = new WhatsAppClienteIdentificacionService(dbAgente, waMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
            var outbox = new WhatsAppOutboxService(dbAgente, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitorNormal);
            var waSvc = new WhatsAppService(dbAgente, outbox, ident, new AuditoriaNoOp(), waMonitor);
            var resAsig = await waSvc.AsignarAgenteAsync(convCarreraId, new WhatsAppAsignarAgenteRequest(agenteId), agenteId);
            Assert.True(resAsig.IsSuccess);
        }

        liberarIa.TrySetResult();
        var resCarrera = await tareaIa;
        Assert.Equal(EstadoOrquestacionWhatsApp.Omitido, resCarrera.Estado);
        Assert.Equal("ASIGNADA_A_AGENTE", resCarrera.MotivoCodigo);

        await using (var checkCarrera = CrearContexto())
        {
            Assert.Equal(0, await checkCarrera.MensajesWhatsApp.CountAsync(m => m.ConversacionId == convCarreraId && m.Direccion == DireccionMensajeWhatsApp.Saliente));
            Assert.Equal(0, await checkCarrera.OutboxMensajesWhatsApp.CountAsync(o => o.ConversacionId == convCarreraId));
        }
    }

    private sealed class FailOnEscalationSaveInterceptor : SaveChangesInterceptor
    {
        public bool Habilitado { get; set; } = true;
        public bool DisparoExcepcion { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Habilitado && eventData.Context is not null)
            {
                var hayAuditoriaEscalamiento = eventData.Context.ChangeTracker
                    .Entries<EventoAuditoria>()
                    .Any(e => e.State == EntityState.Added && e.Entity.Accion == "WHATSAPP_ESCALAR_HUMANO");

                if (hayAuditoriaEscalamiento)
                {
                    DisparoExcepcion = true;
                    throw new InvalidOperationException("Fallo simulado antes de confirmar la transacción de escalamiento.");
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private ApplicationDbContext CrearContextoConInterceptor(IInterceptor interceptor)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_ephemeralConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        return new ApplicationDbContext(opts);
    }

    [Fact]
    public async Task PG07_Fase3_F3H02_WebhookRastreadoConForUpdateReload_DetectaAsignacionModoHumanoYCierreEnPostgresReal()
    {
        var waMonitor = new StaticOptionsMonitor<WhatsAppSettings>(new WhatsAppSettings
        {
            Enabled = true,
            Provider = WhatsAppSettings.ProviderMock,
            DefaultCountryCode = "51"
        });
        var aiMonitor = new StaticOptionsMonitor<WhatsAppAiSettings>(new WhatsAppAiSettings
        {
            Enabled = true,
            Provider = WhatsAppAiSettings.ProviderMock
        });

        Guid agenteId;
        await using (var seed = CrearContexto())
        {
            var agente = new Usuario { NombreCompleto = "Agente PG7", Email = "agente.pg7@t.local", Activo = true };
            seed.Usuarios.Add(agente);
            await seed.SaveChangesAsync();
            agenteId = agente.Id;
        }

        static byte[] CrearPayloadTexto(string wamid, string from, string body) =>
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        changes = new[]
                        {
                            new
                            {
                                field = "messages",
                                value = new
                                {
                                    contacts = new[] { new { wa_id = from, profile = new { name = "Cliente PG7" } } },
                                    messages = new[]
                                    {
                                        new
                                        {
                                            from,
                                            id = wamid,
                                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                                            type = "text",
                                            text = new { body }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }));

        async Task EjecutarCasoWebhookRastreadoAsync(
            string telefonoWaId,
            string wamid,
            Func<WhatsAppService, Guid, Task> intervencionHumana,
            Action<ConversacionWhatsApp> validarEstadoIntacto)
        {
            var iaEnVuelo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var liberarIa = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var convTrackingConfirmado = false;

            // Flujo real de producción: WhatsAppWebhookService -> WhatsAppAiAutoResponder -> WhatsAppConversacionOrquestador
            // compartiendo el MISMO ApplicationDbContext scoped (ConversacionWhatsApp ya rastreada en ChangeTracker).
            var tareaWebhook = Task.Run(async () =>
            {
                await using var dbScoped = CrearContexto();
                var identSvc = new WhatsAppClienteIdentificacionService(dbScoped, waMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
                var crmConsulta = new WhatsAppCrmConsultaService(dbScoped, waMonitor, WhatsAppVerificadorIdentidadNoImplementado.Instancia, NullLogger<WhatsAppCrmConsultaService>.Instance);
                var facade = new WhatsAppAiCrmToolsFacade(dbScoped, crmConsulta);
                var outbox = new WhatsAppOutboxService(dbScoped, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitor);
                var esc = new WhatsAppEscalamientoService(dbScoped, new AuditoriaService(dbScoped), NullLogger<WhatsAppEscalamientoService>.Instance);
                var providerLento = new DelayedPgAiProvider(async (sol, ct) =>
                {
                    // Confirmar que ConversacionWhatsApp está efectivamente rastreada en el mismo DbContext scoped antes de Fase 3.
                    convTrackingConfirmado = dbScoped.ChangeTracker
                        .Entries<ConversacionWhatsApp>()
                        .Any(e => e.Entity.Id == sol.ConversacionId);
                    iaEnVuelo.TrySetResult();
                    await liberarIa.Task.WaitAsync(ct);
                    return new WhatsAppAiRespuesta(true, "Respuesta bot que JAMÁS debe persistirse", false, "MockPg7", "m", 1, 1);
                });
                var orq = new WhatsAppConversacionOrquestador(
                    dbScoped,
                    new WhatsAppIntentClassifier(),
                    facade,
                    providerLento,
                    esc,
                    outbox,
                    aiMonitor,
                    waMonitor,
                    NullLogger<WhatsAppConversacionOrquestador>.Instance);
                var autoResponder = new WhatsAppAiAutoResponder(orq, aiMonitor, waMonitor, NullLogger<WhatsAppAiAutoResponder>.Instance);
                var webhook = new WhatsAppWebhookService(dbScoped, identSvc, autoResponder, waMonitor, NullLogger<WhatsAppWebhookService>.Instance);

                return await webhook.ProcesarNotificacionAsync(CrearPayloadTexto(wamid, telefonoWaId, "Hola"));
            });

            await iaEnVuelo.Task;
            Assert.True(convTrackingConfirmado);

            Guid convId;
            await using (var dbAgente = CrearContexto())
            {
                var conv = await dbAgente.ConversacionesWhatsApp.SingleAsync(c => c.TelefonoE164 == $"+{telefonoWaId}");
                convId = conv.Id;

                var ident = new WhatsAppClienteIdentificacionService(dbAgente, waMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
                var outbox = new WhatsAppOutboxService(dbAgente, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitor);
                var waSvc = new WhatsAppService(dbAgente, outbox, ident, new AuditoriaService(dbAgente), waMonitor);
                await intervencionHumana(waSvc, convId);
            }

            liberarIa.TrySetResult();
            var resWebhook = await tareaWebhook;
            Assert.True(resWebhook.EsValido);
            Assert.Equal(1, resWebhook.MensajesProcesados);

            await using var check = CrearContexto();
            Assert.Equal(0, await check.MensajesWhatsApp.CountAsync(m => m.ConversacionId == convId && m.Direccion == DireccionMensajeWhatsApp.Saliente));
            Assert.Equal(0, await check.OutboxMensajesWhatsApp.CountAsync(o => o.ConversacionId == convId));

            var convFinal = await check.ConversacionesWhatsApp.AsNoTracking().SingleAsync(c => c.Id == convId);
            validarEstadoIntacto(convFinal);
        }

        // Caso 1: Asignación de agente durante inferencia con entidad ya rastreada en el DbContext del webhook
        await EjecutarCasoWebhookRastreadoAsync(
            "51987770001",
            "wamid.pg7.asig.1",
            async (waSvc, convId) => Assert.True((await waSvc.AsignarAgenteAsync(convId, new WhatsAppAsignarAgenteRequest(agenteId), agenteId)).IsSuccess),
            conv =>
            {
                Assert.Equal(agenteId, conv.AgenteAsignadoId);
                Assert.Equal(EstadoConversacionWhatsApp.EnAtencion, conv.Estado);
            });

        // Caso 2: Cambio a modo Humano durante inferencia con entidad ya rastreada en el DbContext del webhook
        await EjecutarCasoWebhookRastreadoAsync(
            "51987770002",
            "wamid.pg7.hum.2",
            async (waSvc, convId) => Assert.True((await waSvc.CambiarModoAtencionAsync(convId, new WhatsAppCambiarModoRequest(ModoAtencionWhatsApp.Humano), agenteId)).IsSuccess),
            conv =>
            {
                Assert.Equal(ModoAtencionWhatsApp.Humano, conv.ModoAtencion);
                Assert.Equal(EstadoConversacionWhatsApp.EnAtencion, conv.Estado);
                Assert.NotNull(conv.FechaCambioModoHumano);
            });

        // Caso 3: Cierre de conversación durante inferencia con entidad ya rastreada en el DbContext del webhook
        await EjecutarCasoWebhookRastreadoAsync(
            "51987770003",
            "wamid.pg7.cerr.3",
            async (waSvc, convId) => Assert.True((await waSvc.CambiarEstadoAsync(convId, new WhatsAppCambiarEstadoRequest(EstadoConversacionWhatsApp.Cerrada), agenteId)).IsSuccess),
            conv => Assert.Equal(EstadoConversacionWhatsApp.Cerrada, conv.Estado));
    }

    [Fact]
    public async Task PG08_Fase3_F3M03_AuditoriaEscalamientoAtomicaYRollbackCompletoEnPostgresReal()
    {
        var waMonitor = new StaticOptionsMonitor<WhatsAppSettings>(new WhatsAppSettings
        {
            Enabled = true,
            Provider = WhatsAppSettings.ProviderMock,
            DefaultCountryCode = "51"
        });
        var aiMonitor = new StaticOptionsMonitor<WhatsAppAiSettings>(new WhatsAppAiSettings
        {
            Enabled = true,
            Provider = WhatsAppAiSettings.ProviderMock
        });

        Guid convId;
        Guid msgEscalarId;
        await using (var seed = CrearContexto())
        {
            var conv = new ConversacionWhatsApp
            {
                TelefonoE164 = "+51987880001",
                ModoAtencion = ModoAtencionWhatsApp.Bot,
                Estado = EstadoConversacionWhatsApp.Abierta,
                Activo = true
            };
            var msg = new MensajeWhatsApp
            {
                Conversacion = conv,
                Direccion = DireccionMensajeWhatsApp.Entrante,
                ContenidoTexto = "Quiero hablar con un asesor humano",
                Activo = true
            };
            seed.ConversacionesWhatsApp.Add(conv);
            seed.MensajesWhatsApp.Add(msg);
            await seed.SaveChangesAsync();
            convId = conv.Id;
            msgEscalarId = msg.Id;
        }

        // 1. Provocar excepción justo antes del commit (cuando modo, estado, mensaje, Outbox y WHATSAPP_ESCALAR_HUMANO ya están preparados)
        var interceptorFallo = new FailOnEscalationSaveInterceptor { Habilitado = true };
        await using (var dbFallo = CrearContextoConInterceptor(interceptorFallo))
        {
            var crmConsulta = new WhatsAppCrmConsultaService(dbFallo, waMonitor, WhatsAppVerificadorIdentidadNoImplementado.Instancia, NullLogger<WhatsAppCrmConsultaService>.Instance);
            var facade = new WhatsAppAiCrmToolsFacade(dbFallo, crmConsulta);
            var outbox = new WhatsAppOutboxService(dbFallo, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitor);
            var esc = new WhatsAppEscalamientoService(dbFallo, new AuditoriaService(dbFallo), NullLogger<WhatsAppEscalamientoService>.Instance);
            var orq = new WhatsAppConversacionOrquestador(
                dbFallo,
                new WhatsAppIntentClassifier(),
                facade,
                new MockWhatsAppAiProvider(aiMonitor),
                esc,
                outbox,
                aiMonitor,
                waMonitor,
                NullLogger<WhatsAppConversacionOrquestador>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                orq.ProcesarMensajeEntranteAsync(convId, msgEscalarId));
        }

        Assert.True(interceptorFallo.DisparoExcepcion);

        // Verificar rollback completo y atómico en PostgreSQL real: 0 cambios en modo/estado, 0 mensajes, 0 Outbox, 0 auditorías.
        await using (var checkRollback = CrearContexto())
        {
            var convPostFallo = await checkRollback.ConversacionesWhatsApp.AsNoTracking().SingleAsync(c => c.Id == convId);
            Assert.Equal(ModoAtencionWhatsApp.Bot, convPostFallo.ModoAtencion);
            Assert.Equal(EstadoConversacionWhatsApp.Abierta, convPostFallo.Estado);
            Assert.Null(convPostFallo.FechaCambioModoHumano);

            Assert.Equal(0, await checkRollback.MensajesWhatsApp.CountAsync(m => m.ConversacionId == convId && m.Direccion == DireccionMensajeWhatsApp.Saliente));
            Assert.Equal(0, await checkRollback.OutboxMensajesWhatsApp.CountAsync(o => o.ConversacionId == convId));
            Assert.Equal(0, await checkRollback.EventosAuditoria.CountAsync(e => e.EntidadId == convId.ToString() && e.Accion == "WHATSAPP_ESCALAR_HUMANO"));
        }

        // 2. Caso exitoso: modo, estado, mensaje, Outbox y exactamente 1 evento WHATSAPP_ESCALAR_HUMANO se confirman en la misma transacción.
        await using (var dbOk = CrearContexto())
        {
            var crmConsulta = new WhatsAppCrmConsultaService(dbOk, waMonitor, WhatsAppVerificadorIdentidadNoImplementado.Instancia, NullLogger<WhatsAppCrmConsultaService>.Instance);
            var facade = new WhatsAppAiCrmToolsFacade(dbOk, crmConsulta);
            var outbox = new WhatsAppOutboxService(dbOk, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitor);
            var esc = new WhatsAppEscalamientoService(dbOk, new AuditoriaService(dbOk), NullLogger<WhatsAppEscalamientoService>.Instance);
            var orq = new WhatsAppConversacionOrquestador(
                dbOk,
                new WhatsAppIntentClassifier(),
                facade,
                new MockWhatsAppAiProvider(aiMonitor),
                esc,
                outbox,
                aiMonitor,
                waMonitor,
                NullLogger<WhatsAppConversacionOrquestador>.Instance);

            var resOk = await orq.ProcesarMensajeEntranteAsync(convId, msgEscalarId);
            Assert.Equal(EstadoOrquestacionWhatsApp.RespondidoYEscalado, resOk.Estado);
            Assert.True(resOk.EscaladoAHumano);

            // 3. Repetición del escalamiento -> no-op, cero auditorías duplicadas.
            var rep = await esc.EscalarAHumanoAsync(convId, "REPETIDO");
            Assert.False(rep.Escalada);
            Assert.True(rep.YaEstabaEnModoHumano);
        }

        await using (var checkOk = CrearContexto())
        {
            var convOk = await checkOk.ConversacionesWhatsApp.AsNoTracking().SingleAsync(c => c.Id == convId);
            Assert.Equal(ModoAtencionWhatsApp.Humano, convOk.ModoAtencion);
            Assert.Equal(EstadoConversacionWhatsApp.EnAtencion, convOk.Estado);
            Assert.NotNull(convOk.FechaCambioModoHumano);

            Assert.Equal(1, await checkOk.MensajesWhatsApp.CountAsync(m => m.ConversacionId == convId && m.Direccion == DireccionMensajeWhatsApp.Saliente));
            Assert.Equal(1, await checkOk.OutboxMensajesWhatsApp.CountAsync(o => o.ConversacionId == convId));

            var eventos = await checkOk.EventosAuditoria
                .AsNoTracking()
                .Where(e => e.EntidadId == convId.ToString() && e.Accion == "WHATSAPP_ESCALAR_HUMANO")
                .ToListAsync();
            var unico = Assert.Single(eventos);
            Assert.DoesNotContain("+51987880001", unico.Detalle ?? string.Empty);
        }
    }

    private sealed class FailOnActionAuditInterceptor(string accionObjetivo, Action? antesDeLanzar = null) : SaveChangesInterceptor
    {
        public bool Habilitado { get; set; } = true;
        public bool Disparo { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Habilitado && eventData.Context is not null)
            {
                var coincide = eventData.Context.ChangeTracker
                    .Entries<EventoAuditoria>()
                    .Any(e => e.State == EntityState.Added && e.Entity.Accion == accionObjetivo);

                if (coincide)
                {
                    Disparo = true;
                    antesDeLanzar?.Invoke();
                    throw new InvalidOperationException($"Fallo simulado para {accionObjetivo}.");
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task PG09_Fase3_MED01_WebhookReutilizaDbContextTrasRollbackYCancelacion_NoResucitaEntidadesRevertidasEnPostgresReal()
    {
        var waMonitor = new StaticOptionsMonitor<WhatsAppSettings>(new WhatsAppSettings
        {
            Enabled = true,
            Provider = WhatsAppSettings.ProviderMock,
            DefaultCountryCode = "51"
        });
        var aiMonitor = new StaticOptionsMonitor<WhatsAppAiSettings>(new WhatsAppAiSettings
        {
            Enabled = true,
            Provider = WhatsAppAiSettings.ProviderMock
        });

        static byte[] CrearPayloadTexto(string wamid, string from, string body) =>
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        changes = new[]
                        {
                            new
                            {
                                field = "messages",
                                value = new
                                {
                                    contacts = new[] { new { wa_id = from, profile = new { name = "Cliente PG9" } } },
                                    messages = new[]
                                    {
                                        new
                                        {
                                            from,
                                            id = wamid,
                                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                                            type = "text",
                                            text = new { body }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }));

        // 1. Flujo real: WhatsAppWebhookService -> WhatsAppAiAutoResponder -> WhatsAppConversacionOrquestador
        //    -> InvalidOperationException (no DbUpdateException) en SaveChangesAsync del escalamiento
        //    -> AutoResponder absorbe el error -> Webhook ejecuta RegistrarEventoWebhookAsync (SaveChangesAsync) en el MISMO DbContext scoped.
        var interceptorFallo = new FailOnEscalationSaveInterceptor { Habilitado = true };
        await using (var dbScoped = CrearContextoConInterceptor(interceptorFallo))
        {
            var identSvc = new WhatsAppClienteIdentificacionService(dbScoped, waMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
            var crmConsulta = new WhatsAppCrmConsultaService(dbScoped, waMonitor, WhatsAppVerificadorIdentidadNoImplementado.Instancia, NullLogger<WhatsAppCrmConsultaService>.Instance);
            var facade = new WhatsAppAiCrmToolsFacade(dbScoped, crmConsulta);
            var outbox = new WhatsAppOutboxService(dbScoped, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitor);
            var esc = new WhatsAppEscalamientoService(dbScoped, new AuditoriaService(dbScoped), NullLogger<WhatsAppEscalamientoService>.Instance);
            var orq = new WhatsAppConversacionOrquestador(
                dbScoped,
                new WhatsAppIntentClassifier(),
                facade,
                new MockWhatsAppAiProvider(aiMonitor),
                esc,
                outbox,
                aiMonitor,
                waMonitor,
                NullLogger<WhatsAppConversacionOrquestador>.Instance);
            var autoResponder = new WhatsAppAiAutoResponder(orq, aiMonitor, waMonitor, NullLogger<WhatsAppAiAutoResponder>.Instance);
            var webhook = new WhatsAppWebhookService(dbScoped, identSvc, autoResponder, waMonitor, NullLogger<WhatsAppWebhookService>.Instance);

            var res = await webhook.ProcesarNotificacionAsync(CrearPayloadTexto("wamid.pg9.med01.1", "51987990001", "Quiero hablar con un asesor"));
            Assert.True(res.EsValido);
            Assert.Equal(1, res.MensajesProcesados);
            Assert.True(interceptorFallo.DisparoExcepcion);

            // Confirmar que no quedó ninguna entidad residual Added/Modified en el ChangeTracker del mismo DbContext
            Assert.DoesNotContain(dbScoped.ChangeTracker.Entries(), e => e.State is EntityState.Added or EntityState.Modified);
        }

        // Verificar en PostgreSQL desde otro contexto: entrante y webhook SÍ se conservaron; escalamiento, mensaje bot, Outbox y auditoría NO resucitaron.
        await using (var check = CrearContexto())
        {
            var conv = await check.ConversacionesWhatsApp.AsNoTracking().SingleAsync(c => c.TelefonoE164 == "+51987990001");
            Assert.Equal(ModoAtencionWhatsApp.Bot, conv.ModoAtencion);
            Assert.Equal(EstadoConversacionWhatsApp.Abierta, conv.Estado);
            Assert.Null(conv.FechaCambioModoHumano);

            Assert.Equal(1, await check.MensajesWhatsApp.CountAsync(m => m.ConversacionId == conv.Id && m.ExternalMessageId == "wamid.pg9.med01.1"));
            Assert.Equal(1, await check.EventosWebhookWhatsApp.CountAsync(e => e.ExternalMessageId == "wamid.pg9.med01.1"));
            Assert.Equal(0, await check.MensajesWhatsApp.CountAsync(m => m.ConversacionId == conv.Id && m.Direccion == DireccionMensajeWhatsApp.Saliente));
            Assert.Equal(0, await check.OutboxMensajesWhatsApp.CountAsync(o => o.ConversacionId == conv.Id));
            Assert.Equal(0, await check.EventosAuditoria.CountAsync(e => e.EntidadId == conv.Id.ToString() && e.Accion == "WHATSAPP_ESCALAR_HUMANO"));
        }

        // 2. Cancelación durante SaveChangesAsync: rollback con CancellationToken.None + limpieza del ChangeTracker
        Guid convCancelId;
        Guid msgCancelId;
        await using (var seed = CrearContexto())
        {
            var conv = new ConversacionWhatsApp { TelefonoE164 = "+51987990002", ModoAtencion = ModoAtencionWhatsApp.Bot, Estado = EstadoConversacionWhatsApp.Abierta, Activo = true };
            var msg = new MensajeWhatsApp { Conversacion = conv, Direccion = DireccionMensajeWhatsApp.Entrante, ContenidoTexto = "Quiero hablar con un asesor", Activo = true };
            seed.ConversacionesWhatsApp.Add(conv);
            seed.MensajesWhatsApp.Add(msg);
            await seed.SaveChangesAsync();
            convCancelId = conv.Id;
            msgCancelId = msg.Id;
        }

        using (var cts = new CancellationTokenSource())
        {
            var cancelInterceptor = new FailOnActionAuditInterceptor("WHATSAPP_ESCALAR_HUMANO", () =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
            });
            await using var dbCancel = CrearContextoConInterceptor(cancelInterceptor);
            var crmConsulta = new WhatsAppCrmConsultaService(dbCancel, waMonitor, WhatsAppVerificadorIdentidadNoImplementado.Instancia, NullLogger<WhatsAppCrmConsultaService>.Instance);
            var facade = new WhatsAppAiCrmToolsFacade(dbCancel, crmConsulta);
            var outbox = new WhatsAppOutboxService(dbCancel, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitor);
            var esc = new WhatsAppEscalamientoService(dbCancel, new AuditoriaService(dbCancel), NullLogger<WhatsAppEscalamientoService>.Instance);
            var orq = new WhatsAppConversacionOrquestador(
                dbCancel,
                new WhatsAppIntentClassifier(),
                facade,
                new MockWhatsAppAiProvider(aiMonitor),
                esc,
                outbox,
                aiMonitor,
                waMonitor,
                NullLogger<WhatsAppConversacionOrquestador>.Instance);

            var rCancel = await orq.ProcesarMensajeEntranteAsync(convCancelId, msgCancelId, cts.Token);
            Assert.Equal(EstadoOrquestacionWhatsApp.Omitido, rCancel.Estado);
            Assert.Equal("CANCELLED", rCancel.MotivoCodigo);
            Assert.DoesNotContain(dbCancel.ChangeTracker.Entries(), e => e.State is EntityState.Added or EntityState.Modified);
        }

        await using (var checkCancel = CrearContexto())
        {
            var convPost = await checkCancel.ConversacionesWhatsApp.AsNoTracking().SingleAsync(c => c.Id == convCancelId);
            Assert.Equal(ModoAtencionWhatsApp.Bot, convPost.ModoAtencion);
            Assert.Equal(EstadoConversacionWhatsApp.Abierta, convPost.Estado);
            Assert.Equal(0, await checkCancel.MensajesWhatsApp.CountAsync(m => m.ConversacionId == convCancelId && m.Direccion == DireccionMensajeWhatsApp.Saliente));
            Assert.Equal(0, await checkCancel.OutboxMensajesWhatsApp.CountAsync(o => o.ConversacionId == convCancelId));
            Assert.Equal(0, await checkCancel.EventosAuditoria.CountAsync(e => e.EntidadId == convCancelId.ToString() && e.Accion == "WHATSAPP_ESCALAR_HUMANO"));
        }

        // 3. LOW-01: Atomicidad y rollback de AsignarAgenteAsync y CambiarEstadoAsync con sus eventos de auditoría en PostgreSQL
        Guid agenteId;
        await using (var seedAg = CrearContexto())
        {
            var ag = new Usuario { NombreCompleto = "Agente PG9", Email = "agente.pg9@t.local", Activo = true };
            seedAg.Usuarios.Add(ag);
            await seedAg.SaveChangesAsync();
            agenteId = ag.Id;
        }

        var failAsig = new FailOnActionAuditInterceptor("WHATSAPP_ASIGNAR_AGENTE");
        await using (var dbFailAsig = CrearContextoConInterceptor(failAsig))
        {
            var ident = new WhatsAppClienteIdentificacionService(dbFailAsig, waMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
            var outbox = new WhatsAppOutboxService(dbFailAsig, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitor);
            var waSvc = new WhatsAppService(dbFailAsig, outbox, ident, new AuditoriaService(dbFailAsig), waMonitor);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                waSvc.AsignarAgenteAsync(convCancelId, new WhatsAppAsignarAgenteRequest(agenteId), agenteId));
        }

        await using (var checkAsigRollback = CrearContexto())
        {
            var convPost = await checkAsigRollback.ConversacionesWhatsApp.AsNoTracking().SingleAsync(c => c.Id == convCancelId);
            Assert.Null(convPost.AgenteAsignadoId);
            Assert.Equal(EstadoConversacionWhatsApp.Abierta, convPost.Estado);
            Assert.Equal(0, await checkAsigRollback.EventosAuditoria.CountAsync(e => e.EntidadId == convCancelId.ToString() && e.Accion == "WHATSAPP_ASIGNAR_AGENTE"));
        }

        await using (var dbOkAsig = CrearContexto())
        {
            var ident = new WhatsAppClienteIdentificacionService(dbOkAsig, waMonitor, NullLogger<WhatsAppClienteIdentificacionService>.Instance);
            var outbox = new WhatsAppOutboxService(dbOkAsig, new MockWhatsAppProvider(NullLogger<MockWhatsAppProvider>.Instance), waMonitor, NullLogger<WhatsAppOutboxService>.Instance, aiMonitor);
            var waSvc = new WhatsAppService(dbOkAsig, outbox, ident, new AuditoriaService(dbOkAsig), waMonitor);
            Assert.True((await waSvc.AsignarAgenteAsync(convCancelId, new WhatsAppAsignarAgenteRequest(agenteId), agenteId)).IsSuccess);
            Assert.True((await waSvc.CambiarEstadoAsync(convCancelId, new WhatsAppCambiarEstadoRequest(EstadoConversacionWhatsApp.Cerrada), agenteId)).IsSuccess);
        }

        await using (var checkAsigOk = CrearContexto())
        {
            Assert.Equal(1, await checkAsigOk.EventosAuditoria.CountAsync(e => e.EntidadId == convCancelId.ToString() && e.Accion == "WHATSAPP_ASIGNAR_AGENTE"));
            Assert.Equal(1, await checkAsigOk.EventosAuditoria.CountAsync(e => e.EntidadId == convCancelId.ToString() && e.Accion == "WHATSAPP_CAMBIAR_ESTADO"));
        }
    }
}
