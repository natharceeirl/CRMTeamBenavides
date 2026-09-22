// Contratos del backend: src/CRMTeamBenavides.Api/Features/**/*Contracts.cs

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
  });

  factory VehiculoApi.desdeJson(Map<String, dynamic> json) => VehiculoApi(
        id: json['id'] as String,
        clienteId: json['clienteId'] as String,
        clienteNombre: json['clienteNombre'] as String,
        placa: json['placa'] as String,
        marca: json['marca'] as String,
        modelo: json['modelo'] as String,
        anio: json['anio'] as int?,
        kilometraje: json['kilometraje'] as int?,
        color: json['color'] as String?,
        observaciones: json['observaciones'] as String?,
        activo: json['activo'] as bool,
      );

  final String id;
  final String clienteId;
  final String clienteNombre;
  final String placa;
  final String marca;
  final String modelo;
  final int? anio;
  final int? kilometraje;
  final String? color;
  final String? observaciones;
  final bool activo;

  String get descripcion {
    final anioTexto = anio == null ? '' : ' $anio';
    return '$marca $modelo$anioTexto';
  }
}

/// Datos del usuario que hoy salen de los claims del token, porque el backend
/// todavía no expone GET /api/auth/me ni manda los roles.
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
  });

  factory UsuarioActualApi.desdeJson(Map<String, dynamic> json) => UsuarioActualApi(
        id: json['id'] as String,
        email: json['email'] as String? ?? '',
        nombreCompleto: json['nombreCompleto'] as String? ?? '',
        activo: json['activo'] as bool? ?? true,
        roles: (json['roles'] as List<dynamic>? ?? const [])
            .map((rol) => rol as String)
            .toList(),
      );

  final String id;
  final String email;
  final String nombreCompleto;
  final bool activo;
  final List<String> roles;
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
      );

  final String id;
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

  String get unidad => '$vehiculoMarca $vehiculoModelo';

  /// La API no da correlativo todavía: se usa el inicio del id.
  String get referencia => '#${id.substring(0, 8).toUpperCase()}';
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
  });

  factory DetalleServicioApi.desdeJson(Map<String, dynamic> json) => DetalleServicioApi(
        id: json['id'] as String,
        descripcion: json['descripcion'] as String? ?? '',
        cantidad: json['cantidad'] as int? ?? 0,
        precioUnitario: (json['precioUnitario'] as num? ?? 0).toDouble(),
        subtotal: (json['subtotal'] as num? ?? 0).toDouble(),
        esRepuesto: json['esRepuesto'] as bool? ?? false,
        productoCodigo: json['productoCodigo'] as String?,
      );

  final String id;
  final String descripcion;
  final int cantidad;
  final double precioUnitario;
  final double subtotal;
  final bool esRepuesto;
  final String? productoCodigo;
}

class OrdenServicioDetalleApi {
  const OrdenServicioDetalleApi({
    required this.orden,
    required this.detalles,
    required this.total,
    this.clienteTelefono,
    this.vehiculoKilometraje,
  });

  factory OrdenServicioDetalleApi.desdeJson(Map<String, dynamic> json) =>
      OrdenServicioDetalleApi(
        orden: OrdenServicioApi.desdeJson(json),
        detalles: (json['detalles'] as List<dynamic>? ?? const [])
            .map((detalle) => DetalleServicioApi.desdeJson(detalle as Map<String, dynamic>))
            .toList(),
        total: (json['total'] as num? ?? 0).toDouble(),
        clienteTelefono: json['clienteTelefono'] as String?,
        vehiculoKilometraje: json['vehiculoKilometraje'] as int?,
      );

  final OrdenServicioApi orden;
  final List<DetalleServicioApi> detalles;
  final double total;
  final String? clienteTelefono;
  final int? vehiculoKilometraje;
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
        consultaId: json['consultaId'] as String? ?? '',
        mensajeRespuesta: json['mensajeRespuesta'] as String? ?? '',
        resueltoPorFaq: json['resueltoPorFaq'] as bool? ?? false,
        requiereAgente: json['requiereAgente'] as bool? ?? false,
        sugerencias: (json['sugerencias'] as List<dynamic>? ?? const [])
            .map((faq) => FaqApi.desdeJson(faq as Map<String, dynamic>))
            .toList(),
      );

  final String consultaId;
  final String mensajeRespuesta;
  final bool resueltoPorFaq;
  final bool requiereAgente;
  final List<FaqApi> sugerencias;
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
        stockMinimo: json['stockMinimo'] as int? ?? 0,
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
  });

  factory DetalleVentaApi.desdeJson(Map<String, dynamic> json) => DetalleVentaApi(
        id: json['id'] as String,
        productoCodigo: json['productoCodigo'] as String? ?? '',
        productoNombre: json['productoNombre'] as String? ?? '',
        cantidad: json['cantidad'] as int? ?? 0,
        precioUnitario: (json['precioUnitario'] as num? ?? 0).toDouble(),
        subtotal: (json['subtotal'] as num? ?? 0).toDouble(),
      );

  final String id;
  final String productoCodigo;
  final String productoNombre;
  final int cantidad;
  final double precioUnitario;
  final double subtotal;
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
    );
  }

  final VentaApi venta;
  final String? clienteDocumento;
  final String? clienteTelefono;
  final List<DetalleVentaApi> detalles;
  final ComprobanteApi? comprobante;
}
