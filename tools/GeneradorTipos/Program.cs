// Genera los tipos TypeScript de la web a partir de los contratos de la API
// (records públicos de Features/** terminados en Request, Response o Dto), para que
// un cambio en el back se note al compilar la web y no en producción.
//
//   dotnet run --project tools/GeneradorTipos -- src/web/src/api/generado/contratos.ts
//   dotnet run --project tools/GeneradorTipos -- src/web/src/api/generado/contratos.ts --verificar
//
// Sigue cómo serializa la API: propiedades en camelCase, enums como número salvo los
// marcados con JsonStringEnumConverter, y `| null` donde C# admite null.
using System.Text;
using CRMTeamBenavides.Data;
using GeneradorTipos;

var argumentos = args.ToList();
var verificar = argumentos.Remove("--verificar");
if (argumentos.Count != 1)
{
    Console.Error.WriteLine("Uso: GeneradorTipos <salida.ts> [--verificar]");
    return 2;
}

var salida = Path.GetFullPath(argumentos[0]);
var generador = new Generador(typeof(ApplicationDbContext).Assembly);
var texto = generador.Generar();

foreach (var aviso in generador.Avisos)
{
    Console.Error.WriteLine($"Aviso: {aviso}");
}

if (verificar)
{
    var actual = File.Exists(salida) ? File.ReadAllText(salida).ReplaceLineEndings("\n") : string.Empty;
    if (actual != texto)
    {
        Console.Error.WriteLine($"{salida} no coincide con los contratos de la API.");
        Console.Error.WriteLine("Corre `npm run tipos:generar` en src/web y revisa qué cambió.");
        return 1;
    }

    Console.WriteLine($"Los {generador.Cantidad} tipos de la web coinciden con los contratos de la API.");
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(salida)!);
File.WriteAllText(salida, texto, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.WriteLine($"{generador.Cantidad} tipos escritos en {salida}");
return 0;
