// Contratos del backend: src/CRMTeamBenavides.Api/Features/**/*Contracts.cs

import '../formato.dart';
import 'estados.dart';

class Sesion {
  const Sesion({
    required this.accessToken,
    required this.accessTokenExpiration,
    required this.refreshToken,
    required this.refreshTokenExpiration,
  });

  factory Sesion.desdeJson(Map<String, dynamic> json) => Sesion(
        accessToken: json['accessToken'] as String,
        accessTokenExpiration:
            DateTime.parse(json['accessTokenExpiration'] as String),
        refreshToken: json['refreshToken'] as String,
        refreshTokenExpiration:
            DateTime.parse(json['refreshTokenExpiration'] as String),
      );

  final String accessToken;
  final DateTime accessTokenExpiration;
  final String refreshToken;
  final DateTime refreshTokenExpiration;

  Map<String, dynamic> aJson() => {
        'accessToken': accessToken,
        'accessTokenExpiration': accessTokenExpiration.toIso8601String(),
        'refreshToken': refreshToken,
        'refreshTokenExpiration': refreshTokenExpiration.toIso8601String(),
      };
}

class ClienteApi {
  const ClienteApi({
    required this.id,
    required this.nombreCompleto,
    required this.activo,
    this.razonSocial,
    this.documentoIdentidad,
    this.telefono,
    this.email,
    this.direccion,
    this.observaciones,
    this.tipoDocumento,
    this.numeroDocumento,
  });

  factory ClienteApi.desdeJson(Map<String, dynamic> json) => ClienteApi(
        id: json['id'] as String,
        nombreCompleto: json['nombreCompleto'] as String,
        razonSocial: json['razonSocial'] as String?,
        documentoIdentidad: json['documentoIdentidad'] as String?,
        telefono: json['telefono'] as String?,
        email: json['email'] as String?,
        direccion: json['direccion'] as String?,
        observaciones: json['observaciones'] as String?,
        activo: json['activo'] as bool,
        tipoDocumento: json['tipoDocumento'] as String?,
        numeroDocumento: json['numeroDocumento'] as String?,
      );

  final String id;
  final String nombreCompleto;
  final String? razonSocial;
  final String? documentoIdentidad;
  final String? telefono;
  final String? email;
  final String? direccion;
  final String? observaciones;
  final bool activo;
  final String? tipoDocumento;
  final String? numeroDocumento;

  /// «DNI 45879231». Un cliente anterior al tipo de documento muestra solo el número.
  String? get documento {
    final numero = numeroDocumento ?? documentoIdentidad;
    if (numero == null || numero.isEmpty) {
      return null;
    }
    return tipoDocumento == null ? numero : '$tipoDocumento $numero';
  }
}

class VehiculoApi {
  const VehiculoApi({
    required this.id,
    required this.clienteId,
    required this.clienteNombre,
    required this.placa,
    required this.marca,
    required this.modelo,
    required this.activo,
    this.anio,
    this.kilometraje,
    this.color,
    this.observaciones,
    this.tipoUnidad,
    this.numeroSerieVIN,
    this.numeroMotor,
    this.tipoMedidor,
    this.horasUso,
    this.tipoUnidadId,
    this.tipoMedidorId,
    this.lecturaMedidorActual,
  });

  factory VehiculoApi.desdeJson(Map<String, dynamic> json) => VehiculoApi(
        id: json['id'] as String,
        clienteId: json['clienteId'] as String,
        clienteNombre: json['clienteNombre'] as String,
        placa: json['placa'] as String? ?? '',
        marca: json['marca'] as String,
        modelo: json['modelo'] as String,
        anio: json['anio'] as int?,
        kilometraje: json['kilometraje'] as int?,
        color: json['color'] as String?,
        observaciones: json['observaciones'] as String?,
        activo: json['activo'] as bool,
        tipoUnidad: json['tipoUnidad'] as String?,
        numeroSerieVIN: json['numeroSerieVIN'] as String?,
        numeroMotor: json['numeroMotor'] as String?,
        tipoMedidor: json['tipoMedidor'] as String?,
        horasUso: (json['horasUso'] as num?)?.toDouble(),
        tipoUnidadId: json['tipoUnidadId'] as int?,
        tipoMedidorId: json['tipoMedidorId'] as int?,
        lecturaMedidorActual: (json['lecturaMedidorActual'] as num?)?.toDouble(),
      );

  final String id;
  final String clienteId;
  final String clienteNombre;

  /// Vacía si la unidad no tiene placa, como las motos acuáticas y los generadores.
  final String placa;
  final String marca;
  final String modelo;
  final int? anio;
  final int? kilometraje;
  final String? color;
  final String? observaciones;
  final bool activo;
  final String? tipoUnidad;
  final String? numeroSerieVIN;
  final String? numeroMotor;
  final String? tipoMedidor;
  final double? horasUso;
  final int? tipoUnidadId;
  final int? tipoMedidorId;
  final double? lecturaMedidorActual;

  String get descripcion {
    final anioTexto = anio == null ? '' : ' $anio';
    return '$marca $modelo$anioTexto';
  }

  String get tipoNombre => nombresTipoUnidad[tipoUnidadId] ?? 'Unidad';

  bool get midePorHoras => tipoMedidorId == medidorHoras;

  /// Placa si tiene; si no, VIN o serie.
  String get identificador {
    if (placa.isNotEmpty) {
      return 'Placa $placa';
    }
    final serie = numeroSerieVIN;
    return serie == null || serie.isEmpty ? 'Sin placa ni serie' : 'Serie $serie';
  }

  /// «12,450 km» o «86 h», según el medidor de la unidad.
  String get lectura {
    final valor = midePorHoras
        ? (lecturaMedidorActual ?? horasUso)
        : (lecturaMedidorActual ?? kilometraje?.toDouble());
    if (valor == null) {
      return '—';
    }
    return midePorHoras ? '${entero(valor)} h' : '${entero(valor)} km';
  }
}

/// Datos del usuario que salen de los claims del token. Los roles y permisos
/// llegan después, con GET /api/auth/me.
class UsuarioSesion {
  const UsuarioSesion({
    required this.id,
    required this.email,
    required this.nombre,
  });

  final String id;
  final String email;
  final String nombre;
}

/// GET /api/auth/me
class UsuarioActualApi {
  const UsuarioActualApi({
    required this.id,
    required this.email,
    required this.nombreCompleto,
    required this.activo,
    required this.roles,
    this.permisos = const [],
    this.clienteId,
  });

  factory UsuarioActualApi.desdeJson(Map<String, dynamic> json) => UsuarioActualApi(
        id: json['id'] as String,
        email: json['email'] as String? ?? '',
        nombreCompleto: json['nombreCompleto'] as String? ?? '',
        activo: json['activo'] as bool? ?? true,
        roles: (json['roles'] as List<dynamic>? ?? const [])
            .map((rol) => rol as String)
            .toList(),
        permisos: (json['permisos'] as List<dynamic>? ?? const [])
            .map((permiso) => permiso as String)
            .toList(),
        clienteId: json['clienteId'] as String?,
      );

  final String id;
  final String email;
  final String nombreCompleto;
  final bool activo;
  final List<String> roles;
  final List<String> permisos;
  final String? clienteId;
}

class OrdenServicioApi {
  const OrdenServicioApi({
    required this.id,
    required this.vehiculoPlaca,
    required this.vehiculoMarca,
    required this.vehiculoModelo,
    required this.clienteId,
    required this.clienteNombre,
    required this.estado,
    required this.estadoId,
    required this.fechaApertura,
    this.tecnicoNombre,
    this.diagnostico,
    this.observaciones,
    this.numeroOrden,
    this.tecnicoAsignadoId,
    this.fechaIngreso,
    this.fechaEstimadaEntrega,
    this.fechaSalida,
    this.fechaCierre,
    this.motivoFalla,
    this.solucion,
    this.tipoAtencionId,
    this.tipoFallaId,
    this.kilometrajeIngreso,
    this.horasUsoIngreso,
    this.lecturaMedidorIngreso,
    this.subtotalGravado,
    this.subtotalExonerado,
    this.subtotalInafecto,
    this.montoIgv,
    this.total,
    this.estadoPresupuestoId = EstadoPresupuesto.pendiente,
    this.fechaRespuestaCliente,
    this.observacionesPresupuesto,
    this.estadoGerenciaId = EstadoGerencia.noAplica,
    this.fechaAprobacionGerencia,
    this.usuarioAprobacionGerencia,
    this.observacionesGerencia,
    this.totalPagado = 0,
    this.saldo,
    this.estadoPago = EstadoPago.pendiente,
  });

  factory OrdenServicioApi.desdeJson(Map<String, dynamic> json) => OrdenServicioApi(
        id: json['id'] as String,
        vehiculoPlaca: json['vehiculoPlaca'] as String? ?? '',
        vehiculoMarca: json['vehiculoMarca'] as String? ?? '',
        vehiculoModelo: json['vehiculoModelo'] as String? ?? '',
        clienteId: json['clienteId'] as String,
        clienteNombre: json['clienteNombre'] as String? ?? '',
        estado: json['estado'] as String? ?? '',
        estadoId: json['estadoId'] as int,
        fechaApertura: DateTime.parse(json['fechaApertura'] as String),
        tecnicoNombre: json['tecnicoNombre'] as String?,
        diagnostico: json['diagnostico'] as String?,
        observaciones: json['observaciones'] as String?,
        numeroOrden: json['numeroOrden'] as String?,
        tecnicoAsignadoId: json['tecnicoAsignadoId'] as String?,
        fechaIngreso: _fecha(json['fechaIngreso']),
        fechaEstimadaEntrega: _fecha(json['fechaEstimadaEntrega']),
        fechaSalida: _fecha(json['fechaSalida']),
        fechaCierre: _fecha(json['fechaCierre']),
        motivoFalla: json['motivoFalla'] as String?,
        solucion: json['solucion'] as String?,
        tipoAtencionId: json['tipoAtencionId'] as int?,
        tipoFallaId: json['tipoFallaId'] as int?,
        kilometrajeIngreso: json['kilometrajeIngreso'] as int?,
        horasUsoIngreso: (json['horasUsoIngreso'] as num?)?.toDouble(),
        // En la API es decimal: una lectura en horas puede traer decimales.
        lecturaMedidorIngreso: (json['lecturaMedidorIngreso'] as num?)?.toDouble(),
        subtotalGravado: (json['subtotalGravado'] as num?)?.toDouble(),
        subtotalExonerado: (json['subtotalExonerado'] as num?)?.toDouble(),
        subtotalInafecto: (json['subtotalInafecto'] as num?)?.toDouble(),
        montoIgv: (json['montoIgv'] as num?)?.toDouble(),
        total: (json['total'] as num?)?.toDouble(),
        estadoPresupuestoId:
            json['estadoPresupuestoClienteId'] as int? ?? EstadoPresupuesto.pendiente,
        fechaRespuestaCliente: _fecha(json['fechaRespuestaCliente']),
        observacionesPresupuesto: json['observacionesPresupuestoCliente'] as String?,
        estadoGerenciaId: json['estadoAprobacionGerenciaId'] as int? ?? EstadoGerencia.noAplica,
        fechaAprobacionGerencia: _fecha(json['fechaAprobacionGerencia']),
        usuarioAprobacionGerencia: json['usuarioAprobacionGerenciaNombre'] as String?,
        observacionesGerencia: json['observacionesAprobacionGerencia'] as String?,
        totalPagado: (json['totalPagado'] as num? ?? 0).toDouble(),
        saldo: (json['saldo'] as num?)?.toDouble(),
        estadoPago: json['estadoPago'] as String? ?? EstadoPago.pendiente,
      );

  final String id;

  /// Vacía si la unidad no tiene placa.
  final String vehiculoPlaca;
  final String vehiculoMarca;
  final String vehiculoModelo;
  final String clienteId;
  final String clienteNombre;
  final String estado;
  final int estadoId;
  final DateTime fechaApertura;
  final String? tecnicoNombre;
  final String? diagnostico;
  final String? observaciones;
  final String? numeroOrden;
  final String? tecnicoAsignadoId;
  final DateTime? fechaIngreso;
  final DateTime? fechaEstimadaEntrega;
  final DateTime? fechaSalida;
  final DateTime? fechaCierre;
  final String? motivoFalla;
  final String? solucion;
  final int? tipoAtencionId;
  final int? tipoFallaId;
  final int? kilometrajeIngreso;
  final double? horasUsoIngreso;
  final double? lecturaMedidorIngreso;
  final double? subtotalGravado;
  final double? subtotalExonerado;
  final double? subtotalInafecto;
  final double? montoIgv;
  final double? total;
  final int estadoPresupuestoId;
  final DateTime? fechaRespuestaCliente;
  final String? observacionesPresupuesto;
  final int estadoGerenciaId;
  final DateTime? fechaAprobacionGerencia;
  final String? usuarioAprobacionGerencia;
  final String? observacionesGerencia;

  /// Adelantos y pagos, también los de la venta que liquidó la orden.
  final double totalPagado;

  /// Null en respuestas que no lo traen: se toma como el total.
  final double? saldo;
  final String estadoPago;

  String get unidad => '$vehiculoMarca $vehiculoModelo';

  /// «YZF-R3 · 4521-7B», o sin la placa si la unidad no tiene.
  String get unidadConPlaca =>
      vehiculoPlaca.isEmpty ? unidad : '$unidad · $vehiculoPlaca';

  String get referencia => numeroOrden ?? '#${id.substring(0, 8).toUpperCase()}';

  /// Lectura del medidor al ingresar, en km o en horas según la que tenga.
  String? get lecturaIngreso {
    if (kilometrajeIngreso != null) {
      return '${entero(kilometrajeIngreso!)} km';
    }
    if (horasUsoIngreso != null) {
      return '${entero(horasUsoIngreso!)} h';
    }
    // La lectura genérica no dice si es km u horas: se muestra el número solo.
    if (lecturaMedidorIngreso != null) {
      return entero(lecturaMedidorIngreso!);
    }
    return null;
  }
}

DateTime? _fecha(Object? valor) => valor is String ? DateTime.tryParse(valor) : null;

/// Un cambio de estado de la orden, con quién y cuándo.
class HistorialEstadoApi {
  const HistorialEstadoApi({
    required this.id,
    required this.estadoNuevoId,
    required this.fechaCambio,
    this.estadoAnteriorId,
    this.usuarioNombre,
    this.observaciones,
  });

  factory HistorialEstadoApi.desdeJson(Map<String, dynamic> json) => HistorialEstadoApi(
        id: json['id'] as String,
        estadoNuevoId: json['estadoNuevoId'] as int? ?? 0,
        estadoAnteriorId: json['estadoAnteriorId'] as int?,
        fechaCambio: DateTime.parse(json['fechaCambio'] as String),
        usuarioNombre: json['usuarioNombre'] as String?,
        observaciones: json['observaciones'] as String?,
      );

  final String id;
  final int estadoNuevoId;
  final int? estadoAnteriorId;

  /// Las aprobaciones y la asignación de técnico se anotan sin cambiar el estado.
  bool get cambiaEstado => estadoAnteriorId != estadoNuevoId;
  final DateTime fechaCambio;
  final String? usuarioNombre;
  final String? observaciones;
}

class DetalleServicioApi {
  const DetalleServicioApi({
    required this.id,
    required this.descripcion,
    required this.cantidad,
    required this.precioUnitario,
    required this.subtotal,
    required this.esRepuesto,
    this.productoCodigo,
    this.tipoItem,
    this.tipoItemNombre,
    this.tipoAfectacionIgv,
    this.tipoAfectacionIgvNombre,
    this.subtotalGravado,
    this.montoIgv,
    this.total,
  });

  factory DetalleServicioApi.desdeJson(Map<String, dynamic> json) => DetalleServicioApi(
        id: json['id'] as String,
        descripcion: json['descripcion'] as String? ?? '',
        cantidad: json['cantidad'] as int? ?? 0,
        precioUnitario: (json['precioUnitario'] as num? ?? 0).toDouble(),
        subtotal: (json['subtotal'] as num? ?? 0).toDouble(),
        esRepuesto: json['esRepuesto'] as bool? ?? false,
        productoCodigo: json['productoCodigo'] as String?,
        tipoItem: json['tipoItem'] as int?,
        tipoItemNombre: json['tipoItemNombre'] as String?,
        tipoAfectacionIgv: json['tipoAfectacionIgv'] as int?,
        tipoAfectacionIgvNombre: json['tipoAfectacionIgvNombre'] as String?,
        subtotalGravado: (json['subtotalGravado'] as num?)?.toDouble(),
        montoIgv: (json['montoIgv'] as num?)?.toDouble(),
        total: (json['total'] as num?)?.toDouble(),
      );

  final String id;
  final String descripcion;
  final int cantidad;
  final double precioUnitario;
  final double subtotal;
  final bool esRepuesto;
  final String? productoCodigo;
  final int? tipoItem;
  final String? tipoItemNombre;
  final int? tipoAfectacionIgv;
  final String? tipoAfectacionIgvNombre;
  final double? subtotalGravado;
  final double? montoIgv;
  final double? total;
}

class OrdenServicioDetalleApi {
  const OrdenServicioDetalleApi({
    required this.orden,
    required this.detalles,
    required this.total,
    this.clienteTelefono,
    this.clienteDocumento,
    this.vehiculoKilometraje,
    this.tipoUnidad,
    this.numeroSerieVIN,
    this.numeroMotor,
    this.historial = const [],
    this.lecturaMedidorIngreso,
    this.subtotalGravado,
    this.subtotalExonerado,
    this.subtotalInafecto,
    this.montoIgv,
    this.porcentajeIgv,
  });

  factory OrdenServicioDetalleApi.desdeJson(Map<String, dynamic> json) {
    final historial = (json['historial'] as List<dynamic>? ?? const [])
        .map((cambio) => HistorialEstadoApi.desdeJson(cambio as Map<String, dynamic>))
        .toList()
      ..sort((a, b) => a.fechaCambio.compareTo(b.fechaCambio));

    return OrdenServicioDetalleApi(
      orden: OrdenServicioApi.desdeJson(json),
      detalles: (json['detalles'] as List<dynamic>? ?? const [])
          .map((detalle) => DetalleServicioApi.desdeJson(detalle as Map<String, dynamic>))
          .toList(),
      total: (json['total'] as num? ?? 0).toDouble(),
      clienteTelefono: json['clienteTelefono'] as String?,
      clienteDocumento: json['clienteDocumentoIdentidad'] as String?,
      vehiculoKilometraje: json['vehiculoKilometraje'] as int?,
      tipoUnidad: json['tipoUnidad'] as String?,
      numeroSerieVIN: json['numeroSerieVIN'] as String?,
      numeroMotor: json['numeroMotor'] as String?,
      historial: historial,
      // En la API es decimal: una lectura en horas puede traer decimales.
      lecturaMedidorIngreso: (json['lecturaMedidorIngreso'] as num?)?.toDouble(),
      subtotalGravado: (json['subtotalGravado'] as num?)?.toDouble(),
      subtotalExonerado: (json['subtotalExonerado'] as num?)?.toDouble(),
      subtotalInafecto: (json['subtotalInafecto'] as num?)?.toDouble(),
      montoIgv: (json['montoIgv'] as num?)?.toDouble(),
      porcentajeIgv: (json['porcentajeIgv'] as num?)?.toDouble(),
    );
  }

  final OrdenServicioApi orden;
  final List<DetalleServicioApi> detalles;
  final double total;
  final String? clienteTelefono;
  final String? clienteDocumento;
  final int? vehiculoKilometraje;
  final String? tipoUnidad;
  final String? numeroSerieVIN;
  final String? numeroMotor;

  /// Del más antiguo al más reciente.
  final List<HistorialEstadoApi> historial;
  final double? lecturaMedidorIngreso;
  final double? subtotalGravado;
  final double? subtotalExonerado;
  final double? subtotalInafecto;
  final double? montoIgv;
  final double? porcentajeIgv;
}


/// GET /api/dashboard/resumen
class ResumenDashboardApi {
  const ResumenDashboardApi({
    required this.enTaller,
    required this.listas,
    required this.entregadas,
    required this.totalOrdenes,
    required this.ventasConfirmadas,
    required this.montoVentas,
    required this.productosStockBajo,
    required this.clientesActivos,
  });

  factory ResumenDashboardApi.desdeJson(Map<String, dynamic> json) {
    final ordenes = json['ordenesServicio'] as Map<String, dynamic>? ?? const {};
    final ventas = json['ventas'] as Map<String, dynamic>? ?? const {};
    final inventario = json['inventario'] as Map<String, dynamic>? ?? const {};
    final crm = json['crmActivos'] as Map<String, dynamic>? ?? const {};

    int entero(Map<String, dynamic> mapa, String clave) => (mapa[clave] as num? ?? 0).toInt();

    // «En taller» es todo lo que no está entregado ni anulado.
    final enTaller = entero(ordenes, 'abierta') +
        entero(ordenes, 'diagnostico') +
        entero(ordenes, 'aprobada') +
        entero(ordenes, 'enProceso') +
        entero(ordenes, 'lista');

    return ResumenDashboardApi(
      enTaller: enTaller,
      listas: entero(ordenes, 'lista'),
      entregadas: entero(ordenes, 'entregada'),
      totalOrdenes: entero(ordenes, 'total'),
      ventasConfirmadas: entero(ventas, 'confirmadas'),
      montoVentas: (ventas['montoConfirmadas'] as num? ?? 0).toDouble(),
      productosStockBajo: entero(inventario, 'productosConStockBajo'),
      clientesActivos: entero(crm, 'clientesActivos'),
    );
  }

  final int enTaller;
  final int listas;
  final int entregadas;
  final int totalOrdenes;
  final int ventasConfirmadas;
  final double montoVentas;
  final int productosStockBajo;
  final int clientesActivos;
}

/// GET /api/chatbot/faqs
class FaqApi {
  const FaqApi({
    required this.id,
    required this.categoria,
    required this.pregunta,
    required this.respuesta,
  });

  factory FaqApi.desdeJson(Map<String, dynamic> json) => FaqApi(
        id: json['id'] as String,
        categoria: json['categoria'] as String? ?? '',
        pregunta: json['pregunta'] as String? ?? '',
        respuesta: json['respuesta'] as String? ?? '',
      );

  final String id;
  final String categoria;
  final String pregunta;
  final String respuesta;
}

/// POST /api/chatbot/consultar
class RespuestaChatbotApi {
  const RespuestaChatbotApi({
    required this.consultaId,
    required this.mensajeRespuesta,
    required this.resueltoPorFaq,
    required this.requiereAgente,
    required this.sugerencias,
  });

  factory RespuestaChatbotApi.desdeJson(Map<String, dynamic> json) => RespuestaChatbotApi(
        consultaId: json['consultaId'] as String?,
        mensajeRespuesta: json['mensajeRespuesta'] as String? ?? '',
        resueltoPorFaq: json['resueltoPorFaq'] as bool? ?? false,
        requiereAgente: json['requiereAgente'] as bool? ?? false,
        sugerencias: (json['sugerencias'] as List<dynamic>? ?? const [])
            .map((faq) => FaqApi.desdeJson(faq as Map<String, dynamic>))
            .toList(),
      );

  /// Solo si respondió una pregunta frecuente: sin respuesta no se registra
  /// consulta, y al pedir un asesor el backend crea una nueva.
  final String? consultaId;
  final String mensajeRespuesta;
  final bool resueltoPorFaq;
  final bool requiereAgente;
  final List<FaqApi> sugerencias;
}

/// GET /api/servicios: catálogo de servicios del taller con su precio.
class ServicioApi {
  const ServicioApi({
    required this.id,
    required this.nombre,
    required this.precioSugerido,
    required this.tipoAfectacionIgv,
  });

  factory ServicioApi.desdeJson(Map<String, dynamic> json) => ServicioApi(
        id: json['id'] as String,
        nombre: json['nombre'] as String? ?? '',
        precioSugerido: (json['precioSugerido'] as num? ?? 0).toDouble(),
        tipoAfectacionIgv: json['tipoAfectacionIgv'] as int? ?? 0,
      );

  final String id;
  final String nombre;
  final double precioSugerido;
  final int tipoAfectacionIgv;
}

class CategoriaProductoApi {
  const CategoriaProductoApi({
    required this.id,
    required this.nombre,
    required this.cantidadProductos,
    required this.activo,
  });

  factory CategoriaProductoApi.desdeJson(Map<String, dynamic> json) =>
      CategoriaProductoApi(
        id: json['id'] as String,
        nombre: json['nombre'] as String? ?? '',
        cantidadProductos: json['cantidadProductos'] as int? ?? 0,
        activo: json['activo'] as bool? ?? true,
      );

  final String id;
  final String nombre;
  final int cantidadProductos;
  final bool activo;
}

class ProductoApi {
  const ProductoApi({
    required this.id,
    required this.codigo,
    required this.nombre,
    required this.unidad,
    required this.precioVenta,
    required this.stockActual,
    required this.stockMinimo,
    required this.esBajoStock,
    required this.categoriaId,
    required this.categoriaNombre,
    required this.activo,
    this.descripcion,
  });

  factory ProductoApi.desdeJson(Map<String, dynamic> json) => ProductoApi(
        id: json['id'] as String,
        codigo: json['codigo'] as String? ?? '',
        nombre: json['nombre'] as String? ?? '',
        descripcion: json['descripcion'] as String?,
        unidad: json['unidad'] as String? ?? '',
        precioVenta: (json['precioVenta'] as num? ?? 0).toDouble(),
        stockActual: json['stockActual'] as int? ?? 0,
        // El mínimo que aplica: el propio, el de la categoría o 4 por defecto.
        stockMinimo: json['stockMinimoEfectivo'] as int? ?? json['stockMinimo'] as int? ?? 4,
        esBajoStock: json['esBajoStock'] as bool? ?? false,
        categoriaId: json['categoriaId'] as String? ?? '',
        categoriaNombre: json['categoriaNombre'] as String? ?? '',
        activo: json['activo'] as bool? ?? true,
      );

  final String id;
  final String codigo;
  final String nombre;
  final String? descripcion;
  final String unidad;
  final double precioVenta;
  final int stockActual;
  final int stockMinimo;

  /// Lo calcula el backend; la app solo lo pinta, no lo recalcula.
  final bool esBajoStock;
  final String categoriaId;
  final String categoriaNombre;
  final bool activo;

  bool get agotado => stockActual <= 0;
}

class MovimientoInventarioApi {
  const MovimientoInventarioApi({
    required this.id,
    required this.productoId,
    required this.productoCodigo,
    required this.productoNombre,
    required this.tipo,
    required this.tipoId,
    required this.cantidad,
    required this.fechaCreacion,
    this.motivo,
  });

  factory MovimientoInventarioApi.desdeJson(Map<String, dynamic> json) =>
      MovimientoInventarioApi(
        id: json['id'] as String,
        productoId: json['productoId'] as String? ?? '',
        productoCodigo: json['productoCodigo'] as String? ?? '',
        productoNombre: json['productoNombre'] as String? ?? '',
        tipo: json['tipo'] as String? ?? '',
        tipoId: json['tipoId'] as int? ?? 0,
        cantidad: json['cantidad'] as int? ?? 0,
        motivo: json['motivo'] as String?,
        fechaCreacion: DateTime.parse(json['fechaCreacion'] as String),
      );

  final String id;
  final String productoId;
  final String productoCodigo;
  final String productoNombre;
  final String tipo;
  final int tipoId;
  final int cantidad;
  final String? motivo;
  final DateTime fechaCreacion;
}

class VentaApi {
  const VentaApi({
    required this.id,
    required this.clienteId,
    required this.clienteNombre,
    required this.estado,
    required this.estadoId,
    required this.fecha,
    required this.total,
    required this.cantidadItems,
    required this.activo,
    this.ordenServicioId,
    this.subtotalGravado = 0,
    this.montoIgv = 0,
    this.totalPagado = 0,
    this.saldo = 0,
    this.estadoPago = EstadoPago.pendiente,
  });

  factory VentaApi.desdeJson(Map<String, dynamic> json) => VentaApi(
        id: json['id'] as String,
        clienteId: json['clienteId'] as String? ?? '',
        clienteNombre: json['clienteNombre'] as String? ?? '',
        ordenServicioId: json['ordenServicioId'] as String?,
        estado: json['estado'] as String? ?? '',
        estadoId: json['estadoId'] as int? ?? 0,
        fecha: DateTime.parse(json['fecha'] as String),
        total: (json['total'] as num? ?? 0).toDouble(),
        cantidadItems: json['cantidadItems'] as int? ?? 0,
        activo: json['activo'] as bool? ?? true,
        subtotalGravado: (json['subtotalGravado'] as num? ?? 0).toDouble(),
        montoIgv: (json['montoIgv'] as num? ?? 0).toDouble(),
        totalPagado: (json['totalPagado'] as num? ?? 0).toDouble(),
        saldo: (json['saldo'] as num? ?? 0).toDouble(),
        estadoPago: json['estadoPago'] as String? ?? EstadoPago.pendiente,
      );

  final String id;
  final String clienteId;
  final String clienteNombre;
  final String? ordenServicioId;
  final String estado;
  final int estadoId;
  final DateTime fecha;
  final double total;
  final int cantidadItems;
  final bool activo;
  final double subtotalGravado;
  final double montoIgv;

  /// Lo pagado y el saldo los calcula el backend, con los adelantos de la
  /// orden si la venta la liquidó.
  final double totalPagado;
  final double saldo;

  /// Pendiente, Parcial o Pagado.
  final String estadoPago;

  /// La API no da correlativo todavía: se usa el inicio del id, como en órdenes.
  String get referencia => '#${id.substring(0, 8).toUpperCase()}';

  bool get vieneDeOrden => ordenServicioId != null;
}

class DetalleVentaApi {
  const DetalleVentaApi({
    required this.id,
    required this.productoCodigo,
    required this.productoNombre,
    required this.cantidad,
    required this.precioUnitario,
    required this.subtotal,
    this.total,
  });

  factory DetalleVentaApi.desdeJson(Map<String, dynamic> json) => DetalleVentaApi(
        id: json['id'] as String,
        // Los servicios y la mano de obra de una orden no tienen código.
        productoCodigo: json['productoCodigo'] as String? ?? '',
        productoNombre: json['productoNombre'] as String? ?? '',
        cantidad: json['cantidad'] as int? ?? 0,
        precioUnitario: (json['precioUnitario'] as num? ?? 0).toDouble(),
        subtotal: (json['subtotal'] as num? ?? 0).toDouble(),
        total: (json['total'] as num?)?.toDouble(),
      );

  final String id;
  final String productoCodigo;
  final String productoNombre;
  final int cantidad;
  final double precioUnitario;
  final double subtotal;

  /// Con IGV. Las ventas anteriores al IGV no lo traen.
  final double? total;

  double get importe => total ?? subtotal;
}

/// Estado de pago que calcula el backend a partir de lo pagado y el saldo.
class EstadoPago {
  const EstadoPago._();

  static const pendiente = 'Pendiente';
  static const parcial = 'Parcial';
  static const pagado = 'Pagado';
}

class PagoApi {
  const PagoApi({
    required this.id,
    required this.monto,
    required this.metodoPagoNombre,
    required this.fecha,
    required this.esAnticipo,
    this.referencia,
  });

  factory PagoApi.desdeJson(Map<String, dynamic> json) => PagoApi(
        id: json['id'] as String,
        monto: (json['monto'] as num? ?? 0).toDouble(),
        metodoPagoNombre: json['metodoPagoNombre'] as String? ?? '',
        fecha: DateTime.parse(json['fecha'] as String),
        esAnticipo: json['esAnticipo'] as bool? ?? false,
        referencia: json['referencia'] as String?,
      );

  final String id;
  final double monto;
  final String metodoPagoNombre;
  final DateTime fecha;
  final bool esAnticipo;
  final String? referencia;
}

class ComprobanteApi {
  const ComprobanteApi({
    required this.id,
    required this.tipo,
    required this.estado,
    this.serie,
    this.numero,
  });

  factory ComprobanteApi.desdeJson(Map<String, dynamic> json) => ComprobanteApi(
        id: json['id'] as String,
        tipo: json['tipo'] as String? ?? '',
        serie: json['serie'] as String?,
        numero: json['numero'] as String?,
        estado: json['estado'] as String? ?? '',
      );

  final String id;
  final String tipo;
  final String? serie;
  final String? numero;
  final String estado;

  /// «F001-000123», o solo el tipo si todavía no tiene serie ni número.
  String get referencia {
    final partes = [serie, numero].whereType<String>().where((p) => p.isNotEmpty);
    return partes.isEmpty ? tipo : '$tipo ${partes.join('-')}';
  }
}

class VentaDetalleApi {
  const VentaDetalleApi({
    required this.venta,
    required this.detalles,
    this.clienteDocumento,
    this.clienteTelefono,
    this.comprobante,
    this.pagos = const [],
  });

  factory VentaDetalleApi.desdeJson(Map<String, dynamic> json) {
    final comprobante = json['comprobante'] as Map<String, dynamic>?;

    return VentaDetalleApi(
      venta: VentaApi.desdeJson({
        ...json,
        // El detalle no trae cantidadItems: se cuenta de las líneas.
        'cantidadItems': (json['detalles'] as List<dynamic>? ?? const []).length,
      }),
      clienteDocumento: json['clienteDocumento'] as String?,
      clienteTelefono: json['clienteTelefono'] as String?,
      detalles: (json['detalles'] as List<dynamic>? ?? const [])
          .map((detalle) => DetalleVentaApi.desdeJson(detalle as Map<String, dynamic>))
          .toList(),
      comprobante:
          comprobante == null ? null : ComprobanteApi.desdeJson(comprobante),
      pagos: (json['pagos'] as List<dynamic>? ?? const [])
          .map((pago) => PagoApi.desdeJson(pago as Map<String, dynamic>))
          .toList(),
    );
  }

  final VentaApi venta;
  final String? clienteDocumento;
  final String? clienteTelefono;
  final List<DetalleVentaApi> detalles;
  final ComprobanteApi? comprobante;
  final List<PagoApi> pagos;
}

/// GET /api/caja-chica/actual: si hay una caja abierta y su saldo.
class CajaActualApi {
  const CajaActualApi({required this.abierta, this.saldo, this.fechaApertura});

  factory CajaActualApi.desdeJson(Map<String, dynamic> json) {
    final caja = json['caja'] as Map<String, dynamic>?;
    return CajaActualApi(
      abierta: (json['tieneCajaAbierta'] as bool? ?? false) && caja != null,
      saldo: (caja?['saldoCalculado'] as num?)?.toDouble(),
      fechaApertura: caja == null ? null : DateTime.parse(caja['fechaApertura'] as String),
    );
  }

  final bool abierta;
  final double? saldo;
  final DateTime? fechaApertura;
}

/// GET /api/portal/resumen: lo que el cliente ve al entrar.
class PortalResumenApi {
  const PortalResumenApi({
    required this.clienteNombre,
    required this.unidades,
    required this.ordenesActivas,
    required this.presupuestosPendientes,
    required this.saldoPendiente,
  });

  factory PortalResumenApi.desdeJson(Map<String, dynamic> json) => PortalResumenApi(
        clienteNombre: json['clienteNombre'] as String? ?? '',
        unidades: json['cantidadUnidades'] as int? ?? 0,
        ordenesActivas: json['cantidadOrdenesActivas'] as int? ?? 0,
        presupuestosPendientes: json['cantidadPresupuestosPendientes'] as int? ?? 0,
        saldoPendiente: (json['saldoPendienteTotal'] as num? ?? 0).toDouble(),
      );

  final String clienteNombre;
  final int unidades;
  final int ordenesActivas;
  final int presupuestosPendientes;
  final double saldoPendiente;
}

/// GET /api/portal/comprobantes: los comprobantes del cliente.
class ComprobantePortalApi {
  const ComprobantePortalApi({
    required this.id,
    required this.tipo,
    required this.fecha,
    required this.total,
    required this.estado,
    this.serie,
    this.numero,
    this.numeroOrden,
    this.metodoPago,
  });

  factory ComprobantePortalApi.desdeJson(Map<String, dynamic> json) => ComprobantePortalApi(
        id: json['id'] as String,
        tipo: json['tipo'] as String? ?? '',
        serie: json['serie'] as String?,
        numero: json['numero'] as String?,
        fecha: DateTime.parse(json['fecha'] as String),
        total: (json['total'] as num? ?? 0).toDouble(),
        estado: json['estado'] as String? ?? '',
        numeroOrden: json['numeroOrden'] as String?,
        metodoPago: json['metodoPagoPrincipal'] as String?,
      );

  final String id;
  final String tipo;
  final String? serie;
  final String? numero;
  final DateTime fecha;
  final double total;
  final String estado;
  final String? numeroOrden;
  final String? metodoPago;

  /// «Boleta B001-000001», o solo el tipo si no tiene serie ni número.
  String get referencia {
    final partes = [serie, numero].whereType<String>().where((p) => p.isNotEmpty);
    return partes.isEmpty ? tipo : '$tipo ${partes.join('-')}';
  }

  bool get anulado => estado == 'Anulado';
}

/// GET /api/vehiculos/{id}/historial-servicio: una atención pasada de la unidad.
class AtencionServicioApi {
  const AtencionServicioApi({
    required this.ordenId,
    required this.fechaIngreso,
    required this.estadoId,
    required this.total,
    required this.trabajos,
    this.numeroOrden,
    this.motivoFalla,
  });

  factory AtencionServicioApi.desdeJson(Map<String, dynamic> json) => AtencionServicioApi(
        ordenId: json['ordenServicioId'] as String,
        numeroOrden: json['numeroOrden'] as String?,
        fechaIngreso: DateTime.parse(json['fechaIngreso'] as String),
        estadoId: json['estadoId'] as int? ?? 0,
        motivoFalla: json['motivoFalla'] as String?,
        total: (json['total'] as num? ?? 0).toDouble(),
        trabajos: (json['items'] as List<dynamic>? ?? const [])
            .map((item) => (item as Map<String, dynamic>)['descripcion'] as String? ?? '')
            .where((descripcion) => descripcion.isNotEmpty)
            .toList(),
      );

  final String ordenId;
  final String? numeroOrden;
  final DateTime fechaIngreso;
  final int estadoId;
  final String? motivoFalla;
  final double total;
  final List<String> trabajos;
}
