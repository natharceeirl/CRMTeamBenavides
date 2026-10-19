using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeneradorTipos;

/// <summary>Traduce los contratos de la API a tipos TypeScript.</summary>
public sealed class Generador(Assembly ensamblado)
{
    private const string EspacioFeatures = "CRMTeamBenavides.Api.Features";

    private readonly NullabilityInfoContext _nulabilidad = new();
    private readonly Dictionary<Type, string> _nombres = [];
    private readonly List<string> _avisos = [];

    public IReadOnlyList<string> Avisos => _avisos;
    public int Cantidad => _nombres.Count;

    public string Generar()
    {
        var tipos = RecogerTipos();
        AsignarNombres(tipos);

        var texto = new StringBuilder();
        texto.Append("// Generado por tools/GeneradorTipos a partir de los contratos de la API (Features/**).\n");
        texto.Append("// No editar a mano: después de cambiar un contrato, correr `npm run tipos:generar`.\n");

        foreach (var area in tipos.GroupBy(Area).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            texto.Append($"\n// ── {area.Key} ".PadRight(80, '─')).Append('\n');
            foreach (var tipo in area.OrderBy(t => _nombres[t], StringComparer.Ordinal))
            {
                texto.Append('\n');
                texto.Append(tipo.IsEnum ? Enum(tipo) : Objeto(tipo));
            }
        }

        return texto.ToString();
    }

    /// <summary>Los contratos de Features y los tipos propios que aparecen dentro de ellos (enums, DTO anidados).</summary>
    private HashSet<Type> RecogerTipos()
    {
        var tipos = new HashSet<Type>();
        var pendientes = new Queue<Type>(ensamblado.GetExportedTypes().Where(EsContrato));
        while (pendientes.TryDequeue(out var tipo))
        {
            if (!tipos.Add(tipo) || tipo.IsEnum)
            {
                continue;
            }

            foreach (var referido in PropiedadesSerializadas(tipo).SelectMany(p => TiposPropios(p.PropertyType)))
            {
                pendientes.Enqueue(referido);
            }
        }

        return tipos;
    }

    private static bool EsContrato(Type tipo)
    {
        if (tipo.Namespace?.StartsWith(EspacioFeatures, StringComparison.Ordinal) != true
            || tipo is not { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
        {
            return false;
        }

        // Los DTO del contrato de Yamaha v3 tienen su propio serializador y no los usa la web.
        if (tipo.Name.EndsWith("V3Dto", StringComparison.Ordinal))
        {
            return false;
        }

        return tipo.Name.EndsWith("Request", StringComparison.Ordinal)
               || tipo.Name.EndsWith("Response", StringComparison.Ordinal)
               || tipo.Name.EndsWith("Dto", StringComparison.Ordinal);
    }

    private void AsignarNombres(IEnumerable<Type> tipos)
    {
        foreach (var grupo in tipos.GroupBy(t => t.Name))
        {
            var repetido = grupo.Count() > 1;
            foreach (var tipo in grupo)
            {
                _nombres[tipo] = repetido ? Area(tipo) + tipo.Name : tipo.Name;
            }

            if (repetido)
            {
                _avisos.Add($"«{grupo.Key}» existe en varias áreas: se generó con el área como prefijo.");
            }
        }
    }

    /// <summary>La carpeta de Features (Clientes, Ventas…) o, fuera de Features, el último tramo del espacio de nombres.</summary>
    private static string Area(Type tipo)
    {
        var espacio = tipo.Namespace ?? string.Empty;
        if (espacio.StartsWith(EspacioFeatures + ".", StringComparison.Ordinal))
        {
            return espacio[(EspacioFeatures.Length + 1)..].Split('.')[0];
        }

        var ultimo = espacio.Split('.')[^1];
        return ultimo == "Entities" ? "Dominio" : ultimo;
    }

    private static IEnumerable<PropertyInfo> PropiedadesSerializadas(Type tipo) =>
        tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
            .Where(p => p.Name != "EqualityContract")
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always });

    private IEnumerable<Type> TiposPropios(Type tipo)
    {
        if (tipo.IsArray)
        {
            return TiposPropios(tipo.GetElementType()!);
        }

        if (tipo.IsGenericType)
        {
            return tipo.GetGenericArguments().SelectMany(TiposPropios);
        }

        return tipo.Assembly == ensamblado && (tipo.IsEnum || tipo.IsClass) ? [tipo] : [];
    }

    private static bool EsEnumComoTexto(Type tipo)
    {
        var convertidor = tipo.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType;
        return convertidor is not null
               && (convertidor == typeof(JsonStringEnumConverter)
                   || (convertidor.IsGenericType && convertidor.GetGenericTypeDefinition() == typeof(JsonStringEnumConverter<>)));
    }

    private string Enum(Type tipo)
    {
        var nombres = System.Enum.GetNames(tipo);
        if (EsEnumComoTexto(tipo))
        {
            return $"export type {_nombres[tipo]} = {string.Join(" | ", nombres.Select(n => $"'{n}'"))}\n";
        }

        var valores = nombres.Select(n => Convert.ToInt64(System.Enum.Parse(tipo, n))).ToList();
        var leyenda = string.Join(" · ", nombres.Zip(valores, (n, v) => $"{n} = {v}"));
        return $"/** {leyenda} */\nexport type {_nombres[tipo]} = {string.Join(" | ", valores.Distinct())}\n";
    }

    private string Objeto(Type tipo)
    {
        // En una petición, un parámetro con valor por defecto se puede omitir.
        var esPeticion = tipo.Name.EndsWith("Request", StringComparison.Ordinal);
        var parametrosConDefecto = esPeticion
            ? tipo.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault()?
                .GetParameters()
                .Where(p => p.HasDefaultValue)
                .Select(p => p.Name!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? []
            : [];

        var texto = new StringBuilder($"export type {_nombres[tipo]} = {{\n");
        foreach (var propiedad in PropiedadesSerializadas(tipo))
        {
            // Una propiedad calculada (solo get) sale en las respuestas, pero al leer una petición
            // la API la ignora: no es algo que la web deba mandar.
            if (esPeticion && propiedad.SetMethod is null)
            {
                continue;
            }

            var nombre = propiedad.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                         ?? JsonNamingPolicy.CamelCase.ConvertName(propiedad.Name);
            var nulabilidad = _nulabilidad.Create(propiedad);
            // Al leer una petición, la API toma lo que falta como null o su valor por defecto:
            // en la web, esos campos se pueden omitir.
            var omitible = parametrosConDefecto.Contains(propiedad.Name)
                           || (esPeticion && nulabilidad.ReadState == NullabilityState.Nullable);
            var opcional = omitible ? "?" : string.Empty;
            var tipoTs = Traducir(nulabilidad, $"{tipo.Name}.{propiedad.Name}");
            texto.Append($"  {nombre}{opcional}: {tipoTs}\n");
        }

        return texto.Append("}\n").ToString();
    }

    private string Traducir(NullabilityInfo info, string donde)
    {
        var subyacente = Nullable.GetUnderlyingType(info.Type);
        var basico = subyacente is not null ? Basico(subyacente, info, donde) : Basico(info.Type, info, donde);
        var admiteNull = subyacente is not null || info.ReadState == NullabilityState.Nullable;
        return admiteNull ? $"{basico} | null" : basico;
    }

    private string Basico(Type tipo, NullabilityInfo info, string donde)
    {
        if (Primitivo(tipo) is { } primitivo)
        {
            return primitivo;
        }

        if (tipo.IsArray)
        {
            return $"{EnLista(Traducir(info.ElementType!, donde))}[]";
        }

        if (tipo.IsGenericType)
        {
            var definicion = tipo.GetGenericTypeDefinition();
            var argumentos = info.GenericTypeArguments;
            if (Implementa(definicion, typeof(IDictionary<,>)) || Implementa(definicion, typeof(IReadOnlyDictionary<,>)))
            {
                return $"Record<string, {Traducir(argumentos[1], donde)}>";
            }

            if (Implementa(definicion, typeof(IEnumerable<>)))
            {
                return $"{EnLista(Traducir(argumentos[0], donde))}[]";
            }
        }

        if (_nombres.TryGetValue(tipo, out var nombre))
        {
            return nombre;
        }

        if (tipo.Name == "IFormFile")
        {
            return "Blob";
        }

        _avisos.Add($"{donde}: no hay traducción para {tipo.FullName}; se usa unknown.");
        return "unknown";
    }

    private static string EnLista(string tipo) => tipo.Contains(' ') ? $"({tipo})" : tipo;

    private static bool Implementa(Type definicion, Type interfazGenerica) =>
        definicion == interfazGenerica
        || definicion.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfazGenerica);

    private static string? Primitivo(Type tipo)
    {
        // Un enum tiene código de tipo numérico, pero se traduce a su propio tipo.
        if (tipo.IsEnum)
        {
            return null;
        }

        if (tipo == typeof(string) || tipo == typeof(char) || tipo == typeof(Guid) || tipo == typeof(Uri)
            || tipo == typeof(DateTime) || tipo == typeof(DateTimeOffset) || tipo == typeof(DateOnly)
            || tipo == typeof(TimeOnly) || tipo == typeof(TimeSpan) || tipo == typeof(byte[]))
        {
            return "string";
        }

        if (tipo == typeof(bool))
        {
            return "boolean";
        }

        if (tipo == typeof(object) || tipo.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true)
        {
            return "unknown";
        }

        return Type.GetTypeCode(tipo) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "number",
            _ => null,
        };
    }
}
