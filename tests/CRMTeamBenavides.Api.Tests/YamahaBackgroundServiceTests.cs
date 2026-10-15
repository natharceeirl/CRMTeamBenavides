using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas unitarias para YamahaSyncBackgroundService.
/// Valida que el scheduler permanezca en reposo cuando Enabled == false y respete la cancelación.
/// </summary>
public class YamahaBackgroundServiceTests
{
    private sealed class FakeOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public FakeOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; set; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FakeSyncOrchestrator : IYamahaSyncOrchestrator
    {
        public List<YamahaServicioSincronizacion> ServiciosEjecutados { get; } = new();

        public Task<YamahaSyncEjecucionResultado> SincronizarServicioAsync(
            YamahaServicioSincronizacion servicio,
            bool esSimulacion = false,
            string modoEnvio = "Manual",
            Guid? usuarioId = null,
            DateTime? rangoDesdeUtc = null,
            DateTime? rangoHastaUtc = null,
            CancellationToken cancellationToken = default)
        {
            ServiciosEjecutados.Add(servicio);
            return Task.FromResult(new YamahaSyncEjecucionResultado(
                SyncLogId: Guid.NewGuid(),
                Servicio: servicio,
                Exitoso: true,
                EsSimulacion: esSimulacion,
                TotalLeidos: 5,
                TotalValidos: 5,
                TotalRechazados: 0,
                PayloadHash: "abc",
                CodigoHttp: 200,
                Mensaje: "OK",
                ErroresOAdvertencias: Array.Empty<string>(),
                Duracion: TimeSpan.FromMilliseconds(10)));
        }
    }

    [Fact]
    public async Task BackgroundService_CuandoEnabledEsFalse_NoDebeEjecutarSincronizaciones()
    {
        var services = new ServiceCollection();
        var fakeOrchestrator = new FakeSyncOrchestrator();
        services.AddSingleton<IYamahaSyncOrchestrator>(fakeOrchestrator);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var optionsMonitor = new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings
        {
            Enabled = false
        });

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            optionsMonitor,
            NullLogger<YamahaSyncBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await backgroundService.StartAsync(cts.Token);
        await Task.Delay(20);
        await backgroundService.StopAsync(CancellationToken.None);

        Assert.Empty(fakeOrchestrator.ServiciosEjecutados);
    }

    [Fact]
    public async Task EjecutarCicloProgramadoAsync_DebeEjecutarVentasYServicioTecnico()
    {
        var services = new ServiceCollection();
        var fakeOrchestrator = new FakeSyncOrchestrator();
        services.AddSingleton<IYamahaSyncOrchestrator>(fakeOrchestrator);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var optionsMonitor = new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings
        {
            Enabled = true
        });

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            optionsMonitor,
            NullLogger<YamahaSyncBackgroundService>.Instance);

        await backgroundService.EjecutarCicloProgramadoAsync(CancellationToken.None);

        Assert.Equal(2, fakeOrchestrator.ServiciosEjecutados.Count);
        Assert.Contains(YamahaServicioSincronizacion.Ventas, fakeOrchestrator.ServiciosEjecutados);
        Assert.Contains(YamahaServicioSincronizacion.ServicioTecnico, fakeOrchestrator.ServiciosEjecutados);
    }
}
