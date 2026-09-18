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

/// Entregada y Cancelada no admiten más cambios.
bool esEstadoTerminal(int estadoId) =>
    estadoId == EstadoOrden.entregada || estadoId == EstadoOrden.cancelada;

/// El backend rechaza el diagnóstico en órdenes entregadas o anuladas.
bool permiteDiagnostico(int estadoId) => !esEstadoTerminal(estadoId);

/// Las que siguen físicamente en el taller.
bool estaEnTaller(int estadoId) => !esEstadoTerminal(estadoId);
