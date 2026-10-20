using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Fixture de integración para pruebas contra PostgreSQL real.
/// Aislamiento obligatorio: SOLO está autorizado a conectarse a la base local 'crm_teambenavides_qa'.
/// Lanza InvalidOperationException si la base no es 'crm_teambenavides_qa' o si el host no es local.
/// </summary>
public class PostgresQaFixture : IAsyncLifetime
{
    public const string DatabaseEsperada = "crm_teambenavides_qa";
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // 1. Obtener cadena de conexión (prioridad: TEST_POSTGRES_CONNECTION -> UserSecrets -> appsettings.json)
        var envConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");

        string connectionString;
        if (!string.IsNullOrWhiteSpace(envConn))
        {
            connectionString = envConn;
        }
        else
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets(typeof(CRMTeamBenavides.Data.ApplicationDbContext).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();

            var baseConn = config.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=crm_teambenavides_qa;Username=postgres;Password=";

            var builder = new NpgsqlConnectionStringBuilder(baseConn)
            {
                Database = DatabaseEsperada
            };
            connectionString = builder.ConnectionString;
        }

        // 2. Guardia de seguridad estricta antes de abrir cualquier conexión
        ValidarAislamientoSeguro(connectionString);

        ConnectionString = connectionString;

        // 3. Asegurar esquema actualizado con migraciones EF Core en crm_teambenavides_qa
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        // 4. Limpieza inicial para comenzar con base de pruebas limpia
        await LimpiarDatosDePruebaAsync();
    }

    /// <summary>
    /// Valida que la cadena de conexión apunte única y exclusivamente a la base de pruebas local 'crm_teambenavides_qa'.
    /// Nunca permite 'crm_teambenavides' ni hosts remotos.
    /// </summary>
    public static void ValidarAislamientoSeguro(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("[SEGURIDAD ESTRICTA] La cadena de conexión a PostgreSQL está vacía.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var dbName = builder.Database?.Trim();
        var host = builder.Host?.Trim().ToLowerInvariant();

        if (!string.Equals(dbName, DatabaseEsperada, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"[SEGURIDAD ESTRICTA] Base de datos no autorizada: '{dbName}'. " +
                $"Las pruebas de integración están terminantemente prohibidas en bases distintas de '{DatabaseEsperada}'.");
        }

        if (host != "localhost" && host != "127.0.0.1" && host != "::1")
        {
            throw new InvalidOperationException(
                $"[SEGURIDAD ESTRICTA] Host no autorizado: '{host}'. " +
                "Las pruebas de integración solo pueden ejecutarse contra PostgreSQL local (localhost / 127.0.0.1).");
        }
    }

    public ApplicationDbContext CreateDbContext(Action<DbContextOptionsBuilder<ApplicationDbContext>>? configure = null)
    {
        ValidarAislamientoSeguro(ConnectionString);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString);

        configure?.Invoke(optionsBuilder);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    public async Task LimpiarDatosDePruebaAsync()
    {
        ValidarAislamientoSeguro(ConnectionString);

        await using var context = CreateDbContext();
        await using var tx = await context.Database.BeginTransactionAsync();

        // Limpieza de tablas específicas de Yamaha y entidades de prueba aisladas
        await context.Database.ExecuteSqlRawAsync(@"
            DELETE FROM ""YamahaSyncLogs"";
            DELETE FROM ""DetallesVenta"";
            DELETE FROM ""Comprobantes"";
            DELETE FROM ""Pagos"";
            DELETE FROM ""Ventas"";
            DELETE FROM ""Productos"" WHERE ""Codigo"" IN ('90793-AJ412', '2DP-E4451-00');
            DELETE FROM ""CategoriasProducto"" WHERE ""Nombre"" = 'Repuestos Yamaha';
            DELETE FROM ""Clientes"" WHERE ""NumeroDocumento"" IN ('10203040');
        ");

        await tx.CommitAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            await LimpiarDatosDePruebaAsync();
        }
        catch
        {
            // Ignorar errores en desecho
        }
    }
}

[CollectionDefinition("PostgresIntegrationTests")]
public class PostgresIntegrationCollection : ICollectionFixture<PostgresQaFixture>
{
}
