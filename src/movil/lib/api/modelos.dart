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
