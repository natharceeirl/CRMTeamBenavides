using System.Text.RegularExpressions;

namespace CRMTeamBenavides.Api.Services;

/// <summary>El nombre del cliente listo para guardar.</summary>
public sealed record NombreResuelto(string NombreCompleto, string? Nombres, string? ApellidoPaterno, string? ApellidoMaterno);

/// <summary>
/// Una persona se registra con nombres, primer apellido y segundo apellido, como en su
/// DNI; el nombre completo que se ve en órdenes, ventas y comprobantes los une en ese
/// orden. Una empresa (o un cliente registrado antes) tiene solo el nombre completo.
/// </summary>
public static partial class NombreDeCliente
{
    public const int Maximo = 200;

    public static (NombreResuelto? Nombre, string? Error) Resolver(
        string? nombreCompleto, string? nombres, string? apellidoPaterno, string? apellidoMaterno)
    {
        var limpioNombres = Limpiar(nombres);
        if (limpioNombres is not null)
        {
            var paterno = Limpiar(apellidoPaterno);
            var materno = Limpiar(apellidoMaterno);
            var completo = string.Join(' ', new[] { limpioNombres, paterno, materno }.Where(p => p is not null));
            return completo.Length > Maximo
                ? (null, $"El nombre del cliente no puede pasar de {Maximo} caracteres.")
                : (new NombreResuelto(completo, limpioNombres, paterno, materno), null);
        }

        // Sin nombres separados: una empresa o un cliente de antes.
        var solo = Limpiar(nombreCompleto);
        if (solo is null)
            return (null, "Escribe el nombre del cliente.");
        return solo.Length > Maximo
            ? (null, $"El nombre del cliente no puede pasar de {Maximo} caracteres.")
            : (new NombreResuelto(solo, null, null, null), null);
    }

    private static string? Limpiar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : Espacios().Replace(texto.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}
