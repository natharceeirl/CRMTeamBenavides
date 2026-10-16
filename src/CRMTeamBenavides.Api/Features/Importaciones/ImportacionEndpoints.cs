using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services.Importacion;
using Microsoft.AspNetCore.Mvc;

namespace CRMTeamBenavides.Api.Features.Importaciones;

public static class ImportacionEndpoints
{
    private const long TamanioMaximoBytes = 10 * 1024 * 1024; // 10 MB

    public static void MapImportacionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/importaciones")
            .RequireAuthorization(PermisosDefinidos.ConfiguracionEditar);

        // 1. Descargar plantilla Excel (.xlsx) con las 2 hojas acordadas (Clientes y Catalogo)
        group.MapGet("/plantilla", (IImportadorExcelService service) =>
        {
            var bytes = service.GenerarPlantillaExcel();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Plantilla_Importacion_CRM_TeamBenavides.xlsx");
        })
        .WithName("DescargarPlantillaImportacionExcel");

        // 2. Validar archivo Excel en modo dry-run (no modifica la base de datos)
        group.MapPost("/validar", async (
            [FromForm] IFormFile? archivo,
            [FromForm] Guid? categoriaId,
            [FromForm] string? modoDuplicados,
            HttpContext httpContext,
            ClaimsPrincipal user,
            IImportadorExcelService service,
            CancellationToken ct) =>
        {
            var categoriaResuelta = ResolverCategoriaId(categoriaId, httpContext);
            var modoResuelto = ResolverModoDuplicados(modoDuplicados, httpContext);
            var errorArchivo = ValidarArchivoSubido(archivo, categoriaResuelta, modoResuelto, soloValidacion: true);
            if (errorArchivo is not null)
            {
                return Results.BadRequest(errorArchivo);
            }

            var usuarioId = ObtenerUsuarioId(user);
            await using var stream = archivo!.OpenReadStream();
            var resultado = await service.ValidarArchivoAsync(
                stream,
                archivo.FileName,
                categoriaResuelta,
                usuarioId,
                modoResuelto,
                ct);

            return Results.Ok(resultado);
        })
        .DisableAntiforgery()
        .WithName("ValidarImportacionExcel");

        // 3. Ejecutar importación transaccional atómica
        group.MapPost("/ejecutar", async (
            [FromForm] IFormFile? archivo,
            [FromForm] Guid? categoriaId,
            [FromForm] string? modoDuplicados,
            HttpContext httpContext,
            ClaimsPrincipal user,
            IImportadorExcelService service,
            CancellationToken ct) =>
        {
            var categoriaResuelta = ResolverCategoriaId(categoriaId, httpContext);
            var modoResuelto = ResolverModoDuplicados(modoDuplicados, httpContext);
            var errorArchivo = ValidarArchivoSubido(archivo, categoriaResuelta, modoResuelto, soloValidacion: false);
            if (errorArchivo is not null)
            {
                return Results.BadRequest(errorArchivo);
            }

            var usuarioId = ObtenerUsuarioId(user);
            await using var stream = archivo!.OpenReadStream();
            var resultado = await service.EjecutarImportacionAsync(
                stream,
                archivo.FileName,
                categoriaResuelta,
                usuarioId,
                modoResuelto,
                ct);

            return resultado.Exitoso
                ? Results.Ok(resultado)
                : Results.BadRequest(resultado);
        })
        .DisableAntiforgery()
        .WithName("EjecutarImportacionExcel");

        // 4. Consultar historial de importaciones registradas en EventosAuditoria
        group.MapGet("/historial", async (
            int? limite,
            int? limit,
            IImportadorExcelService service,
            CancellationToken ct) =>
        {
            var historial = await service.ObtenerHistorialAsync(limite ?? limit ?? 50, ct);
            return Results.Ok(historial);
        })
        .WithName("ObtenerHistorialImportacionesExcel");
    }

    private static string ResolverModoDuplicados(string? modoDuplicadosForm, HttpContext httpContext)
    {
        if (!string.IsNullOrWhiteSpace(modoDuplicadosForm))
        {
            return NormalizarModoDuplicados(modoDuplicadosForm);
        }

        if (httpContext.Request.Query.TryGetValue("modoDuplicados", out var qVal)
            && !string.IsNullOrWhiteSpace(qVal.FirstOrDefault()))
        {
            return NormalizarModoDuplicados(qVal.FirstOrDefault()!);
        }

        return "ErrorSiExiste";
    }

    private static string NormalizarModoDuplicados(string modo)
    {
        if (string.Equals(modo.Trim(), "OmitirExistentes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(modo.Trim(), "omitir", StringComparison.OrdinalIgnoreCase))
        {
            return "OmitirExistentes";
        }

        return "ErrorSiExiste";
    }

    private static Guid? ResolverCategoriaId(Guid? categoriaIdForm, HttpContext httpContext)
    {
        if (categoriaIdForm.HasValue && categoriaIdForm.Value != Guid.Empty)
        {
            return categoriaIdForm.Value;
        }

        if (httpContext.Request.Query.TryGetValue("categoriaId", out var qVal)
            && Guid.TryParse(qVal.FirstOrDefault(), out var parsedQuery)
            && parsedQuery != Guid.Empty)
        {
            return parsedQuery;
        }

        return categoriaIdForm;
    }

    private static ResultadoImportacionExcelDto? ValidarArchivoSubido(
        IFormFile? archivo,
        Guid? categoriaId,
        string modoDuplicados,
        bool soloValidacion)
    {
        var vacio = new ResumenHojaImportacionDto(0, 0, 0, 0, 0);

        if (archivo is null || archivo.Length == 0)
        {
            return new ResultadoImportacionExcelDto(
                Exitoso: false,
                SoloValidacion: soloValidacion,
                NombreArchivo: archivo?.FileName ?? string.Empty,
                FechaProceso: DateTime.UtcNow,
                CategoriaId: categoriaId,
                Clientes: vacio,
                Catalogo: vacio,
                Errores:
                [
                    new ErrorFilaImportacionDto(
                        Hoja: "Archivo",
                        Fila: 0,
                        Columna: "Archivo",
                        ValorLeido: null,
                        Mensaje: "Debe adjuntar un archivo Excel (.xlsx) con contenido.")
                ],
                Unidades: vacio,
                Repuestos: vacio,
                ModoDuplicados: modoDuplicados);
        }

        if (archivo.Length > TamanioMaximoBytes)
        {
            return new ResultadoImportacionExcelDto(
                Exitoso: false,
                SoloValidacion: soloValidacion,
                NombreArchivo: archivo.FileName,
                FechaProceso: DateTime.UtcNow,
                CategoriaId: categoriaId,
                Clientes: vacio,
                Catalogo: vacio,
                Errores:
                [
                    new ErrorFilaImportacionDto(
                        Hoja: "Archivo",
                        Fila: 0,
                        Columna: "Tamanio",
                        ValorLeido: $"{archivo.Length} bytes",
                        Mensaje: "El archivo supera el tamaño máximo permitido de 10 MB.")
                ],
                Unidades: vacio,
                Repuestos: vacio,
                ModoDuplicados: modoDuplicados);
        }

        var ext = Path.GetExtension(archivo.FileName);
        if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return new ResultadoImportacionExcelDto(
                Exitoso: false,
                SoloValidacion: soloValidacion,
                NombreArchivo: archivo.FileName,
                FechaProceso: DateTime.UtcNow,
                CategoriaId: categoriaId,
                Clientes: vacio,
                Catalogo: vacio,
                Errores:
                [
                    new ErrorFilaImportacionDto(
                        Hoja: "Archivo",
                        Fila: 0,
                        Columna: "Extension",
                        ValorLeido: ext,
                        Mensaje: "Solo se admiten archivos en formato Excel (.xlsx).")
                ],
                Unidades: vacio,
                Repuestos: vacio,
                ModoDuplicados: modoDuplicados);
        }

        return null;
    }

    private static Guid? ObtenerUsuarioId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("uid");
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}
