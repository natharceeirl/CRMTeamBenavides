using CRMTeamBenavides.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// La revisión de la configuración al arrancar la API: en producción rechaza claves débiles
/// o de ejemplo sin mostrarlas en el error; en Development no revisa nada.
/// </summary>
public class ValidacionConfiguracionEntornoTests
{
    private sealed class HostEnvironmentSimulado(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "CRMTeamBenavides.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private const string JwtValido =
        "9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a1f0e9d8c7b6a5f4e3d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a";

    private const string ClaveBaseValida =
        "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2";

    private static string CadenaConexion(string clave) =>
        $"Host=db;Port=5432;Database=crm_teambenavides;Username=crm;Password={clave}";

    private static Dictionary<string, string?> CrearConfiguracionProduccionValida() =>
        new()
        {
            ["JwtSettings:SecretKey"] = JwtValido,
            ["ConnectionStrings:DefaultConnection"] = CadenaConexion(ClaveBaseValida)
        };

    private static void Validar(string entorno, Dictionary<string, string?> datos) =>
        ValidacionConfiguracionEntorno.ValidarAlArrancar(
            new HostEnvironmentSimulado(entorno),
            new ConfigurationBuilder().AddInMemoryCollection(datos).Build());

    [Fact]
    public void EnProduccion_AceptaUnaConfiguracionValida()
    {
        var ex = Record.Exception(() => Validar(Environments.Production, CrearConfiguracionProduccionValida()));

        Assert.Null(ex);
    }

    [Fact]
    public void EnProduccion_UnaClaveAlAzarQueContieneXxxxNoEsUnFalsoPositivo()
    {
        var datos = CrearConfiguracionProduccionValida();
        datos["JwtSettings:SecretKey"] = "9f8e7d6c5b4axxxx1d0c9b8a7f6e5d4c3b2a1f0e9d8c7b6a5f4e3d2c1b0a9f8e";

        var ex = Record.Exception(() => Validar(Environments.Production, datos));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData("REEMPLAZAR_CON_CLAVE_JWT_QA_HEX_48_BYTES_COMPLETOS")]
    [InlineData("CAMBIAR_ESTA_CLAVE_POR_UNA_REAL_DE_48_BYTES_MINIMO")]
    [InlineData("PLACEHOLDER_SECRET_KEY_123456789012345678901234567890")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("123456789012345678901234567890123456789012345678")]
    [InlineData("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6")] // 32 caracteres: se exigen 48
    [InlineData("")]
    public void EnProduccion_RechazaUnaClaveJwtDebilODeEjemplo_SinMostrarla(string jwt)
    {
        var datos = CrearConfiguracionProduccionValida();
        datos["JwtSettings:SecretKey"] = jwt;

        var ex = Assert.Throws<InvalidOperationException>(() => Validar(Environments.Production, datos));

        Assert.Contains("JwtSettings:SecretKey", ex.Message);
        if (!string.IsNullOrWhiteSpace(jwt))
        {
            Assert.DoesNotContain(jwt, ex.Message);
        }
    }

    [Theory]
    [InlineData("REEMPLAZAR_CON_CLAVE_DB_QA_HEX_32", "DefaultConnection")]
    [InlineData("a1b2c3d4e5f6a7b8", "contraseña de PostgreSQL")]
    [InlineData("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "contraseña de PostgreSQL")]
    public void EnProduccion_RechazaUnaClaveDeLaBaseDebilODeEjemplo_SinMostrarla(string clave, string fragmentoEsperado)
    {
        var datos = CrearConfiguracionProduccionValida();
        datos["ConnectionStrings:DefaultConnection"] = CadenaConexion(clave);

        var ex = Assert.Throws<InvalidOperationException>(() => Validar(Environments.Production, datos));

        Assert.Contains(fragmentoEsperado, ex.Message);
        Assert.DoesNotContain(clave, ex.Message);
    }

    [Fact]
    public void EnDevelopment_NoRevisaNada()
    {
        var ex = Record.Exception(() => Validar(Environments.Development, new Dictionary<string, string?>()));

        Assert.Null(ex);
    }
}
