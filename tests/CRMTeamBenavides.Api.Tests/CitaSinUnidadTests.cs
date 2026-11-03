using CRMTeamBenavides.Api.Features.Citas;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.Exportacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// La cita se puede agendar sin unidad: el choque de horario se mira por cliente y la
/// orden que se abre al recibirla le completa la unidad. Crear la cita usa la secuencia
/// de PostgreSQL; eso se prueba contra la API.
/// </summary>
public class CitaSinUnidadTests
{
    private readonly string _base = Guid.NewGuid().ToString();
    private static readonly DateTime Manana = DateTime.UtcNow.Date.AddDays(1).AddHours(14);

    private ApplicationDbContext Contexto() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(_base)
        .Options);

    private CitaService Servicio(ApplicationDbContext contexto) => new(contexto, new ExportacionExcelService());

    private async Task<(Guid ClienteId, Guid VehiculoId, Guid CitaId)> CitaSinUnidadAsync()
    {
        await using var contexto = Contexto();
        var cliente = new Cliente { NombreCompleto = "Rosa Mamani", NumeroDocumento = "40123456", Activo = true };
        var unidad = new Vehiculo { ClienteId = cliente.Id, Marca = "Yamaha", Modelo = "FZ 2.0", Placa = "ABC-123", Activo = true };
        var cita = new Cita
        {
            NumeroCita = "CIT-000001", ClienteId = cliente.Id, VehiculoId = null, FechaHoraProgramada = Manana,
            DuracionMinutos = 60, Motivo = "Revisión", Estado = EstadoCita.Pendiente, Activo = true
        };
        contexto.AddRange(cliente, unidad, cita);
        await contexto.SaveChangesAsync();
        return (cliente.Id, unidad.Id, cita.Id);
    }

    [Fact]
    public async Task Una_cita_sin_unidad_se_lee_y_se_edita_sin_elegirla()
    {
        var (_, _, citaId) = await CitaSinUnidadAsync();

        await using var contexto = Contexto();
        var editada = await Servicio(contexto).ActualizarAsync(
            citaId, new ActualizarCitaRequest(null, Manana, 60, "Revisión y cambio de aceite", null), usuarioId: null);

        Assert.True(editada.IsSuccess, editada.Error);
        Assert.Null(editada.Data!.VehiculoId);
        Assert.Null(editada.Data.VehiculoModelo);
        Assert.Equal("Revisión y cambio de aceite", editada.Data.Motivo);
    }

    [Fact]
    public async Task Sin_unidad_el_mismo_cliente_no_tiene_dos_citas_que_se_crucen()
    {
        var (clienteId, _, citaId) = await CitaSinUnidadAsync();
        await using (var contexto = Contexto())
        {
            contexto.Citas.Add(new Cita
            {
                NumeroCita = "CIT-000002", ClienteId = clienteId, FechaHoraProgramada = Manana.AddHours(3),
                DuracionMinutos = 60, Motivo = "Otra", Estado = EstadoCita.Pendiente, Activo = true
            });
            await contexto.SaveChangesAsync();
        }

        await using var otro = Contexto();
        var reprogramada = await Servicio(otro).ReprogramarAsync(
            citaId, new ReprogramarCitaRequest(Manana.AddHours(3).AddMinutes(30), 60, null), usuarioId: null);

        Assert.Equal(ServiceResultStatus.Conflict, reprogramada.Status);
        Assert.Contains("cliente", reprogramada.Error);
    }

    [Fact]
    public async Task La_orden_que_se_abre_le_completa_la_unidad()
    {
        var (clienteId, vehiculoId, citaId) = await CitaSinUnidadAsync();
        var ordenId = Guid.NewGuid();
        await using (var contexto = Contexto())
        {
            contexto.OrdenesServicio.Add(new OrdenServicio
            {
                Id = ordenId, NumeroOrden = "OS-000902", ClienteId = clienteId, VehiculoId = vehiculoId, Activo = true
            });
            await contexto.SaveChangesAsync();
        }

        await using var otro = Contexto();
        var vinculada = await Servicio(otro).VincularOrdenAsync(citaId, new VincularOrdenCitaRequest(ordenId), usuarioId: null);

        Assert.True(vinculada.IsSuccess, vinculada.Error);
        Assert.Equal(vehiculoId, vinculada.Data!.VehiculoId);
        Assert.Equal(EstadoCita.EnTaller, vinculada.Data.Estado);
    }

    [Fact]
    public async Task Una_orden_de_otro_cliente_no_se_vincula()
    {
        var (_, vehiculoId, citaId) = await CitaSinUnidadAsync();
        var ordenId = Guid.NewGuid();
        await using (var contexto = Contexto())
        {
            contexto.OrdenesServicio.Add(new OrdenServicio
            {
                Id = ordenId, NumeroOrden = "OS-000903", ClienteId = Guid.NewGuid(), VehiculoId = vehiculoId, Activo = true
            });
            await contexto.SaveChangesAsync();
        }

        await using var otro = Contexto();
        var vinculada = await Servicio(otro).VincularOrdenAsync(citaId, new VincularOrdenCitaRequest(ordenId), usuarioId: null);

        Assert.False(vinculada.IsSuccess);
        Assert.Contains("mismo cliente", vinculada.Error);
    }
}
