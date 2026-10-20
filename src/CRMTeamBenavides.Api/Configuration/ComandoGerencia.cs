using System.Text;
using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Configuration;

/// <summary>
/// Comando de una sola vez, con la misma API compilada y sin levantar el servidor web:
/// <code>
/// dotnet CRMTeamBenavides.Api.dll crear-gerencia                  # primer usuario de Gerencia
/// dotnet CRMTeamBenavides.Api.dll crear-gerencia --restablecer    # nueva contraseña para Gerencia
/// </code>
/// En Docker: <c>docker compose run --rm api crear-gerencia</c>. Pide los datos por la
/// terminal y la contraseña no se muestra, así que no queda en el historial ni en variables.
/// </summary>
public static class ComandoGerencia
{
    public const string Nombre = "crear-gerencia";
    public const string OpcionRestablecer = "--restablecer";

    public static bool EsComando(string[] args) => args.Length > 0 && args[0] == Nombre;

    /// <returns>El código de salida del proceso: 0 si se hizo el cambio.</returns>
    public static async Task<int> EjecutarAsync(IServiceProvider servicios, string[] args)
    {
        var restablecer = args.Contains(OpcionRestablecer);
        // La terminal de Windows no usa UTF-8 por defecto y desarma las tildes.
        Console.OutputEncoding = Encoding.UTF8;

        using var alcance = servicios.CreateScope();
        var gerencia = alcance.ServiceProvider.GetRequiredService<GerenciaInicialService>();

        Console.WriteLine(restablecer
            ? "Nueva contraseña para un usuario de Gerencia. También le quita el bloqueo y cierra sus sesiones."
            : "Primer usuario de Gerencia de esta base.");

        var email = Preguntar("Correo: ");
        var nombre = restablecer ? string.Empty : Preguntar("Nombre completo: ");
        var password = PreguntarOculto("Contraseña: ");
        if (PreguntarOculto("Repite la contraseña: ") != password)
        {
            Console.Error.WriteLine("Las contraseñas no coinciden. No se hizo ningún cambio.");
            return 1;
        }

        var resultado = restablecer
            ? await gerencia.RestablecerAsync(email, password)
            : await gerencia.CrearAsync(email, nombre, password);

        if (!resultado.IsSuccess)
        {
            Console.Error.WriteLine(resultado.Error);
            return 1;
        }

        Console.WriteLine(restablecer
            ? $"Listo: {email} ya puede entrar con la contraseña nueva."
            : $"Listo: {email} ya puede entrar como Gerencia.");
        return 0;
    }

    private static string Preguntar(string texto)
    {
        Console.Write(texto);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    private static string PreguntarOculto(string texto)
    {
        Console.Write(texto);

        // Sin terminal (entrada redirigida) no hay cómo ocultarla: se lee la línea.
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var password = new StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0) password.Length--;
                continue;
            }

            if (!char.IsControl(tecla.KeyChar))
            {
                password.Append(tecla.KeyChar);
            }
        }

        Console.WriteLine();
        return password.ToString();
    }
}
