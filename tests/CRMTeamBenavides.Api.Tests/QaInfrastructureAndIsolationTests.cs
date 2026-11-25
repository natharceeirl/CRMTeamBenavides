using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CRMTeamBenavides.Api.Configuration;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Suite automatizada de verificación estática y funcional de infraestructura QA, aislamiento
/// respecto de producción, validación de configuración (placeholders, entropía, Staging),
/// fixture de datos sintéticos y ejecución funcional de actualizar-qa.sh / respaldo-qa.sh
/// en repositorios Git temporales aislados con mocks controlados de Docker y curl.
/// </summary>
public class QaInfrastructureAndIsolationTests
{
    private sealed class HostEnvironmentSimulado(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "CRMTeamBenavides.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private const string JwtValidoQa =
        "9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a1f0e9d8c7b6a5f4e3d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a";

    private const string DbPassValidaQa =
        "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2";

    private static string ObtenerRaizRepositorio()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null && !File.Exists(Path.Combine(actual.FullName, "CRMTeamBenavides.slnx")))
        {
            actual = actual.Parent;
        }

        return actual?.FullName
            ?? throw new InvalidOperationException("No se pudo localizar la raíz del repositorio CRMTeamBenavides.");
    }

    private static string LeerArchivoRepo(string rutaRelativa)
    {
        var rutaCompleta = Path.Combine(ObtenerRaizRepositorio(), rutaRelativa.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(rutaCompleta), $"Debe existir el archivo: {rutaRelativa}");
        return File.ReadAllText(rutaCompleta);
    }

    private static ApplicationDbContext CrearContextoEnMemoria()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(opciones);
    }

    private static Dictionary<string, string?> CrearConfiguracionStagingValida() =>
        new()
        {
            ["JwtSettings:SecretKey"] = JwtValidoQa,
            ["ConnectionStrings:DefaultConnection"] =
                $"Host=db;Port=5432;Database=crm_teambenavides_qa;Username=crm_qa;Password={DbPassValidaQa};Gss Encryption Mode=Disable",
            ["Proxy:RedesConfiables:0"] = "172.31.0.0/24",
            ["WhatsApp:Enabled"] = "false",
            ["WhatsApp:Provider"] = "Mock",
            ["WhatsAppAi:Enabled"] = "false",
            ["WhatsAppAi:Provider"] = "Mock",
            ["YamahaApi:Enabled"] = "false"
        };

    // =========================================================================
    // 1. Docker Compose QA vs Producción: aislamiento de proyecto, BD, red, volúmenes y puertos
    // =========================================================================

    [Fact]
    public void QA_01_DockerComposeQa_ProyectoVolumenesRedesYBaseDatosTotalmenteAisladosDeProduccion()
    {
        var composeProd = LeerArchivoRepo("despliegue/docker-compose.yml");
        var composeQa = LeerArchivoRepo("despliegue/docker-compose.qa.yml");

        Assert.Contains("name: crm-team-benavides", composeProd);
        Assert.Contains("name: crm-team-benavides-qa", composeQa);
        Assert.DoesNotContain("\nname: crm-team-benavides\n", composeQa.Replace("\r\n", "\n"));

        Assert.Contains("\n  db:\n", composeQa.Replace("\r\n", "\n"));
        Assert.Contains("\n  api:\n", composeQa.Replace("\r\n", "\n"));
        Assert.Contains("\n  respaldo:\n", composeQa.Replace("\r\n", "\n"));
        Assert.Contains("\n  caddy:\n", composeQa.Replace("\r\n", "\n"));

        Assert.Contains("image: postgres:18", composeQa);
        Assert.Contains("POSTGRES_DB: ${POSTGRES_DB:-crm_teambenavides_qa}", composeQa);
        Assert.Contains("POSTGRES_USER: ${POSTGRES_USER:-crm_qa}", composeQa);
        Assert.DoesNotContain(":-crm_teambenavides}", composeQa);
        Assert.DoesNotContain(":-crm}", composeQa);

        Assert.Contains("name: crm-team-benavides-qa_interna", composeQa);
        Assert.Contains("172.31.0.0/24", composeQa);
        Assert.DoesNotContain("172.30.0.0/24", composeQa);

        Assert.Contains("name: crm-team-benavides-qa_base", composeQa);
        Assert.Contains("name: crm-team-benavides-qa_datos-api", composeQa);
        Assert.Contains("name: crm-team-benavides-qa_caddy-datos", composeQa);
        Assert.Contains("name: crm-team-benavides-qa_caddy-config", composeQa);

        // Respaldos exclusivos en carpeta ./respaldos-qa con script respaldo-qa.sh y patrón crm-qa-*.dump
        Assert.Contains("./respaldo-qa.sh:/respaldo-qa.sh:ro", composeQa);
        Assert.Contains("./respaldos-qa:/respaldos", composeQa);
        Assert.DoesNotContain("./respaldos:/respaldos", composeQa);
        Assert.Contains("crm-qa-*.dump", composeQa);
        Assert.Contains("RETENCION_DIAS: ${RETENCION_DIAS:-7}", composeQa);
    }

    [Fact]
    public void QA_02_DockerComposeQa_NoExponePostgresNiApiAlHost_SoloCaddyPublicaPuertos()
    {
        var composeQa = LeerArchivoRepo("despliegue/docker-compose.qa.yml").Replace("\r\n", "\n");

        var coincidenciasPorts = Regex.Matches(composeQa, @"^\s+ports:\s*$", RegexOptions.Multiline);
        Assert.Single(coincidenciasPorts);

        var indiceCaddy = composeQa.IndexOf("\n  caddy:\n", StringComparison.Ordinal);
        Assert.True(indiceCaddy > 0);
        Assert.True(coincidenciasPorts[0].Index > indiceCaddy);

        Assert.Contains("\"${PUERTO_HTTP:-80}:80\"", composeQa);
        Assert.Contains("\"${PUERTO_HTTPS:-443}:443\"", composeQa);
        Assert.Contains("./Caddyfile.qa:/etc/caddy/Caddyfile:ro", composeQa);
    }

    [Fact]
    public void QA_03_DockerComposeQa_EntornoStagingYFlagsDeWhatsAppIaYamahaDeshabilitadosPorDefecto()
    {
        var composeQa = LeerArchivoRepo("despliegue/docker-compose.qa.yml");

        Assert.Contains("ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Staging}", composeQa);
        Assert.Contains("WhatsApp__Enabled: ${WHATSAPP_ENABLED:-false}", composeQa);
        Assert.Contains("WhatsApp__Provider: ${WHATSAPP_PROVIDER:-Mock}", composeQa);
        Assert.Contains("WhatsAppAi__Enabled: ${WHATSAPP_AI_ENABLED:-false}", composeQa);
        Assert.Contains("WhatsAppAi__Provider: ${WHATSAPP_AI_PROVIDER:-Mock}", composeQa);
        Assert.Contains("YamahaApi__Enabled: ${YAMAHA_ENABLED:-false}", composeQa);
    }

    // =========================================================================
    // 2. Caddyfile.qa y .env.qa.ejemplo
    // =========================================================================

    [Fact]
    public void QA_04_CaddyfileQa_DominioParametrizado_NoIndex_ProxyApi_SinBasicAuthGlobal()
    {
        var caddyQa = LeerArchivoRepo("despliegue/Caddyfile.qa");

        Assert.Contains("{$DOMINIO}", caddyQa);
        Assert.DoesNotContain("app.teambenavides.com", caddyQa);
        Assert.Contains("handle /api/*", caddyQa);
        Assert.Contains("reverse_proxy api:8080", caddyQa);
        Assert.Contains("try_files {path} /index.html", caddyQa);
        Assert.Contains("X-Robots-Tag \"noindex, nofollow\"", caddyQa);
        Assert.DoesNotContain("basicauth", caddyQa, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("basic_auth", caddyQa, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QA_05_EnvQaEjemplo_IncluyeComposeFileYProjectNameObligatorios_SinSecretosReales()
    {
        var envQa = LeerArchivoRepo("despliegue/.env.qa.ejemplo");

        Assert.Contains("COMPOSE_FILE=docker-compose.qa.yml", envQa);
        Assert.Contains("COMPOSE_PROJECT_NAME=crm-team-benavides-qa", envQa);
        Assert.Contains("DOMINIO=REEMPLAZAR_CON_DOMINIO_QA", envQa);
        Assert.Contains("POSTGRES_DB=crm_teambenavides_qa", envQa);
        Assert.Contains("POSTGRES_USER=crm_qa", envQa);
        Assert.Contains("POSTGRES_PASSWORD=REEMPLAZAR_CON_CLAVE_DB_QA_HEX_32", envQa);
        Assert.Contains("ASPNETCORE_ENVIRONMENT=Staging", envQa);
        Assert.Contains("JWT_SECRET_KEY=REEMPLAZAR_CON_CLAVE_JWT_QA_HEX_48", envQa);
        Assert.Contains("RED_INTERNA_QA=172.31.0.0/24", envQa);
        Assert.Contains("RETENCION_DIAS=7", envQa);
        Assert.Contains("YAMAHA_ENABLED=false", envQa);
        Assert.Contains("WHATSAPP_ENABLED=false", envQa);
        Assert.Contains("WHATSAPP_PROVIDER=Mock", envQa);
        Assert.Contains("WHATSAPP_AI_ENABLED=false", envQa);
        Assert.Contains("WHATSAPP_AI_PROVIDER=Mock", envQa);

        var raiz = ObtenerRaizRepositorio();
        Assert.False(File.Exists(Path.Combine(raiz, "despliegue", ".env")));
        Assert.False(File.Exists(Path.Combine(raiz, "despliegue", ".env.qa")));
    }

    // =========================================================================
    // 3. Scripts preparar-servidor-qa.sh, actualizar-qa.sh y respaldo-qa.sh
    // =========================================================================

    [Fact]
    public void QA_06_ScriptsShellQa_ContienenCuatroModos_PatronRespaldoQa_YTrampasDeLimpieza()
    {
        var prepararQa = LeerArchivoRepo("despliegue/preparar-servidor-qa.sh");
        var actualizarQa = LeerArchivoRepo("despliegue/actualizar-qa.sh");
        var respaldoQa = LeerArchivoRepo("despliegue/respaldo-qa.sh");

        Assert.StartsWith("#!/bin/sh", prepararQa);
        Assert.Contains("set -eu", prepararQa);
        Assert.Contains("RAMA=develop", prepararQa);
        Assert.Contains("DESTINO=/opt/crm-team-benavides-qa", prepararQa);
        Assert.DoesNotContain("ssh-keygen", prepararQa);

        Assert.StartsWith("#!/bin/sh", actualizarQa);
        Assert.Contains("set -eu", actualizarQa);
        Assert.Contains("trap limpiar_temporales EXIT INT TERM HUP", actualizarQa);
        Assert.Contains("validar | generar-sql | desplegar)", actualizarQa);
        Assert.Contains("migrar)", actualizarQa);
        Assert.Contains("git fetch --quiet origin develop", actualizarQa);
        Assert.DoesNotContain("origin/main", actualizarQa);
        Assert.DoesNotContain("refs/heads/main", actualizarQa);
        Assert.Contains("compose_qa build api", actualizarQa);
        Assert.Contains("compose_qa build caddy", actualizarQa);
        Assert.DoesNotContain("up -d --build", actualizarQa);

        Assert.StartsWith("#!/bin/sh", respaldoQa);
        Assert.Contains("crm-qa-*.dump", respaldoQa);
        Assert.Contains("antes-de-restaurar-qa", respaldoQa);
        Assert.Contains("BASE_QA_ESPERADA=crm_teambenavides_qa", respaldoQa);
    }

    // =========================================================================
    // 4. Workflow GitHub Actions desplegar-qa.yml
    // =========================================================================

    [Fact]
    public void QA_07_WorkflowDesplegarQa_DisparoPorEtiquetasQa_ValidaOriginDevelop_SinMigraciones()
    {
        var workflowQa = LeerArchivoRepo(".github/workflows/desplegar-qa.yml");

        Assert.Contains("qa-validar-*", workflowQa);
        Assert.Contains("qa-desplegar-*", workflowQa);
        Assert.Contains("workflow_dispatch:", workflowQa);
        Assert.DoesNotContain("\n  pull_request:", workflowQa);

        // No permite ejecutar modo migrar desde el workflow
        Assert.DoesNotContain("- migrar", workflowQa);
        Assert.Contains("refs/remotes/origin/develop", workflowQa);
        Assert.DoesNotContain("refs/heads/main", workflowQa);
        Assert.Contains("environment: qa", workflowQa);

        Assert.Contains("secrets.QA_SERVIDOR_HOST", workflowQa);
        Assert.Contains("secrets.QA_SERVIDOR_SSH_CLAVE", workflowQa);
        Assert.Contains("secrets.QA_SERVIDOR_SSH_HUELLA", workflowQa);
        Assert.Contains("vars.QA_DOMINIO", workflowQa);
        Assert.Contains("--dominio-esperado=$QA_DOM", workflowQa);

        Assert.DoesNotContain("secrets.SERVIDOR_HOST", workflowQa);
        Assert.DoesNotContain("secrets.SERVIDOR_SSH_CLAVE", workflowQa);
        Assert.DoesNotContain("secrets.SERVIDOR_SSH_HUELLA", workflowQa);
    }

    // =========================================================================
    // 5. Validación de configuración de arranque (ValidacionConfiguracionEntorno)
    // =========================================================================

    [Fact]
    public void QA_08_ValidacionConfiguracionEntorno_EnStaging_AceptaConfiguracionQaValida_IncluyendoSubcadenaXxxxAleatoria()
    {
        var env = new HostEnvironmentSimulado(Environments.Staging);
        var datos = CrearConfiguracionStagingValida();
        // Secreto aleatorio legítimo que contiene "xxxx" no debe producir falso positivo (QA-FIN-LOW-02)
        datos["JwtSettings:SecretKey"] = "9f8e7d6c5b4axxxx1d0c9b8a7f6e5d4c3b2a1f0e9d8c7b6a5f4e3d2c1b0a9f8e";

        var config = new ConfigurationBuilder().AddInMemoryCollection(datos).Build();

        var ex = Record.Exception(() => ValidacionConfiguracionEntorno.ValidarAlArrancar(env, config));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("REEMPLAZAR_CON_CLAVE_JWT_QA_HEX_48_BYTES_COMPLETOS")]
    [InlineData("CAMBIAR_ESTA_CLAVE_POR_UNA_REAL_DE_48_BYTES_MINIMO")]
    [InlineData("PLACEHOLDER_SECRET_KEY_123456789012345678901234567890")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("123456789012345678901234567890123456789012345678")]
    [InlineData("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6")] // 32 chars < 48 exigidos para JWT
    [InlineData("")]
    public void QA_09_ValidacionConfiguracionEntorno_RechazaJwtSecretPlaceholderCortoORepetitivo_SinExponerlo(
        string jwtSecretInvalido)
    {
        var env = new HostEnvironmentSimulado(Environments.Staging);
        var datos = CrearConfiguracionStagingValida();
        datos["JwtSettings:SecretKey"] = jwtSecretInvalido;

        var config = new ConfigurationBuilder().AddInMemoryCollection(datos).Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ValidacionConfiguracionEntorno.ValidarAlArrancar(env, config));
        Assert.Contains("JwtSettings:SecretKey", ex.Message);
        if (!string.IsNullOrWhiteSpace(jwtSecretInvalido))
        {
            Assert.DoesNotContain(jwtSecretInvalido, ex.Message);
        }
    }

    [Theory]
    [InlineData("Host=db;Port=5432;Database=crm_teambenavides_qa;Username=crm_qa;Password=REEMPLAZAR_CON_CLAVE_DB_QA_HEX_32", "DefaultConnection")]
    [InlineData("Host=db;Port=5432;Database=crm_teambenavides_qa;Username=crm_qa;Password=a1b2c3d4e5f6a7b8", "contraseña de PostgreSQL")]
    [InlineData("Host=db;Port=5432;Database=crm_teambenavides_qa;Username=crm_qa;Password=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "contraseña de PostgreSQL")]
    [InlineData("Host=db;Port=5432;Database=crm_teambenavides;Username=crm_qa;Password=a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6", "Violación de aislamiento QA")]
    [InlineData("Host=db;Port=5432;Database=crm_otra_base;Username=crm_qa;Password=a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6", "Violación de aislamiento QA")]
    [InlineData("Host=db;Port=5432;Database=crm_teambenavides_qa;Username=crm;Password=a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6", "Violación de aislamiento QA")]
    [InlineData("Host=db;Port=5432;Database=crm_teambenavides_qa;Username=postgres;Password=a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6", "Violación de aislamiento QA")]
    public void QA_10_ValidacionConfiguracionEntorno_EnStaging_RechazaPlaceholdersClavesDebilesYColisionesDeBaseOUsuario(
        string cadenaConexionInvalida,
        string fragmentoEsperado)
    {
        var env = new HostEnvironmentSimulado(Environments.Staging);
        var datos = CrearConfiguracionStagingValida();
        datos["ConnectionStrings:DefaultConnection"] = cadenaConexionInvalida;

        var config = new ConfigurationBuilder().AddInMemoryCollection(datos).Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ValidacionConfiguracionEntorno.ValidarAlArrancar(env, config));
        Assert.Contains(fragmentoEsperado, ex.Message);
    }

    [Fact]
    public void QA_11_ValidacionConfiguracionEntorno_EnStaging_ExigeProxyValido_YRechazaActivacionDeWhatsAppIaOYamaha()
    {
        var env = new HostEnvironmentSimulado(Environments.Staging);

        // 1. Proxy vacío
        var datosSinProxy = CrearConfiguracionStagingValida();
        datosSinProxy.Remove("Proxy:RedesConfiables:0");
        Assert.Throws<InvalidOperationException>(() =>
            ValidacionConfiguracionEntorno.ValidarAlArrancar(
                env, new ConfigurationBuilder().AddInMemoryCollection(datosSinProxy).Build()));

        // 2. Subred de producción en Proxy
        var datosSubredProd = CrearConfiguracionStagingValida();
        datosSubredProd["Proxy:RedesConfiables:0"] = "172.30.0.0/24";
        Assert.Throws<InvalidOperationException>(() =>
            ValidacionConfiguracionEntorno.ValidarAlArrancar(
                env, new ConfigurationBuilder().AddInMemoryCollection(datosSubredProd).Build()));

        // 3. WhatsApp habilitado o Provider=Meta en Staging
        var datosWaActivo = CrearConfiguracionStagingValida();
        datosWaActivo["WhatsApp:Enabled"] = "true";
        Assert.Throws<InvalidOperationException>(() =>
            ValidacionConfiguracionEntorno.ValidarAlArrancar(
                env, new ConfigurationBuilder().AddInMemoryCollection(datosWaActivo).Build()));

        var datosWaMeta = CrearConfiguracionStagingValida();
        datosWaMeta["WhatsApp:Provider"] = "Meta";
        Assert.Throws<InvalidOperationException>(() =>
            ValidacionConfiguracionEntorno.ValidarAlArrancar(
                env, new ConfigurationBuilder().AddInMemoryCollection(datosWaMeta).Build()));

        // 4. WhatsAppAi habilitada o Provider!=Mock en Staging
        var datosAiActiva = CrearConfiguracionStagingValida();
        datosAiActiva["WhatsAppAi:Enabled"] = "true";
        Assert.Throws<InvalidOperationException>(() =>
            ValidacionConfiguracionEntorno.ValidarAlArrancar(
                env, new ConfigurationBuilder().AddInMemoryCollection(datosAiActiva).Build()));

        // 5. Yamaha habilitado en Staging
        var datosYamahaActivo = CrearConfiguracionStagingValida();
        datosYamahaActivo["YamahaApi:Enabled"] = "true";
        Assert.Throws<InvalidOperationException>(() =>
            ValidacionConfiguracionEntorno.ValidarAlArrancar(
                env, new ConfigurationBuilder().AddInMemoryCollection(datosYamahaActivo).Build()));
    }

    // =========================================================================
    // 6. Fixture de datos sintéticos de QA (QaSyntheticDataFixture — QA-FIN-MED-04)
    // =========================================================================

    [Fact]
    public async Task QA_12_QaSyntheticDataFixture_SiembraIdempotente_ConPasswordHashRealVerificableYSinPii()
    {
        using var db = CrearContextoEnMemoria();

        var primera = await QaSyntheticDataFixture.SembrarAsync(db);
        var segunda = await QaSyntheticDataFixture.SembrarAsync(db);

        Assert.Equal(primera, segunda);

        Assert.Equal(4, await db.Usuarios.CountAsync());
        Assert.True(await db.Roles.CountAsync() >= 5);
        Assert.Equal(2, await db.Clientes.CountAsync());
        Assert.Equal(2, await db.Productos.CountAsync());
        Assert.Equal(2, await db.Vehiculos.CountAsync());
        Assert.Equal(1, await db.OrdenesServicio.CountAsync());
        Assert.Equal(1, await db.Ventas.CountAsync());
        Assert.Equal(2, await db.ConversacionesWhatsApp.CountAsync());
        Assert.Equal(3, await db.MensajesWhatsApp.CountAsync());
        Assert.Equal(1, await db.OutboxMensajesWhatsApp.CountAsync());

        // Verificar que los usuarios tengan un PasswordHash PBKDF2 real y válido para ClaveSinteticaQa
        var hasher = new PasswordHasher<Usuario>();
        var usuarios = await db.Usuarios.ToListAsync();
        Assert.All(usuarios, u =>
        {
            Assert.False(string.IsNullOrWhiteSpace(u.PasswordHash));
            Assert.DoesNotContain("SYNTHETIC_QA_HASH_ONLY", u.PasswordHash);
            var verificacion = hasher.VerifyHashedPassword(u, u.PasswordHash!, QaSyntheticDataFixture.ClaveSinteticaQa);
            Assert.NotEqual(PasswordVerificationResult.Failed, verificacion);
        });
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=crm_teambenavides;Username=crm;Password=test", true)]
    [InlineData("Host=localhost;Port=5432;Database=crm_teambenavides_qa;Username=crm_qa;Password=test", true)]
    [InlineData("Host=localhost;Port=5432;Database=otra_base_postgres;Username=crm_qa;Password=test", true)]
    [InlineData("Host=localhost;Port=5432;Database=crm_qa_isolated_123;Username=crm_qa;Password=test", false)]
    public async Task QA_13_QaSyntheticDataFixture_BloqueaBasesRelacionalesDeProduccionYQaCompartido(
        string connectionString,
        bool autorizarFlag)
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using var dbRelacional = new ApplicationDbContext(opciones);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            QaSyntheticDataFixture.SembrarAsync(dbRelacional, autorizarBaseRelacionalAisladaQa: autorizarFlag));
        Assert.Contains("Operación bloqueada", ex.Message);
    }

    // =========================================================================
    // 7. Inmutabilidad de archivos de producción y documentación QA
    // =========================================================================

    [Fact]
    public void QA_14_ArchivosDeProduccionYDocumentacionQa_IntactosYSinSecretos()
    {
        var composeProd = LeerArchivoRepo("despliegue/docker-compose.yml");
        var actualizarProd = LeerArchivoRepo("despliegue/actualizar.sh");
        var prepararProd = LeerArchivoRepo("despliegue/preparar-servidor.sh");
        var respaldoProd = LeerArchivoRepo("despliegue/respaldo.sh");
        var workflowProd = LeerArchivoRepo(".github/workflows/desplegar.yml");
        var docQa = LeerArchivoRepo("docs/qa.md");
        var gitignore = LeerArchivoRepo(".gitignore");

        Assert.Contains("name: crm-team-benavides", composeProd);
        Assert.Contains("REPOSITORIO=/opt/crm-team-benavides", actualizarProd);
        Assert.Contains("RAMA=main", prepararProd);
        Assert.Contains("copia=\"$DESTINO/${1:-crm}-$marca.dump\"", respaldoProd);
        Assert.Contains("refs/heads/main", workflowProd);

        Assert.Contains("generar-sql", docQa);
        Assert.Contains("qa-validar-", docQa);
        Assert.Contains("qa-desplegar-", docQa);
        Assert.Contains("crm-qa-", docQa);
        Assert.Contains("COMPOSE_FILE=docker-compose.qa.yml", docQa);
        Assert.Contains("COMPOSE_PROJECT_NAME=crm-team-benavides-qa", docQa);

        Assert.Contains("despliegue/.env.qa", gitignore);
        Assert.Contains("despliegue/migraciones-qa/", gitignore);
        Assert.Contains("despliegue/respaldos-qa/", gitignore);
    }

    // =========================================================================
    // 8. Pruebas FUNCIONALES reales de actualizar-qa.sh y respaldo-qa.sh en sandbox Git
    // =========================================================================

    private static string LocalizarEjecutableSh()
    {
        string[] candidatos =
        [
            @"D:\Git\bin\sh.exe",
            @"C:\Program Files\Git\bin\sh.exe",
            "/bin/sh"
        ];

        foreach (var candidato in candidatos)
        {
            if (File.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new InvalidOperationException("No se encontró un intérprete POSIX sh para ejecutar las pruebas funcionales.");
    }

    private sealed class SandboxQaFuncional : IDisposable
    {
        public string RootDir { get; }
        public string BareOriginDir { get; }
        public string RepoQaDir { get; }
        public string MockBinDir { get; }
        public string MockStateDir { get; }
        public string CommitInicialDevelop { get; private set; } = string.Empty;

        public SandboxQaFuncional()
        {
            RootDir = Path.Combine(Path.GetTempPath(), "crm-qa-func-" + Guid.NewGuid().ToString("N")[..10]);
            BareOriginDir = Path.Combine(RootDir, "origin.git");
            RepoQaDir = Path.Combine(RootDir, "repo-qa");
            MockBinDir = Path.Combine(RootDir, "mock-bin");
            MockStateDir = Path.Combine(RootDir, "mock-state");

            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(BareOriginDir);
            Directory.CreateDirectory(RepoQaDir);
            Directory.CreateDirectory(MockBinDir);
            Directory.CreateDirectory(MockStateDir);

            CrearMocksDeDockerYCurl();
            InicializarRepositorioGitConArchivosReales();
        }

        private void CrearMocksDeDockerYCurl()
        {
            var utf8SinBom = new UTF8Encoding(false);

            // Mock de docker en POSIX sh que simula ps, build, compose config/ps/up/exec e image prune
            const string mockDocker = """
                #!/bin/sh
                set -eu
                STATE="${QA_MOCK_STATE_DIR}"

                if [ "${1:-}" = "ps" ]; then
                  if [ -f "$STATE/prod_containers.txt" ]; then
                    cat "$STATE/prod_containers.txt"
                  fi
                  exit 0
                fi

                if [ "${1:-}" = "image" ] && [ "${2:-}" = "prune" ]; then
                  exit 0
                fi

                if [ "${1:-}" = "build" ]; then
                  # Simula: docker build --quiet -f .../Dockerfile.api --target migraciones --output <DIR> <CONTEXT>
                  out_dir=""
                  while [ $# -gt 0 ]; do
                    if [ "$1" = "--output" ]; then
                      out_dir="$2"
                      shift 2
                    else
                      shift
                    fi
                  done
                  if [ -n "$out_dir" ]; then
                    mkdir -p "$out_dir"
                    printf '%s\n' "-- SQL IDEMPOTENTE QA GENERADO" > "$out_dir/migraciones.sql"
                    printf '%s\n' "INSERT INTO \"__EFMigrationsHistory\" VALUES ('20261101000000_InicialQa', '10.0.0');" >> "$out_dir/migraciones.sql"
                  fi
                  exit 0
                fi

                if [ "${1:-}" = "compose" ]; then
                  shift
                  proj=""
                  comp_file=""
                  while [ $# -gt 0 ]; do
                    case "$1" in
                      -p) proj="$2"; shift 2 ;;
                      -f) comp_file="$2"; shift 2 ;;
                      --env-file) shift 2 ;;
                      *) break ;;
                    esac
                  done

                  subcmd="${1:-}"
                  shift || true

                  case "$subcmd" in
                    config)
                      [ -f "$comp_file" ] || exit 1
                      grep -Eq '^name:[[:space:]]*crm-team-benavides-qa' "$comp_file" || exit 1
                      exit 0
                      ;;
                    ps)
                      if [ -f "$STATE/running_services.txt" ]; then
                        cat "$STATE/running_services.txt"
                      fi
                      exit 0
                      ;;
                    up)
                      echo "up:$*" >> "$STATE/docker_actions.log"
                      # Registrar servicios como corriendo
                      for s in db api respaldo caddy; do
                        grep -qx "$s" "$STATE/running_services.txt" 2>/dev/null || echo "$s" >> "$STATE/running_services.txt"
                      done
                      exit 0
                      ;;
                    build)
                      echo "build:$1" >> "$STATE/docker_actions.log"
                      exit 0
                      ;;
                    exec)
                      [ "${1:-}" = "-T" ] && shift
                      svc="${1:-}"
                      shift
                      if [ "$svc" = "respaldo" ]; then
                        if [ -f "$STATE/fail_backup" ]; then
                          exit 1
                        fi
                        mkdir -p "$QA_TEST_SANDBOX_ROOT/despliegue/respaldos-qa"
                        printf 'PGDMP_QA_MOCK' > "$QA_TEST_SANDBOX_ROOT/despliegue/respaldos-qa/crm-qa-20261123-180000.dump"
                        echo "backup:ok" >> "$STATE/docker_actions.log"
                        exit 0
                      fi
                      if [ "$svc" = "db" ]; then
                        cmd_str="$*"
                        case "$cmd_str" in
                          *pg_isready*)
                            exit 0
                            ;;
                          *current_database*)
                            cat "$STATE/db_identity.txt"
                            exit 0
                            ;;
                          *to_regclass*)
                            cat "$STATE/ef_table_exists.txt"
                            exit 0
                            ;;
                          *MigrationId*)
                            if [ -f "$STATE/applied_migrations.txt" ]; then
                              cat "$STATE/applied_migrations.txt"
                            fi
                            exit 0
                            ;;
                          *"ON_ERROR_STOP=1 -q"*)
                            echo "applied" > "$STATE/last_applied_sql.sql"
                            echo "SI" > "$STATE/ef_table_exists.txt"
                            if [ -f "$STATE/expected_migrations_after_sql.txt" ]; then
                              cp "$STATE/expected_migrations_after_sql.txt" "$STATE/applied_migrations.txt"
                            fi
                            echo "sql:applied" >> "$STATE/docker_actions.log"
                            exit 0
                            ;;
                        esac
                      fi
                      exit 0
                      ;;
                  esac
                fi
                exit 0
                """;

            const string mockCurl = """
                #!/bin/sh
                for arg in "$@"; do
                  if [ "$arg" = "-I" ]; then
                    printf 'HTTP/2 200\r\nx-robots-tag: noindex, nofollow\r\n\r\n'
                    exit 0
                  fi
                done
                url=""
                for arg in "$@"; do
                  url="$arg"
                done
                case "$url" in
                  */api/aprobaciones) printf '401' ;;
                  */api/whatsapp/webhook) printf '503' ;;
                  *) printf '200' ;;
                esac
                exit 0
                """;

            File.WriteAllText(Path.Combine(MockBinDir, "docker"), mockDocker.Replace("\r\n", "\n"), utf8SinBom);
            File.WriteAllText(Path.Combine(MockBinDir, "curl"), mockCurl.Replace("\r\n", "\n"), utf8SinBom);
            File.WriteAllText(Path.Combine(MockStateDir, "db_identity.txt"), "crm_teambenavides_qa:crm_qa\n", utf8SinBom);
            File.WriteAllText(Path.Combine(MockStateDir, "ef_table_exists.txt"), "CON_HISTORIAL\n", utf8SinBom);
        }

        private void InicializarRepositorioGitConArchivosReales()
        {
            var utf8SinBom = new UTF8Encoding(false);
            var raizReal = ObtenerRaizRepositorio();

            Directory.CreateDirectory(Path.Combine(RepoQaDir, "despliegue"));
            Directory.CreateDirectory(Path.Combine(RepoQaDir, "src", "CRMTeamBenavides.Api", "Migrations"));

            foreach (var archivo in new[]
            {
                "despliegue/docker-compose.qa.yml",
                "despliegue/Caddyfile.qa",
                "despliegue/.env.qa.ejemplo",
                "despliegue/actualizar-qa.sh",
                "despliegue/preparar-servidor-qa.sh",
                "despliegue/respaldo-qa.sh",
                "despliegue/Dockerfile.api"
            })
            {
                var contenido = File.ReadAllText(Path.Combine(raizReal, archivo.Replace('/', Path.DirectorySeparatorChar)));
                File.WriteAllText(
                    Path.Combine(RepoQaDir, archivo.Replace('/', Path.DirectorySeparatorChar)),
                    contenido.Replace("\r\n", "\n"),
                    utf8SinBom);
            }

            File.WriteAllText(
                Path.Combine(RepoQaDir, "src", "CRMTeamBenavides.Api", "Migrations", "20261101000000_InicialQa.cs"),
                "// Migración sintética 1\n",
                utf8SinBom);

            File.WriteAllText(
                Path.Combine(MockStateDir, "ef_table_exists.txt"),
                "SI\n",
                utf8SinBom);
            File.WriteAllText(
                Path.Combine(MockStateDir, "applied_migrations.txt"),
                "20261101000000_InicialQa\n",
                utf8SinBom);
            File.WriteAllText(
                Path.Combine(MockStateDir, "expected_migrations_after_sql.txt"),
                "20261101000000_InicialQa\n",
                utf8SinBom);

            EscribirEnvQaValido();

            EjecutarComandoEnSandbox(
                $"git init --bare \"{BareOriginDir.Replace('\\', '/')}\" && " +
                $"cd \"{RepoQaDir.Replace('\\', '/')}\" && " +
                "git init -b develop && " +
                "git config user.email 'qa@sintetico.test' && " +
                "git config user.name 'QA Test' && " +
                $"git remote add origin \"{BareOriginDir.Replace('\\', '/')}\" && " +
                "git add despliegue src && " +
                "git commit -m 'Commit inicial QA' && " +
                "git push -u origin develop");

            CommitInicialDevelop = EjecutarComandoEnSandbox(
                $"git -C \"{RepoQaDir.Replace('\\', '/')}\" rev-parse HEAD").Salida.Trim();
        }

        public void EscribirEnvQaValido(Action<Dictionary<string, string>>? mutar = null)
        {
            var valores = new Dictionary<string, string>
            {
                ["COMPOSE_FILE"] = "docker-compose.qa.yml",
                ["COMPOSE_PROJECT_NAME"] = "crm-team-benavides-qa",
                ["DOMINIO"] = "\"qa.reservado.teambenavides.test\"",
                ["HSTS_SEGUNDOS"] = "0",
                ["PUERTO_HTTP"] = "80",
                ["PUERTO_HTTPS"] = "443",
                ["POSTGRES_DB"] = "crm_teambenavides_qa",
                ["POSTGRES_USER"] = "crm_qa",
                ["POSTGRES_PASSWORD"] = DbPassValidaQa,
                ["ASPNETCORE_ENVIRONMENT"] = "Staging",
                ["JWT_SECRET_KEY"] = JwtValidoQa,
                ["RED_INTERNA_QA"] = "172.31.0.0/24",
                ["HORA_RESPALDO"] = "03:00",
                ["RETENCION_DIAS"] = "7",
                ["NOTIFICACIONES_PROVEEDOR"] = "Registro",
                ["YAMAHA_ENABLED"] = "false",
                ["WHATSAPP_ENABLED"] = "false",
                ["WHATSAPP_PROVIDER"] = "Mock",
                ["WHATSAPP_AI_ENABLED"] = "false",
                ["WHATSAPP_AI_PROVIDER"] = "Mock"
            };

            mutar?.Invoke(valores);

            var sb = new StringBuilder();
            foreach (var kvp in valores)
            {
                sb.Append(kvp.Key).Append('=').Append(kvp.Value).Append('\n');
            }

            File.WriteAllText(
                Path.Combine(RepoQaDir, "despliegue", ".env"),
                sb.ToString(),
                new UTF8Encoding(false));
        }

        public (int ExitCode, string Salida) EjecutarActualizarQa(
            string argumentos,
            string? sshOriginalCommand = null,
            string? prodDirOverride = null)
        {
            var scriptPath = Path.Combine(RepoQaDir, "despliegue", "actualizar-qa.sh").Replace('\\', '/');
            var comando = string.IsNullOrEmpty(argumentos)
                ? $"sh \"{scriptPath}\""
                : $"sh \"{scriptPath}\" {argumentos}";

            return EjecutarComandoEnSandbox(comando, sshOriginalCommand, prodDirOverride);
        }

        public (int ExitCode, string Salida) EjecutarComandoEnSandbox(
            string comandoSh,
            string? sshOriginalCommand = null,
            string? prodDirOverride = null)
        {
            var shExe = LocalizarEjecutableSh();
            var mockBinPosix = MockBinDir.Length >= 2 && MockBinDir[1] == ':'
                ? $"/{char.ToLowerInvariant(MockBinDir[0])}{MockBinDir[2..].Replace('\\', '/')}"
                : MockBinDir.Replace('\\', '/');
            var comandoConPath = $"export PATH='{mockBinPosix}':\"$PATH\"; {comandoSh}";

            var psi = new ProcessStartInfo
            {
                FileName = shExe,
                Arguments = $"-c \"{comandoConPath.Replace("\"", "\\\"")}\"",
                WorkingDirectory = RepoQaDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.Environment["QA_TEST_SANDBOX_ROOT"] = RepoQaDir.Replace('\\', '/');
            psi.Environment["QA_TEST_PROD_DIR"] = (prodDirOverride ?? Path.Combine(RootDir, "no-existe-prod")).Replace('\\', '/');
            psi.Environment["QA_TEST_SIN_SETSID"] = "1";
            psi.Environment["QA_MOCK_STATE_DIR"] = MockStateDir.Replace('\\', '/');

            if (sshOriginalCommand is not null)
            {
                psi.Environment["SSH_ORIGINAL_COMMAND"] = sshOriginalCommand;
            }
            else
            {
                psi.Environment.Remove("SSH_ORIGINAL_COMMAND");
            }

            using var proc = Process.Start(psi)!;
            proc.StandardInput.Close();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(30000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException($"El comando en sandbox excedió 30s: {comandoSh}");
            }
            Task.WaitAll(stdoutTask, stderrTask);

            return (proc.ExitCode, stdoutTask.Result + "\n" + stderrTask.Result);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootDir))
                {
                    foreach (var file in Directory.GetFiles(RootDir, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    Directory.Delete(RootDir, recursive: true);
                }
            }
            catch
            {
                // Ignorar bloqueos transitorios de sistema de archivos en limpieza temporal
            }
        }
    }

    [Fact]
    public void QA_15_Funcional_ActualizarQa_RechazaArgumentosInvalidos_ShaCortos_YRamasAjenasADevelop()
    {
        using var sandbox = new SandboxQaFuncional();

        // 1. SHA corto
        var rCorto = sandbox.EjecutarActualizarQa("5993761 validar");
        Assert.NotEqual(0, rCorto.ExitCode);
        Assert.Contains("40 caracteres", rCorto.Salida);

        // 2. Nombre de rama en lugar de SHA
        var rRama = sandbox.EjecutarActualizarQa("develop desplegar");
        Assert.NotEqual(0, rRama.ExitCode);
        Assert.Contains("SHA hexadecimal", rRama.Salida);

        // 3. Intento de inyección de flag interno --ejecutar o metacaracteres
        var rFlag = sandbox.EjecutarActualizarQa("\"--ejecutar\" validar");
        Assert.NotEqual(0, rFlag.ExitCode);

        // 4. Modo migrar sin confirmación explícita
        var rSinConf = sandbox.EjecutarActualizarQa($"{sandbox.CommitInicialDevelop} migrar");
        Assert.NotEqual(0, rSinConf.ExitCode);
        Assert.Contains("CONFIRMO_MIGRAR_QA:crm_teambenavides_qa", rSinConf.Salida);

        // 5. Commit existente en otra rama (main) pero que NO es ancestro de origin/develop
        sandbox.EjecutarComandoEnSandbox(
            "git checkout -b main && " +
            "echo '// cambio solo en main' >> src/CRMTeamBenavides.Api/Migrations/20261101000000_InicialQa.cs && " +
            "git commit -am 'Commit exclusivo de main' && " +
            "git push origin main");
        var shaMain = sandbox.EjecutarComandoEnSandbox("git rev-parse HEAD").Salida.Trim();
        sandbox.EjecutarComandoEnSandbox($"git checkout develop && git reset --hard {sandbox.CommitInicialDevelop}");

        var rAjeno = sandbox.EjecutarActualizarQa($"{shaMain} validar");
        Assert.NotEqual(0, rAjeno.ExitCode);
        Assert.Contains("NO pertenece a la historia de origin/develop", rAjeno.Salida);
    }

    [Fact]
    public void QA_16_Funcional_ActualizarQa_RechazaColisionesConProduccion_ComposeFileIncorrecto_YDiscrepanciaDeDominio()
    {
        using var sandbox = new SandboxQaFuncional();
        var sha = sandbox.CommitInicialDevelop;

        // 1. Si existe el directorio productivo en el mismo servidor -> bloqueado
        var dirProdSimulado = Path.Combine(sandbox.RootDir, "opt-prod-existente");
        Directory.CreateDirectory(dirProdSimulado);
        var rDirProd = sandbox.EjecutarActualizarQa($"{sha} validar", prodDirOverride: dirProdSimulado);
        Assert.NotEqual(0, rDirProd.ExitCode);
        Assert.Contains("aislamiento de QA", rDirProd.Salida);

        // 2. Si docker ps reporta un contenedor productivo -> bloqueado
        File.WriteAllText(Path.Combine(sandbox.MockStateDir, "prod_containers.txt"), "crm-team-benavides-db-1\n");
        var rContProd = sandbox.EjecutarActualizarQa($"{sha} validar");
        Assert.NotEqual(0, rContProd.ExitCode);
        Assert.Contains("contenedores del proyecto de producción", rContProd.Salida);
        File.Delete(Path.Combine(sandbox.MockStateDir, "prod_containers.txt"));

        // 3. Si COMPOSE_FILE apunta a docker-compose.yml -> bloqueado (QA-FIN-HIGH-04)
        sandbox.EscribirEnvQaValido(env => env["COMPOSE_FILE"] = "docker-compose.yml");
        var rComposeFile = sandbox.EjecutarActualizarQa($"{sha} validar");
        Assert.NotEqual(0, rComposeFile.ExitCode);
        Assert.Contains("COMPOSE_FILE", rComposeFile.Salida);

        // 4. Si COMPOSE_PROJECT_NAME apunta a crm-team-benavides -> bloqueado (QA-FIN-HIGH-04)
        sandbox.EscribirEnvQaValido(env => env["COMPOSE_PROJECT_NAME"] = "crm-team-benavides");
        var rProj = sandbox.EjecutarActualizarQa($"{sha} validar");
        Assert.NotEqual(0, rProj.ExitCode);
        Assert.Contains("COMPOSE_PROJECT_NAME", rProj.Salida);

        // 5. Si POSTGRES_DB es crm_teambenavides -> bloqueado
        sandbox.EscribirEnvQaValido(env => env["POSTGRES_DB"] = "crm_teambenavides");
        var rDbProd = sandbox.EjecutarActualizarQa($"{sha} validar");
        Assert.NotEqual(0, rDbProd.ExitCode);
        Assert.Contains("POSTGRES_DB", rDbProd.Salida);

        // 6. Discrepancia entre QA_DOMINIO del workflow y DOMINIO del .env remoto (QA-FIN-MED-07)
        sandbox.EscribirEnvQaValido();
        var rDominioDistinto = sandbox.EjecutarActualizarQa($"{sha} validar --dominio-esperado=otro.subdominio.test");
        Assert.NotEqual(0, rDominioDistinto.ExitCode);
        Assert.Contains("no coincide con QA_DOMINIO del workflow", rDominioDistinto.Salida);
        // No debe filtrar el dominio real en el mensaje de error
        Assert.DoesNotContain("qa.reservado.teambenavides.test", rDominioDistinto.Salida);

        // 7. Cuando --dominio-esperado coincide con el valor entrecomillado en .env (QA-FIN-MED-01 + MED-07) -> éxito
        var rDominioOk = sandbox.EjecutarActualizarQa($"{sha} validar --dominio-esperado=qa.reservado.teambenavides.test");
        Assert.Equal(0, rDominioOk.ExitCode);
    }

    [Fact]
    public void QA_17_Funcional_ModoValidarYDesplegar_InspeccionaShaObjetivo_DetectaBaseSinTabla_YNoMueveCheckoutAlAbortar()
    {
        using var sandbox = new SandboxQaFuncional();
        var sha1 = sandbox.CommitInicialDevelop;
        var utf8SinBom = new UTF8Encoding(false);

        // Crear un segundo commit en origin/develop que añade una nueva migración
        File.WriteAllText(
            Path.Combine(sandbox.RepoQaDir, "src", "CRMTeamBenavides.Api", "Migrations", "20261123000000_NuevaMigracionQa.cs"),
            "// Migración sintética 2\n",
            utf8SinBom);
        sandbox.EjecutarComandoEnSandbox("git add src && git commit -m 'Agregar segunda migración' && git push origin develop");
        var sha2 = sandbox.EjecutarComandoEnSandbox("git rev-parse HEAD").Salida.Trim();

        // Volver el checkout local del servidor QA al commit inicial (sha1)
        sandbox.EjecutarComandoEnSandbox($"git reset --hard {sha1}");

        // Escenario A: Base recién creada sin tabla __EFMigrationsHistory (QA-FIN-CRIT-01)
        File.WriteAllText(Path.Combine(sandbox.MockStateDir, "ef_table_exists.txt"), "NO\n", utf8SinBom);
        File.WriteAllText(Path.Combine(sandbox.MockStateDir, "running_services.txt"), "db\n", utf8SinBom);

        var rValidarBaseNueva = sandbox.EjecutarActualizarQa($"{sha2} validar");
        Assert.Equal(0, rValidarBaseNueva.ExitCode);
        Assert.Contains("SIN_TABLA", rValidarBaseNueva.Salida);
        Assert.Contains("20261123000000_NuevaMigracionQa", rValidarBaseNueva.Salida);
        // Verificar que validar NO movió el checkout local de sha1
        var headActual = sandbox.EjecutarComandoEnSandbox("git rev-parse HEAD").Salida.Trim();
        Assert.Equal(sha1, headActual);

        // Escenario B: Modo desplegar sobre base nueva (con db inicialmente detenida) -> inicia db y aborta limpiamente sin mover HEAD
        File.Delete(Path.Combine(sandbox.MockStateDir, "running_services.txt"));
        var rDesplegarBaseNueva = sandbox.EjecutarActualizarQa($"{sha2} desplegar");
        Assert.NotEqual(0, rDesplegarBaseNueva.ExitCode);
        Assert.Contains("no está inicializada (SIN_TABLA)", rDesplegarBaseNueva.Salida);
        Assert.Equal(sha1, sandbox.EjecutarComandoEnSandbox("git rev-parse HEAD").Salida.Trim());

        // Escenario C: Base con historial EF de sha1, pero intentando «desplegar» sha2 (que trae migración pendiente)
        File.WriteAllText(Path.Combine(sandbox.MockStateDir, "ef_table_exists.txt"), "SI\n", utf8SinBom);
        File.WriteAllText(Path.Combine(sandbox.MockStateDir, "applied_migrations.txt"), "20261101000000_InicialQa\n", utf8SinBom);

        var rDesplegarConPendiente = sandbox.EjecutarActualizarQa($"{sha2} desplegar");
        Assert.NotEqual(0, rDesplegarConPendiente.ExitCode);
        Assert.Contains("20261123000000_NuevaMigracionQa", rDesplegarConPendiente.Salida);
        // El checkout local debe permanecer intacto en sha1 (QA-FIN-HIGH-03)
        Assert.Equal(sha1, sandbox.EjecutarComandoEnSandbox("git rev-parse HEAD").Salida.Trim());
    }

    [Fact]
    public void QA_18_Funcional_GenerarSqlYModoMigrar_RechazaSqlAlterado_YAplicaConRespaldoPrevioVerificado()
    {
        using var sandbox = new SandboxQaFuncional();
        var sha = sandbox.CommitInicialDevelop;
        var utf8SinBom = new UTF8Encoding(false);

        // Simular base QA nueva sin historial EF y sin contenedores corriendo inicialmente
        File.WriteAllText(Path.Combine(sandbox.MockStateDir, "ef_table_exists.txt"), "NO\n", utf8SinBom);
        File.WriteAllText(Path.Combine(sandbox.MockStateDir, "applied_migrations.txt"), "", utf8SinBom);
        File.Delete(Path.Combine(sandbox.MockStateDir, "running_services.txt"));

        // 1. Intentar migrar sin haber ejecutado antes generar-sql -> rechazado (QA-FIN-HIGH-02)
        var rSinSql = sandbox.EjecutarActualizarQa($"{sha} migrar CONFIRMO_MIGRAR_QA:crm_teambenavides_qa");
        Assert.NotEqual(0, rSinSql.ExitCode);
        Assert.Contains("generar-sql", rSinSql.Salida);

        // 2. Ejecutar generar-sql para el commit objetivo
        var rGen = sandbox.EjecutarActualizarQa($"{sha} generar-sql");
        Assert.Equal(0, rGen.ExitCode);
        var rutaSql = Path.Combine(sandbox.RepoQaDir, "despliegue", "migraciones-qa", "migraciones.sql");
        var rutaManifest = Path.Combine(sandbox.RepoQaDir, "despliegue", "migraciones-qa", "migraciones.manifest");
        Assert.True(File.Exists(rutaSql));
        Assert.True(File.Exists(rutaManifest));
        Assert.Contains($"COMMIT={sha}", File.ReadAllText(rutaManifest));

        // 3. Alterar migraciones.sql después de generar-sql -> migrar debe rechazarlo por discrepancia de SHA-256
        var sqlOriginal = File.ReadAllText(rutaSql);
        File.WriteAllText(rutaSql, sqlOriginal + "\n-- alteracion no autorizada\n", utf8SinBom);
        var rSqlAlterado = sandbox.EjecutarActualizarQa($"{sha} migrar CONFIRMO_MIGRAR_QA:crm_teambenavides_qa");
        Assert.NotEqual(0, rSqlAlterado.ExitCode);
        Assert.Contains("SHA-256", rSqlAlterado.Salida);

        // 4. Restaurar el SQL legítimo y simular fallo de respaldo -> migrar debe abortar antes de aplicar SQL
        File.WriteAllText(rutaSql, sqlOriginal, utf8SinBom);
        File.WriteAllText(Path.Combine(sandbox.MockStateDir, "fail_backup"), "1", utf8SinBom);
        var rFalloBackup = sandbox.EjecutarActualizarQa($"{sha} migrar CONFIRMO_MIGRAR_QA:crm_teambenavides_qa");
        Assert.NotEqual(0, rFalloBackup.ExitCode);
        Assert.Contains("respaldo previo", rFalloBackup.Salida);
        Assert.False(File.Exists(Path.Combine(sandbox.MockStateDir, "last_applied_sql.sql")));
        File.Delete(Path.Combine(sandbox.MockStateDir, "fail_backup"));

        // 5. Ejecutar migrar con SQL íntegro y respaldo funcional -> éxito completo
        var rMigrarOk = sandbox.EjecutarActualizarQa(
            $"{sha} migrar CONFIRMO_MIGRAR_QA:crm_teambenavides_qa --dominio-esperado=qa.reservado.teambenavides.test");
        Assert.True(rMigrarOk.ExitCode == 0, rMigrarOk.Salida);
        Assert.Contains("Respaldo QA verificado: despliegue/respaldos-qa/crm-qa-", rMigrarOk.Salida);
        Assert.Contains("Total de migraciones registradas en crm_teambenavides_qa: 1", rMigrarOk.Salida);
        Assert.Contains("Web=200, API=401 (sin sesión), Webhook WhatsApp=503 (deshabilitado), X-Robots-Tag=noindex", rMigrarOk.Salida);

        // 6. Con la base ya migrada, ejecutar modo desplegar -> toma respaldo previo, compila secuencialmente y despliega con éxito
        var rDesplegarOk = sandbox.EjecutarActualizarQa(
            $"{sha} desplegar --dominio-esperado=qa.reservado.teambenavides.test");
        Assert.True(rDesplegarOk.ExitCode == 0, rDesplegarOk.Salida);
        Assert.Contains("Despliegue QA completado en modo «desplegar»", rDesplegarOk.Salida);
    }

    [Fact]
    public void QA_19_Funcional_RespaldoQa_RechazaBaseDeProduccion_YRechazaRestaurarDumpSinPrefijoCrmQa()
    {
        using var sandbox = new SandboxQaFuncional();
        var scriptRespaldoQa = Path.Combine(sandbox.RepoQaDir, "despliegue", "respaldo-qa.sh").Replace('\\', '/');

        // 1. Si PGDATABASE es crm_teambenavides (producción) -> bloqueado inmediatamente
        var rDbProd = sandbox.EjecutarComandoEnSandbox(
            $"DESTINO_RESPALDOS_QA=\"{sandbox.RootDir.Replace('\\', '/')}\" PGDATABASE=crm_teambenavides PGUSER=crm_qa sh \"{scriptRespaldoQa}\" ahora");
        Assert.NotEqual(0, rDbProd.ExitCode);
        Assert.Contains("crm_teambenavides_qa", rDbProd.Salida);

        // 2. Si PGUSER es crm (producción) -> bloqueado inmediatamente
        var rUserProd = sandbox.EjecutarComandoEnSandbox(
            $"DESTINO_RESPALDOS_QA=\"{sandbox.RootDir.Replace('\\', '/')}\" PGDATABASE=crm_teambenavides_qa PGUSER=crm sh \"{scriptRespaldoQa}\" ahora");
        Assert.NotEqual(0, rUserProd.ExitCode);
        Assert.Contains("crm_qa", rUserProd.Salida);

        // 3. Si se intenta restaurar un archivo con nombre productivo crm-20261123-0300.dump -> bloqueado (QA-FIN-LOW-04)
        var dumpProdSimulado = Path.Combine(sandbox.RootDir, "crm-20261123-0300.dump");
        File.WriteAllText(dumpProdSimulado, "DUMP_SIMULADO");
        var rDumpProd = sandbox.EjecutarComandoEnSandbox(
            $"DESTINO_RESPALDOS_QA=\"{sandbox.RootDir.Replace('\\', '/')}\" PGDATABASE=crm_teambenavides_qa PGUSER=crm_qa " +
            $"sh \"{scriptRespaldoQa}\" restaurar \"{dumpProdSimulado.Replace('\\', '/')}\"");
        Assert.NotEqual(0, rDumpProd.ExitCode);
        Assert.Contains("crm-qa-*.dump", rDumpProd.Salida);
    }
}
