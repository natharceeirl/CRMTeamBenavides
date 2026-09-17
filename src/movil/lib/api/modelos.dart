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
