using System.Text.Json;
using CRMTeamBenavides.Api.Features.Configuracion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class ConfiguracionService : IConfiguracionService
{
    private readonly ApplicationDbContext _context;

    public ConfiguracionService(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<ConfiguracionEmpresa> GetOrCreateEntityAsync(CancellationToken ct = default)
    {
        var config = await _context.ConfiguracionesEmpresa.FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new ConfiguracionEmpresa
            {
                NombreEmpresa = "Team Benavides",
                RazonSocial = "Team Benavides S.R.L.",
                Ruc = "20000000001",
                Direccion = null,
                Telefono = null,
                Email = null,
                PorcentajeIgv = 18.00m,
                MonedaBase = "PEN",
                TipoCambioVigente = null,
                FechaActualizacionTipoCambio = null,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.ConfiguracionesEmpresa.Add(config);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            bool modificado = false;
            if (string.IsNullOrWhiteSpace(config.MonedaBase))
            {
                config.MonedaBase = "PEN";
                modificado = true;
            }
            if (string.IsNullOrWhiteSpace(config.RazonSocial))
            {
                config.RazonSocial = string.IsNullOrWhiteSpace(config.NombreEmpresa) ? "Team Benavides S.R.L." : config.NombreEmpresa;
                modificado = true;
            }
            if (modificado)
            {
                await _context.SaveChangesAsync(ct);
            }
        }
        return config;
    }

    public async Task<ConfiguracionEmpresaResponse> ObtenerConfiguracionEmpresaAsync(CancellationToken ct = default)
    {
        var config = await GetOrCreateEntityAsync(ct);
        return MapearEmpresa(config);
    }

    public async Task<decimal> ObtenerPorcentajeIgvVigenteAsync(CancellationToken ct = default)
    {
        var config = await GetOrCreateEntityAsync(ct);
        return config.PorcentajeIgv;
    }

    public async Task<ServiceResult<ConfiguracionEmpresaResponse>> ActualizarConfiguracionEmpresaAsync(
        ActualizarConfiguracionEmpresaRequest request,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        if (request.NombreEmpresa != null && string.IsNullOrWhiteSpace(request.NombreEmpresa))
            return ServiceResult<ConfiguracionEmpresaResponse>.Invalid("El nombre de la empresa es obligatorio.");

        if (request.PorcentajeIgv.HasValue && (request.PorcentajeIgv.Value < 0 || request.PorcentajeIgv.Value > 100))
            return ServiceResult<ConfiguracionEmpresaResponse>.Invalid("El porcentaje de IGV debe estar entre 0 y 100.");

        string? rucNormalizado = null;
        if (!string.IsNullOrWhiteSpace(request.Ruc))
        {
            var rucTrim = request.Ruc.Trim();
            if (rucTrim.Length != 11 || !rucTrim.All(char.IsDigit))
            {
                return ServiceResult<ConfiguracionEmpresaResponse>.Invalid("El RUC debe tener exactamente 11 dígitos numéricos.");
            }
            rucNormalizado = rucTrim;
        }

        if (request.TipoCambioVigente.HasValue && request.TipoCambioVigente.Value <= 0)
            return ServiceResult<ConfiguracionEmpresaResponse>.Invalid("El tipo de cambio vigente debe ser mayor a cero.");

        var config = await GetOrCreateEntityAsync(ct);

        var valoresAnteriores = new
        {
            config.NombreEmpresa,
            config.RazonSocial,
            config.Ruc,
            config.Direccion,
            config.Telefono,
            config.Email,
            config.PorcentajeIgv,
            config.MonedaBase,
            config.TipoCambioVigente
        };

        if (!string.IsNullOrWhiteSpace(request.NombreEmpresa))
            config.NombreEmpresa = request.NombreEmpresa.Trim();

        if (request.RazonSocial != null)
            config.RazonSocial = string.IsNullOrWhiteSpace(request.RazonSocial) ? config.NombreEmpresa : request.RazonSocial.Trim();

        if (request.Ruc != null)
            config.Ruc = rucNormalizado;

        if (request.Direccion != null)
            config.Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim();

        if (request.Telefono != null)
            config.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();

        if (request.Email != null)
            config.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        if (request.PorcentajeIgv.HasValue)
            config.PorcentajeIgv = request.PorcentajeIgv.Value;

        if (!string.IsNullOrWhiteSpace(request.MonedaBase))
            config.MonedaBase = request.MonedaBase.Trim().ToUpperInvariant();

        if (request.TipoCambioVigente.HasValue)
        {
            config.TipoCambioVigente = request.TipoCambioVigente.Value;
            config.FechaActualizacionTipoCambio = DateTime.UtcNow;
        }

        config.FechaModificacion = DateTime.UtcNow;

        var valoresNuevos = new
        {
            config.NombreEmpresa,
            config.RazonSocial,
            config.Ruc,
            config.Direccion,
            config.Telefono,
            config.Email,
            config.PorcentajeIgv,
            config.MonedaBase,
            config.TipoCambioVigente
        };

        _context.EventosAuditoria.Add(new EventoAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Accion = "Actualizar",
            Entidad = "ConfiguracionEmpresa",
            EntidadId = config.Id.ToString(),
            Detalle = JsonSerializer.Serialize(new
            {
                ValoresAnteriores = valoresAnteriores,
                ValoresNuevos = valoresNuevos
            })
        });

        await _context.SaveChangesAsync(ct);

        return ServiceResult<ConfiguracionEmpresaResponse>.Success(MapearEmpresa(config));
    }

    public async Task<TipoCambioResponse> ObtenerTipoCambioVigenteAsync(CancellationToken ct = default)
    {
        var config = await GetOrCreateEntityAsync(ct);

        var ultimoRegistro = await _context.HistorialTiposCambio
            .AsNoTracking()
            .Include(h => h.Usuario)
            .OrderByDescending(h => h.FechaVigencia)
            .FirstOrDefaultAsync(ct);

        bool configurado = config.TipoCambioVigente.HasValue && config.TipoCambioVigente.Value > 0;
        decimal? tipoCambio = configurado ? config.TipoCambioVigente : null;
        decimal? venta = ultimoRegistro?.ValorVenta ?? tipoCambio;
        decimal? compra = ultimoRegistro?.ValorCompra ?? tipoCambio;
        string? usuarioNombre = ultimoRegistro?.Usuario?.NombreCompleto;

        return new TipoCambioResponse(
            configurado,
            tipoCambio,
            venta,
            compra,
            config.MonedaBase ?? "PEN",
            "USD",
            config.FechaActualizacionTipoCambio ?? ultimoRegistro?.FechaVigencia,
            usuarioNombre
        );
    }

    public async Task<ServiceResult<TipoCambioResponse>> ActualizarTipoCambioAsync(
        ActualizarTipoCambioRequest request,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        decimal venta = request.ValorVenta ?? request.TipoCambio ?? request.Valor ?? 0m;
        decimal compra = request.ValorCompra ?? venta;

        if (venta <= 0 || compra <= 0)
            return ServiceResult<TipoCambioResponse>.Invalid("El valor del tipo de cambio debe ser mayor a cero.");

        var config = await GetOrCreateEntityAsync(ct);
        var valorAnterior = config.TipoCambioVigente;

        config.TipoCambioVigente = venta;
        config.FechaActualizacionTipoCambio = DateTime.UtcNow;
        config.FechaModificacion = DateTime.UtcNow;

        var registro = new HistorialTipoCambio
        {
            Id = Guid.NewGuid(),
            MonedaOrigen = string.IsNullOrWhiteSpace(request.MonedaOrigen) ? "USD" : request.MonedaOrigen.Trim().ToUpperInvariant(),
            MonedaDestino = string.IsNullOrWhiteSpace(request.MonedaDestino) ? "PEN" : request.MonedaDestino.Trim().ToUpperInvariant(),
            ValorCompra = compra,
            ValorVenta = venta,
            FechaVigencia = request.FechaVigencia ?? DateTime.UtcNow,
            UsuarioId = usuarioId,
            Observaciones = request.Observaciones?.Trim(),
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        _context.HistorialTiposCambio.Add(registro);

        _context.EventosAuditoria.Add(new EventoAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Accion = "Actualizar",
            Entidad = "TipoCambio",
            EntidadId = registro.Id.ToString(),
            Detalle = JsonSerializer.Serialize(new
            {
                ValorAnterior = valorAnterior,
                ValorNuevo = venta,
                ValorCompra = compra,
                MonedaOrigen = registro.MonedaOrigen,
                MonedaDestino = registro.MonedaDestino,
                Observaciones = registro.Observaciones
            })
        });

        await _context.SaveChangesAsync(ct);

        Usuario? usuario = null;
        if (usuarioId.HasValue)
        {
            usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId.Value, ct);
        }

        string? usuarioNombre = usuario?.NombreCompleto;

        return ServiceResult<TipoCambioResponse>.Success(new TipoCambioResponse(
            true,
            venta,
            venta,
            compra,
            config.MonedaBase ?? "PEN",
            registro.MonedaOrigen,
            config.FechaActualizacionTipoCambio,
            usuarioNombre
        ));
    }

    public async Task<List<HistorialTipoCambioResponse>> ObtenerHistorialTipoCambioAsync(CancellationToken ct = default)
    {
        var list = await _context.HistorialTiposCambio
            .AsNoTracking()
            .Include(h => h.Usuario)
            .OrderByDescending(h => h.FechaVigencia)
            .ToListAsync(ct);

        return list.Select(h => new HistorialTipoCambioResponse(
            h.Id,
            h.MonedaOrigen,
            h.MonedaDestino,
            h.ValorCompra,
            h.ValorVenta,
            h.FechaVigencia,
            h.Observaciones,
            h.UsuarioId,
            h.Usuario?.NombreCompleto,
            h.FechaCreacion
        )).ToList();
    }

    public async Task<ServiceResult<ConversionMonedaResponse>> ConvertirUsdAPenAsync(
        decimal montoUsd,
        decimal? tipoCambio = null,
        CancellationToken ct = default)
    {
        if (montoUsd < 0)
        {
            return ServiceResult<ConversionMonedaResponse>.Invalid("El monto en USD no puede ser negativo.");
        }

        decimal tc;
        if (tipoCambio.HasValue && tipoCambio.Value > 0)
        {
            tc = tipoCambio.Value;
        }
        else
        {
            var config = await GetOrCreateEntityAsync(ct);
            tc = config.TipoCambioVigente ?? 3.80m;
        }

        // Regla comercial estricta: Math.Ceiling(montoUsd * tipoCambio)
        var montoPen = Math.Ceiling(montoUsd * tc);

        var response = new ConversionMonedaResponse(
            montoUsd,
            tc,
            montoPen,
            "Math.Ceiling(montoUsd * tipoCambio)");

        return ServiceResult<ConversionMonedaResponse>.Success(response);
    }

    private static ConfiguracionEmpresaResponse MapearEmpresa(ConfiguracionEmpresa config)
    {
        return new ConfiguracionEmpresaResponse(
            config.Id,
            config.NombreEmpresa,
            config.RazonSocial,
            config.Ruc,
            config.Direccion,
            config.Telefono,
            config.Email,
            config.PorcentajeIgv,
            config.MonedaBase ?? "PEN",
            config.TipoCambioVigente,
            config.FechaActualizacionTipoCambio
        );
    }
}
