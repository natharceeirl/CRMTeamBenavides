using CRMTeamBenavides.Api.Features.Yamaha;

namespace CRMTeamBenavides.Api.Services.Yamaha;

public class YamahaMockService : IYamahaService
{
    private record ModeloMock(
        string CodigoModelo,
        string NombreComercial,
        int Anio,
        string Categoria,
        string PrefijoVin,
        string VinEjemplo,
        string MotorEjemplo,
        string EstadoGarantia,
        string[] Campanias,
        string[] Intervalos,
        YamahaEspecificacionesDto Especificaciones,
        string[] PalabrasClave);

    private static readonly List<ModeloMock> CatalogoMock = new()
    {
        new ModeloMock(
            CodigoModelo: "MT-03",
            NombreComercial: "Yamaha MT-03 ABS",
            Anio: 2024,
            Categoria: "Hyper Naked",
            PrefijoVin: "9C6RG43",
            VinEjemplo: "9C6RG4300P0001234",
            MotorEjemplo: "G3J8E-004521",
            EstadoGarantia: "Vigente (Garantía Oficial de Fábrica 2 años o 20,000 km)",
            Campanias: new[]
            {
                "Campaña MOCK: Actualización del software ECU para optimización de ralentí (Realizada)",
                "Campaña MOCK: Inspección de conducto de líquido de frenos delantero (Al día)"
            },
            Intervalos: new[]
            {
                "1,000 km o 1 mes: Primer cambio de aceite Yamalube 10W-40 y filtro de aceite.",
                "5,000 km o 6 meses: Inspección de bujías, tensión de cadena y nivel de refrigerante.",
                "10,000 km o 12 meses: Reemplazo de bujías, filtro de aire y líquido de frenos."
            },
            Especificaciones: new YamahaEspecificacionesDto(
                MotorTipo: "Bicilíndrico en paralelo, 4 tiempos, DOHC, 4 válvulas",
                CilindradaCc: 321,
                PotenciaHp: 42.0m,
                TorqueNm: 29.6m,
                Refrigeracion: "Líquida",
                CapacidadTanque: "14.0 Litros",
                CapacidadAceiteMotor: "2.40 Litros (con cambio de filtro)",
                TipoBujiaRecomendada: "NGK CR8E",
                PresionNeumaticos: "Delantero: 29 PSI / Trasero: 33 PSI"),
            PalabrasClave: new[] { "mt03", "mt-03", "321", "9c6rg43" }),

        new ModeloMock(
            CodigoModelo: "MT-07",
            NombreComercial: "Yamaha MT-07 ABS",
            Anio: 2023,
            Categoria: "Hyper Naked",
            PrefijoVin: "JYARN07",
            VinEjemplo: "JYARN0700P0005678",
            MotorEjemplo: "M401E-012890",
            EstadoGarantia: "Vigente (Garantía Extendida Activa)",
            Campanias: new[]
            {
                "Campaña MOCK: Reemplazo preventivo de arandela de sensor de ABS (Completada)"
            },
            Intervalos: new[]
            {
                "1,000 km o 1 mes: Cambio de aceite Yamalube Full Synthetic y filtro.",
                "10,000 km o 12 meses: Servicio preventivo mayor e inspección de sincronización CP2.",
                "20,000 km o 24 meses: Reemplazo de bujías láser y líquido refrigerante."
            },
            Especificaciones: new YamahaEspecificacionesDto(
                MotorTipo: "2 cilindros Crossplane (CP2), 4 tiempos, DOHC, 4 válvulas",
                CilindradaCc: 689,
                PotenciaHp: 73.4m,
                TorqueNm: 67.0m,
                Refrigeracion: "Líquida",
                CapacidadTanque: "14.0 Litros",
                CapacidadAceiteMotor: "2.60 Litros",
                TipoBujiaRecomendada: "NGK LMAR8A-9",
                PresionNeumaticos: "Delantero: 33 PSI / Trasero: 36 PSI"),
            PalabrasClave: new[] { "mt07", "mt-07", "689", "cp2", "jyarn07" }),

        new ModeloMock(
            CodigoModelo: "FZ-25",
            NombreComercial: "Yamaha FZ-25 ABS",
            Anio: 2024,
            Categoria: "Street / Urbano",
            PrefijoVin: "ME4RG06",
            VinEjemplo: "ME4RG0600R0009988",
            MotorEjemplo: "G3L4E-008745",
            EstadoGarantia: "Vigente (Garantía Oficial de Fábrica)",
            Campanias: new[]
            {
                "Campaña MOCK: Verificación de par de apriete en soporte de estribo (Sin observaciones)"
            },
            Intervalos: new[]
            {
                "1,000 km o 1 mes: Cambio de aceite y limpieza de tamiz.",
                "3,000 km o 3 meses: Cambio de aceite Yamalube 20W-50.",
                "6,000 km o 6 meses: Mantenimiento periódico general y calibración de válvulas."
            },
            Especificaciones: new YamahaEspecificacionesDto(
                MotorTipo: "Monocilíndrico, 4 tiempos, SOHC, 2 válvulas, Blue Core",
                CilindradaCc: 249,
                PotenciaHp: 20.8m,
                TorqueNm: 20.1m,
                Refrigeracion: "Aire y radiador de aceite",
                CapacidadTanque: "14.0 Litros",
                CapacidadAceiteMotor: "1.45 Litros",
                TipoBujiaRecomendada: "NGK DR8EA",
                PresionNeumaticos: "Delantero: 28 PSI / Trasero: 33 PSI"),
            PalabrasClave: new[] { "fz25", "fz-25", "250", "bluecore", "me4rg06" }),

        new ModeloMock(
            CodigoModelo: "NMAX-155",
            NombreComercial: "Yamaha NMAX Connected 155 ABS",
            Anio: 2024,
            Categoria: "Scooter / Maxi-Scooter",
            PrefijoVin: "MH3SG43",
            VinEjemplo: "MH3SG4300P0003412",
            MotorEjemplo: "E3T4E-001298",
            EstadoGarantia: "Vigente (Garantía de Fábrica)",
            Campanias: new string[0],
            Intervalos: new[]
            {
                "1,000 km: Cambio de aceite de motor y aceite final de transmisión.",
                "4,000 km: Inspección de correa CVT y rodillos.",
                "8,000 km: Reemplazo de aceite, bujía y filtro de aire."
            },
            Especificaciones: new YamahaEspecificacionesDto(
                MotorTipo: "Monocilíndrico Blue Core con VVA, SOHC, 4 válvulas",
                CilindradaCc: 155,
                PotenciaHp: 15.1m,
                TorqueNm: 13.9m,
                Refrigeracion: "Líquida",
                CapacidadTanque: "7.1 Litros",
                CapacidadAceiteMotor: "0.90 Litros (Motor) / 0.10 Litros (Transmisión)",
                TipoBujiaRecomendada: "NGK CPR8EA-9",
                PresionNeumaticos: "Delantero: 29 PSI / Trasero: 33 PSI"),
            PalabrasClave: new[] { "nmax", "nmax155", "nmax-155", "scooter", "mh3sg43" }),

        new ModeloMock(
            CodigoModelo: "WAVERUNNER-FX",
            NombreComercial: "Yamaha WaveRunner FX Cruiser SVHO",
            Anio: 2023,
            Categoria: "Moto Acuática (Watercraft)",
            PrefijoVin: "YAMFX18",
            VinEjemplo: "YAMFX1800N0007788",
            MotorEjemplo: "6ET-000412",
            EstadoGarantia: "Vigente (Garantía Marina Yamaha)",
            Campanias: new[]
            {
                "Campaña MOCK: Inspección de sellado de casco e impulsor jet (Aprobada)"
            },
            Intervalos: new[]
            {
                "10 Horas: Primer cambio de aceite marino Yamalube 4W y filtro de aceite.",
                "50 Horas o 6 meses: Engrase de eje de transmisión y cambio de bujías marinas.",
                "100 Horas o 12 meses: Mantenimiento mayor, inspección de turbina y ánodos de sacrificio."
            },
            Especificaciones: new YamahaEspecificacionesDto(
                MotorTipo: "4 cilindros, 4 tiempos, Super Vortex High Output (SVHO), Supercargado",
                CilindradaCc: 1812,
                PotenciaHp: 250.0m,
                TorqueNm: 280.0m,
                Refrigeracion: "Circuito abierto por agua de mar / río",
                CapacidadTanque: "70.0 Litros",
                CapacidadAceiteMotor: "5.30 Litros",
                TipoBujiaRecomendada: "NGK LFR6A",
                PresionNeumaticos: "No aplica (propulsión a chorro de agua)"),
            PalabrasClave: new[] { "waverunner", "acuática", "acuatica", "svho", "yamfx18", "fx cruiser" }),

        new ModeloMock(
            CodigoModelo: "GEN-EF2000IS",
            NombreComercial: "Generador Yamaha EF2000iS Inverter",
            Anio: 2023,
            Categoria: "Fuerza / Generador Portátil",
            PrefijoVin: "7PB00",
            VinEjemplo: "7PB001234567890",
            MotorEjemplo: "MZ80-001234",
            EstadoGarantia: "Vigente (Garantía Equipos de Potencia)",
            Campanias: new string[0],
            Intervalos: new[]
            {
                "20 Horas o 1 mes: Cambio de aceite de rodaje inicial.",
                "50 Horas o 3 meses: Limpieza de filtro de aire y trampa de combustible.",
                "100 Horas o 6 meses: Cambio de bujía e inspección de juego de válvulas."
            },
            Especificaciones: new YamahaEspecificacionesDto(
                MotorTipo: "Monocilíndrico 4 tiempos, OHV, refrigerado por aire forzado",
                CilindradaCc: 79,
                PotenciaHp: 2.7m,
                TorqueNm: 5.2m,
                Refrigeracion: "Aire",
                CapacidadTanque: "4.4 Litros",
                CapacidadAceiteMotor: "0.40 Litros (Yamalube 10W-30)",
                TipoBujiaRecomendada: "NGK BPR6HS",
                PresionNeumaticos: "No aplica (patas amortiguadoras de goma)"),
            PalabrasClave: new[] { "generador", "ef2000", "ef2000is", "inverter", "7pb00" })
    };

    public Task<YamahaConsultaMockResponse?> ConsultarAsync(string criterio, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(criterio))
        {
            return Task.FromResult<YamahaConsultaMockResponse?>(null);
        }

        var normalized = criterio.Trim().ToLowerInvariant().Replace("-", "").Replace(" ", "");

        var match = CatalogoMock.FirstOrDefault(m =>
            m.CodigoModelo.ToLowerInvariant().Replace("-", "").Contains(normalized) ||
            normalized.Contains(m.CodigoModelo.ToLowerInvariant().Replace("-", "")) ||
            m.PrefijoVin.ToLowerInvariant().StartsWith(normalized) ||
            normalized.StartsWith(m.PrefijoVin.ToLowerInvariant()) ||
            m.PalabrasClave.Any(k => normalized.Contains(k.Replace("-", "")) || k.Replace("-", "").Contains(normalized)));

        if (match == null)
        {
            return Task.FromResult<YamahaConsultaMockResponse?>(null);
        }

        var esPorVin = match.PrefijoVin.ToLowerInvariant().StartsWith(normalized) ||
                       normalized.StartsWith(match.PrefijoVin.ToLowerInvariant()) ||
                       normalized.Length >= 10;

        var response = new YamahaConsultaMockResponse(
            EsMock: true,
            AvisoLegal: "MOCK / DEMOSTRACIÓN: Información técnica simulada localmente con fines de pruebas. No realiza conexiones externas a servicios de Yamaha Motor.",
            CriterioConsultado: criterio.Trim(),
            TipoBusqueda: esPorVin ? "Búsqueda por VIN / Serie" : "Búsqueda por Modelo / Catálogo",
            CodigoModelo: match.CodigoModelo,
            NombreComercial: match.NombreComercial,
            AnioFabricacion: match.Anio,
            Categoria: match.Categoria,
            VinEjemplo: match.VinEjemplo,
            NumeroMotorEjemplo: match.MotorEjemplo,
            EstadoGarantia: match.EstadoGarantia,
            CampaniasServicio: match.Campanias,
            IntervalosMantenimiento: match.Intervalos,
            Especificaciones: match.Especificaciones,
            FechaConsultaUtc: DateTime.UtcNow);

        return Task.FromResult<YamahaConsultaMockResponse?>(response);
    }
}
