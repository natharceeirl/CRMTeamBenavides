using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Almacenamiento;

public class AlmacenamientoOptions
{
    public const string SectionName = "Almacenamiento";

    public string RutaBase { get; set; } = "uploads";
    public int TamanioMaximoMb { get; set; } = 10;
}

public class AlmacenamientoLocalService : IAlmacenamientoArchivoService
{
    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> MimeTypesPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private static readonly Dictionary<string, string> ExtensionesPorMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly string _rutaBaseAbsoluta;
    private readonly long _tamanioMaximoBytes;
    private readonly ILogger<AlmacenamientoLocalService> _logger;

    public AlmacenamientoLocalService(
        IWebHostEnvironment env,
        IOptions<AlmacenamientoOptions> options,
        ILogger<AlmacenamientoLocalService> logger)
    {
        _logger = logger;
        var opts = options.Value ?? new AlmacenamientoOptions();
        _tamanioMaximoBytes = (long)Math.Max(1, opts.TamanioMaximoMb) * 1024 * 1024;

        var configuredPath = string.IsNullOrWhiteSpace(opts.RutaBase) ? "uploads" : opts.RutaBase;
        _rutaBaseAbsoluta = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configuredPath));

        if (!Directory.Exists(_rutaBaseAbsoluta))
        {
            Directory.CreateDirectory(_rutaBaseAbsoluta);
        }
    }

    public async Task<(string rutaRelativa, string nombreAlmacenado)> GuardarFotoAsync(
        Stream stream,
        string nombreOriginal,
        string contentType,
        string subcarpeta,
        CancellationToken ct = default)
    {
        if (stream == null || (stream.CanSeek && stream.Length == 0))
        {
            throw new ArgumentException("El archivo está vacío o no es válido.", nameof(stream));
        }

        if (stream.CanSeek && stream.Length > _tamanioMaximoBytes)
        {
            throw new InvalidOperationException($"El archivo excede el tamaño máximo permitido ({_tamanioMaximoBytes / (1024 * 1024)} MB).");
        }

        var ext = Path.GetExtension(nombreOriginal ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !ExtensionesPermitidas.Contains(ext))
        {
            throw new InvalidOperationException($"La extensión '{ext}' no está permitida. Solo se aceptan: {string.Join(", ", ExtensionesPermitidas)}.");
        }

        var normalizedContentType = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!MimeTypesPermitidos.Contains(normalizedContentType))
        {
            // Si el mime enviado es genérico o application/octet-stream pero la extensión es válida
            if (ExtensionesPermitidas.Contains(ext))
            {
                normalizedContentType = ext switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };
            }
            else
            {
                throw new InvalidOperationException($"El tipo de contenido '{contentType}' no está permitido para fotografías.");
            }
        }

        // Sanitizar subcarpeta para evitar path traversal
        var subcarpetaLimpia = Path.GetFileName(subcarpeta?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(subcarpetaLimpia))
        {
            subcarpetaLimpia = "general";
        }

        var nombreAlmacenado = $"{Guid.NewGuid():N}{ext}";
        var carpetaDestino = Path.GetFullPath(Path.Combine(_rutaBaseAbsoluta, subcarpetaLimpia));

        // Validación estricta anti path traversal
        if (!carpetaDestino.StartsWith(_rutaBaseAbsoluta, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ruta de destino inválida.");
        }

        if (!Directory.Exists(carpetaDestino))
        {
            Directory.CreateDirectory(carpetaDestino);
        }

        var rutaArchivoCompleta = Path.GetFullPath(Path.Combine(carpetaDestino, nombreAlmacenado));
        if (!rutaArchivoCompleta.StartsWith(carpetaDestino, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ruta de archivo inválida.");
        }

        await using (var fileStream = new FileStream(rutaArchivoCompleta, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
            await stream.CopyToAsync(fileStream, ct);
        }

        // Verificar tamaño real post-escritura
        var info = new FileInfo(rutaArchivoCompleta);
        if (info.Length > _tamanioMaximoBytes)
        {
            File.Delete(rutaArchivoCompleta);
            throw new InvalidOperationException($"El archivo guardado excede el tamaño máximo permitido ({_tamanioMaximoBytes / (1024 * 1024)} MB).");
        }

        var rutaRelativa = $"{subcarpetaLimpia}/{nombreAlmacenado}";
        return (rutaRelativa, nombreAlmacenado);
    }

    public Task<(Stream stream, string contentType)?> ObtenerArchivoAsync(
        string rutaRelativa,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            return Task.FromResult<(Stream stream, string contentType)?>(null);
        }

        // Sanitizar y validar path traversal
        var partes = rutaRelativa.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rutaCompleta = Path.GetFullPath(Path.Combine(_rutaBaseAbsoluta, Path.Combine(partes)));

        if (!rutaCompleta.StartsWith(_rutaBaseAbsoluta, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Intento de path traversal detectado al consultar archivo: {Ruta}", rutaRelativa);
            return Task.FromResult<(Stream stream, string contentType)?>(null);
        }

        if (!File.Exists(rutaCompleta))
        {
            return Task.FromResult<(Stream stream, string contentType)?>(null);
        }

        var ext = Path.GetExtension(rutaCompleta).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        Stream fileStream = new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return Task.FromResult<(Stream stream, string contentType)?>((fileStream, contentType));
    }

    public Task<bool> EliminarArchivoAsync(string rutaRelativa, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            return Task.FromResult(false);
        }

        try
        {
            var partes = rutaRelativa.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var rutaCompleta = Path.GetFullPath(Path.Combine(_rutaBaseAbsoluta, Path.Combine(partes)));

            if (!rutaCompleta.StartsWith(_rutaBaseAbsoluta, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Intento de path traversal detectado al eliminar archivo: {Ruta}", rutaRelativa);
                return Task.FromResult(false);
            }

            if (File.Exists(rutaCompleta))
            {
                File.Delete(rutaCompleta);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar archivo físico: {Ruta}", rutaRelativa);
        }

        return Task.FromResult(false);
    }
}
