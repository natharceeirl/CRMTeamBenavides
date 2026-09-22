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
