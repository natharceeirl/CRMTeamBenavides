namespace CRMTeamBenavides.Api.Features.Yamaha;

public record YamahaEspecificacionesDto(
    string MotorTipo,
    int CilindradaCc,
    decimal PotenciaHp,
    decimal TorqueNm,
    string Refrigeracion,
    string CapacidadTanque,
    string CapacidadAceiteMotor,
    string TipoBujiaRecomendada,
    string PresionNeumaticos);

public record YamahaConsultaMockResponse(
    bool EsMock,
    string AvisoLegal,
    string CriterioConsultado,
    string TipoBusqueda,
    string CodigoModelo,
    string NombreComercial,
    int? AnioFabricacion,
    string Categoria,
    string? VinEjemplo,
    string? NumeroMotorEjemplo,
    string EstadoGarantia,
    IReadOnlyList<string> CampaniasServicio,
    IReadOnlyList<string> IntervalosMantenimiento,
    YamahaEspecificacionesDto Especificaciones,
    DateTime FechaConsultaUtc);
