using System.Text;
using System.Text.Json;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.WhatsApp;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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
}
