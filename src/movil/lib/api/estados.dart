/// Estados de la orden de servicio, iguales a los del backend
/// (enum EstadoOrdenServicio). La API los manda como número en `estadoId` y
/// como texto en `estado`.
class EstadoOrden {
  const EstadoOrden._();

  static const abierta = 0;
  static const diagnostico = 1;
  static const aprobada = 2;
  static const enProceso = 3;
  static const lista = 4;
  static const entregada = 5;
  static const cancelada = 6;
}

const nombresEstadoOrden = <int, String>{
  EstadoOrden.abierta: 'Abierta',
  EstadoOrden.diagnostico: 'Diagnóstico',
  EstadoOrden.aprobada: 'Aprobada',
  EstadoOrden.enProceso: 'En proceso',
  EstadoOrden.lista: 'Lista',
  EstadoOrden.entregada: 'Entregada',
  EstadoOrden.cancelada: 'Cancelada',
};

String nombreEstadoOrden(int estadoId) => nombresEstadoOrden[estadoId] ?? 'Desconocido';

/// Transiciones que acepta el backend (OrdenServicioService.CambiarEstadoAsync).
const transicionesOrden = <int, List<int>>{
  EstadoOrden.abierta: [EstadoOrden.diagnostico, EstadoOrden.cancelada],
  EstadoOrden.diagnostico: [EstadoOrden.aprobada, EstadoOrden.cancelada],
  EstadoOrden.aprobada: [EstadoOrden.enProceso, EstadoOrden.cancelada],
  EstadoOrden.enProceso: [EstadoOrden.lista, EstadoOrden.cancelada],
  EstadoOrden.lista: [EstadoOrden.entregada, EstadoOrden.enProceso, EstadoOrden.cancelada],
  EstadoOrden.entregada: [],
  EstadoOrden.cancelada: [],
};

/// El técnico no hace la aprobación final, la entrega ni la anulación.
const estadosVedadosAlTecnico = {
  EstadoOrden.aprobada,
  EstadoOrden.entregada,
  EstadoOrden.cancelada,
};

/// Enum EstadoPresupuestoCliente del backend.
class EstadoPresupuesto {
  const EstadoPresupuesto._();

  static const pendiente = 0;
  static const aprobado = 1;
  static const rechazado = 2;
}

/// Qué respuestas al presupuesto se pueden dar todavía, con la regla del backend:
/// solo con la orden en Abierta o Diagnóstico, y nunca después de aprobado. Si se
/// rechazó y la orden sigue en diagnóstico, el cliente aún puede aprobarlo.
List<int> respuestasPresupuesto(int estadoOrden, int presupuesto) {
  final enEtapa = estadoOrden == EstadoOrden.abierta || estadoOrden == EstadoOrden.diagnostico;
  if (!enEtapa || presupuesto == EstadoPresupuesto.aprobado) return const [];
  return presupuesto == EstadoPresupuesto.rechazado
      ? const [EstadoPresupuesto.aprobado]
      : const [EstadoPresupuesto.aprobado, EstadoPresupuesto.rechazado];
}

const nombresEstadoPresupuesto = <int, String>{
  EstadoPresupuesto.pendiente: 'Pendiente',
  EstadoPresupuesto.aprobado: 'Aprobado',
  EstadoPresupuesto.rechazado: 'Rechazado',
};

/// Enum EstadoAprobacionGerencia del backend.
class EstadoGerencia {
  const EstadoGerencia._();

  static const noAplica = 0;
  static const pendiente = 1;
  static const aprobado = 2;
  static const rechazado = 3;
}

const nombresEstadoGerencia = <int, String>{
  EstadoGerencia.noAplica: 'No aplica',
  EstadoGerencia.pendiente: 'Pendiente',
  EstadoGerencia.aprobado: 'Aprobada',
  EstadoGerencia.rechazado: 'Rechazada',
};

/// Por qué la orden no puede pasar a «Aprobada», o null si puede. Son las
/// reglas de OrdenServicioService.CambiarEstadoAsync.
String? motivoBloqueoAprobacion({int? presupuesto, int? gerencia}) {
  if (presupuesto == EstadoPresupuesto.rechazado) {
    return 'El cliente rechazó el presupuesto.';
  }
  if (gerencia == EstadoGerencia.pendiente) {
    return 'Falta la aprobación de Gerencia.';
  }
  if (gerencia == EstadoGerencia.rechazado) {
    return 'Gerencia rechazó la orden.';
  }
  return null;
}

/// Enum TipoAtencion del backend.
const nombresTipoAtencion = <int, String>{
  0: 'Mantenimiento preventivo',
  1: 'Mantenimiento correctivo',
  2: 'Reclamo de garantía',
  3: 'Gratuito',
};

/// Enum TipoFalla del backend.
const nombresTipoFalla = <int, String>{0: 'Menor', 1: 'Mayor'};

/// Enum TipoUnidad del backend.
const nombresTipoUnidad = <int, String>{
  0: 'Motocicleta',
  1: 'Cuatrimoto',
  2: 'Moto acuática',
  3: 'Generador',
  4: 'Otro',
};

/// Enum TipoMedidor del backend: 0 kilómetros, 1 horas.
const medidorHoras = 1;

/// Enum TipoItemServicio del backend.
class TipoItem {
  const TipoItem._();

  static const repuesto = 0;
  static const servicio = 1;
  static const manoDeObra = 2;
  static const terceros = 3;
}

const nombresTipoItem = <int, String>{
  TipoItem.repuesto: 'Repuesto',
  TipoItem.servicio: 'Servicio',
  TipoItem.manoDeObra: 'Mano de obra',
  TipoItem.terceros: 'Terceros',
};

/// Repuestos y servicios salen del catálogo con su precio. La mano de obra
/// libre y los terceros llevan un precio que alguien fija: sin
/// `precios.modificar` el backend los rechaza.
List<int> tiposDeItemPermitidos({required bool puedeFijarPrecios}) => [
      TipoItem.repuesto,
      TipoItem.servicio,
      if (puedeFijarPrecios) ...[TipoItem.manoDeObra, TipoItem.terceros],
    ];

/// Enum TipoAfectacionIgv del backend.
const nombresAfectacionIgv = <int, String>{0: 'Gravado', 1: 'Exonerado', 2: 'Inafecto'};

/// La orden admite ítems nuevos salvo en Lista o cerrada.
bool permiteEditarItems(int estadoId) =>
    !esEstadoTerminal(estadoId) && estadoId != EstadoOrden.lista;

/// Entregada y Cancelada no admiten más cambios.
bool esEstadoTerminal(int estadoId) =>
    estadoId == EstadoOrden.entregada || estadoId == EstadoOrden.cancelada;

/// El backend rechaza el diagnóstico en órdenes entregadas o anuladas.
bool permiteDiagnostico(int estadoId) => !esEstadoTerminal(estadoId);

/// Las que siguen físicamente en el taller.
bool estaEnTaller(int estadoId) => !esEstadoTerminal(estadoId);

/// Tipos de movimiento de inventario, iguales a los del backend
/// (enum TipoMovimientoInventario). La API los manda como número en `tipoId`
/// y como texto en `tipo`.
class TipoMovimiento {
  const TipoMovimiento._();

  static const entrada = 0;
  static const salida = 1;
  static const ajuste = 2;
}

const nombresTipoMovimiento = <int, String>{
  TipoMovimiento.entrada: 'Entrada',
  TipoMovimiento.salida: 'Salida',
  TipoMovimiento.ajuste: 'Ajuste',
};

String nombreTipoMovimiento(int tipoId) =>
    nombresTipoMovimiento[tipoId] ?? 'Desconocido';

/// Estados de la venta, iguales a los del backend (enum EstadoVenta). La API
/// los manda como número en `estadoId` y como texto en `estado`.
class EstadoVenta {
  const EstadoVenta._();

  static const cotizacion = 0;
  static const confirmada = 1;
  static const anulada = 2;
}

const nombresEstadoVenta = <int, String>{
  EstadoVenta.cotizacion: 'Cotización',
  EstadoVenta.confirmada: 'Confirmada',
  EstadoVenta.anulada: 'Anulada',
};

String nombreEstadoVenta(int estadoId) =>
    nombresEstadoVenta[estadoId] ?? 'Desconocido';

/// Una venta anulada ya no cuenta para caja ni para stock.
bool esVentaVigente(int estadoId) => estadoId != EstadoVenta.anulada;

/// Situación de un paso en el avance que ve el cliente.
enum SituacionPaso { hecho, actual, pendiente }

class PasoAvance {
  const PasoAvance({
    required this.titulo,
    required this.situacion,
    this.fecha,
    this.estimada = false,
  });

  final String titulo;
  final SituacionPaso situacion;
  final DateTime? fecha;

  /// La fecha es la entrega estimada, todavía no pasó.
  final bool estimada;
}

/// Los estados de la orden contados como los entiende el cliente, en orden.
const _pasosDelCliente = <int, String>{
  EstadoOrden.abierta: 'Recibida',
  EstadoOrden.diagnostico: 'Diagnóstico',
  EstadoOrden.aprobada: 'Presupuesto aprobado',
  EstadoOrden.enProceso: 'En reparación',
  EstadoOrden.lista: 'Lista para recoger',
  EstadoOrden.entregada: 'Entregada',
};

/// Avance de la orden para el cliente: cada paso con la última vez que la
/// orden llegó a él. Si vuelve de «Lista» a «En proceso» (reingreso), «Lista»
/// queda pendiente otra vez. La entrega estimada va en «Lista para recoger».
List<PasoAvance> pasosDeAvance({
  required int estadoId,
  required List<({int estado, DateTime fecha})> cambios,
  DateTime? fechaIngreso,
  DateTime? fechaEstimadaEntrega,
}) {
  DateTime? ultimaVez(int estado) {
    DateTime? fecha;
    for (final cambio in cambios) {
      if (cambio.estado == estado && (fecha == null || cambio.fecha.isAfter(fecha))) {
        fecha = cambio.fecha;
      }
    }
    return fecha;
  }

  return [
    for (final paso in _pasosDelCliente.entries)
      () {
        final situacion = paso.key < estadoId
            ? SituacionPaso.hecho
            : paso.key == estadoId
                ? SituacionPaso.actual
                : SituacionPaso.pendiente;
        final llego = situacion != SituacionPaso.pendiente;
        final fecha = llego
            ? (paso.key == EstadoOrden.abierta ? (fechaIngreso ?? ultimaVez(paso.key)) : ultimaVez(paso.key))
            : (paso.key == EstadoOrden.lista ? fechaEstimadaEntrega : null);
        return PasoAvance(
          titulo: paso.value,
          situacion: situacion,
          fecha: fecha,
          estimada: !llego && fecha != null,
        );
      }(),
  ];
}

/// Estados de la cita (EstadoCita del backend). Viajan como texto, no como
/// número: el enum lleva JsonStringEnumConverter.
class EstadoCita {
  const EstadoCita._();

  static const pendiente = 'Pendiente';
  static const confirmada = 'Confirmada';
  static const enTaller = 'EnTaller';
  static const completada = 'Completada';
  static const cancelada = 'Cancelada';
  static const noAsistio = 'NoAsistio';
}

const nombresEstadoCita = <String, String>{
  EstadoCita.pendiente: 'Pendiente',
  EstadoCita.confirmada: 'Confirmada',
  EstadoCita.enTaller: 'En taller',
  EstadoCita.completada: 'Completada',
  EstadoCita.cancelada: 'Cancelada',
  EstadoCita.noAsistio: 'No asistió',
};

bool esCitaFinal(String estado) =>
    estado == EstadoCita.completada || estado == EstadoCita.cancelada || estado == EstadoCita.noAsistio;

/// La cita se cancela mientras la unidad no haya llegado al taller (CitaService.CancelarAsync).
bool citaAntesDelTaller(String estado) => estado == EstadoCita.pendiente || estado == EstadoCita.confirmada;

/// A qué estados puede pasar una cita (CitaService.CambiarEstadoAsync): no se
/// retrocede desde el taller y una pendiente no se completa sin pasar por él.
List<String> siguientesEstadosCita(String estado) => switch (estado) {
      EstadoCita.pendiente => const [EstadoCita.confirmada, EstadoCita.enTaller, EstadoCita.noAsistio],
      EstadoCita.confirmada => const [EstadoCita.enTaller, EstadoCita.noAsistio],
      EstadoCita.enTaller => const [EstadoCita.completada],
      _ => const [],
    };

/// Estados del pedido de Lima (EstadoPedidoLima del backend), como texto.
class EstadoPedidoLima {
  const EstadoPedidoLima._();

  static const pendiente = 'Pendiente';
  static const confirmado = 'Confirmado';
  static const enPreparacion = 'EnPreparacion';
  static const enTransito = 'EnTransito';
  static const recibido = 'Recibido';
  static const entregado = 'Entregado';
  static const cancelado = 'Cancelado';
}

const nombresEstadoPedidoLima = <String, String>{
  EstadoPedidoLima.pendiente: 'Pendiente',
  EstadoPedidoLima.confirmado: 'Confirmado',
  EstadoPedidoLima.enPreparacion: 'En preparación',
  EstadoPedidoLima.enTransito: 'En camino',
  EstadoPedidoLima.recibido: 'Listo para recoger',
  EstadoPedidoLima.entregado: 'Entregado',
  EstadoPedidoLima.cancelado: 'Cancelado',
};

bool esPedidoFinal(String estado) => estado == EstadoPedidoLima.entregado || estado == EstadoPedidoLima.cancelado;
