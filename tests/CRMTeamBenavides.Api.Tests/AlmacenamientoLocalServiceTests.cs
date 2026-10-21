using CRMTeamBenavides.Api.Services.Almacenamiento;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

public class AlmacenamientoLocalServiceTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), $"fotos-prueba-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    private AlmacenamientoLocalService Crear() => new(
        new EntornoDePrueba(_carpeta),
        Options.Create(new AlmacenamientoOptions { RutaBase = _carpeta }),
        NullLogger<AlmacenamientoLocalService>.Instance);

    private static MemoryStream Foto(int bytes) => new(new byte[bytes]);

    [Fact]
    public async Task Una_foto_achicada_de_unos_cientos_de_KB_se_guarda()
    {
        var (ruta, _) = await Crear().GuardarFotoAsync(Foto(400 * 1024), "moto.jpg", "image/jpeg", "os1");

        Assert.True(File.Exists(Path.Combine(_carpeta, ruta)));
    }

    [Fact]
    public async Task Una_foto_sin_achicar_de_mas_de_3_MB_se_rechaza_y_no_ocupa_disco()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Crear().GuardarFotoAsync(Foto(4 * 1024 * 1024), "IMG_2041.jpg", "image/jpeg", "os1"));

        Assert.Contains("3 MB", error.Message);
        Assert.Empty(Directory.EnumerateFiles(_carpeta, "*", SearchOption.AllDirectories));
    }

    private sealed class EntornoDePrueba(string raiz) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = raiz;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Pruebas";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = raiz;
        public string EnvironmentName { get; set; } = "Testing";
    }
}
