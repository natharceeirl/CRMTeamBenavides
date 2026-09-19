import 'package:dio/dio.dart';

import 'almacen_sesion.dart';
import 'config.dart';
import 'modelos.dart';

class ErrorApi implements Exception {
  ErrorApi(this.mensaje, [this.estado]);

  final String mensaje;
  final int? estado;

  @override
  String toString() => mensaje;
}

/// Cliente HTTP de la app: pone el token en cada llamada, lo renueva cuando
/// vence y avisa a la app cuando la sesión ya no se puede recuperar.
class ApiHttp {
  ApiHttp(this._almacen, {Dio? dio, Dio? dioSinToken})
      : _dio = dio ?? Dio(BaseOptions(baseUrl: urlBaseApi())),
        _dioSinToken = dioSinToken ?? Dio(BaseOptions(baseUrl: urlBaseApi())) {
    _dio.interceptors.add(
      InterceptorsWrapper(onRequest: _alEnviar, onError: _alFallar),
    );
  }

  final AlmacenSesion _almacen;
  final Dio _dio;

  /// Sin interceptor: lo usan el login y el refresh para no entrar en bucle.
  final Dio _dioSinToken;

  Sesion? _sesion;
  Future<Sesion?>? _renovacionEnCurso;

  /// La app escucha esto para mandar al login cuando ya no hay forma de renovar.
  void Function()? alExpirarSesion;

  Sesion? get sesion => _sesion;

  Future<Sesion?> recuperarSesionGuardada() async {
    _sesion = await _almacen.leer();
    return _sesion;
  }

  Future<Sesion> iniciarSesion(String email, String password) async {
    try {
      final respuesta = await _dioSinToken.post<Map<String, dynamic>>(
        '/api/auth/login',
        data: {'email': email, 'password': password},
      );

      final sesion = Sesion.desdeJson(respuesta.data!);
      await _guardar(sesion);
      return sesion;
    } on DioException catch (fallo) {
      if (fallo.response?.statusCode == 401) {
        throw ErrorApi('Correo o contraseña incorrectos.', 401);
      }
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  /// El backend no tiene logout: se descarta la sesión local.
  Future<void> cerrarSesion() async {
    _sesion = null;
    await _almacen.borrar();
  }

  Future<List<ClienteApi>> clientes() async {
    final datos = await _lista('/api/clientes');
    return datos.map(ClienteApi.desdeJson).toList();
  }

  Future<ClienteApi> cliente(String id) async {
    final datos = await _pedir<Map<String, dynamic>>('/api/clientes/$id');
    return ClienteApi.desdeJson(datos);
  }

  Future<List<VehiculoApi>> vehiculos({String? clienteId}) async {
    final ruta = clienteId == null
        ? '/api/vehiculos'
        : '/api/vehiculos?clienteId=$clienteId';
    final datos = await _lista(ruta);
    return datos.map(VehiculoApi.desdeJson).toList();
  }

  Future<UsuarioActualApi> usuarioActual() async {
    final datos = await _pedir<Map<String, dynamic>>('/api/auth/me');
    return UsuarioActualApi.desdeJson(datos);
  }

  Future<List<OrdenServicioApi>> ordenes({int? estado}) async {
    final ruta = estado == null
        ? '/api/ordenes-servicio'
        : '/api/ordenes-servicio?estado=$estado';
    final datos = await _lista(ruta);
    return datos.map(OrdenServicioApi.desdeJson).toList();
  }

  Future<OrdenServicioDetalleApi> orden(String id) async {
    final datos = await _pedir<Map<String, dynamic>>('/api/ordenes-servicio/$id');
    return OrdenServicioDetalleApi.desdeJson(datos);
  }

  /// El backend pasa la orden a Diagnóstico si estaba Abierta.
  Future<OrdenServicioApi> registrarDiagnostico(String id, String diagnostico) async {
    try {
      final respuesta = await _dio.put<Map<String, dynamic>>(
        '/api/ordenes-servicio/$id/diagnostico',
        data: {'diagnostico': diagnostico},
      );
      return OrdenServicioApi.desdeJson(respuesta.data!);
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<ResumenDashboardApi> resumenDashboard() async {
    final datos = await _pedir<Map<String, dynamic>>('/api/dashboard/resumen');
    return ResumenDashboardApi.desdeJson(datos);
  }

  /// Las FAQs son públicas: no hacen falta credenciales.
  Future<List<FaqApi>> faqs() async {
    final datos = await _lista('/api/chatbot/faqs');
    return datos.map(FaqApi.desdeJson).toList();
  }

  Future<RespuestaChatbotApi> consultarChatbot(String mensaje) async {
    try {
      final respuesta = await _dio.post<Map<String, dynamic>>(
        '/api/chatbot/consultar',
        data: {'mensaje': mensaje, 'canal': 'app'},
      );
      return RespuestaChatbotApi.desdeJson(respuesta.data!);
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<String> solicitarAgente({
    required String telefono,
    required String motivo,
    String? consultaId,
    String? nombre,
  }) async {
    try {
      final respuesta = await _dio.post<Map<String, dynamic>>(
        '/api/chatbot/solicitar-agente',
        data: {
          'consultaId': consultaId,
          'nombreContacto': nombre,
          'telefonoContacto': telefono,
          'motivo': motivo,
          'canal': 'app',
        },
      );
      return respuesta.data?['mensaje'] as String? ?? 'Solicitud registrada.';
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<List<Map<String, dynamic>>> _lista(String ruta) async {
    final datos = await _pedir<List<dynamic>>(ruta);
    return datos.cast<Map<String, dynamic>>();
  }

  Future<T> _pedir<T>(String ruta) async {
    try {
      final respuesta = await _dio.get<T>(ruta);
      return respuesta.data as T;
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<void> _guardar(Sesion sesion) async {
    _sesion = sesion;
    await _almacen.guardar(sesion);
  }

  Future<void> _alEnviar(
    RequestOptions opciones,
    RequestInterceptorHandler siguiente,
  ) async {
    final actual = _sesion;
    if (actual != null) {
      // Medio minuto de margen para no mandar un token que vence en el camino.
      final limite = actual.accessTokenExpiration.subtract(
        const Duration(seconds: 30),
      );
      if (limite.isBefore(DateTime.now().toUtc())) {
        await _renovar();
      }

      final vigente = _sesion;
      if (vigente != null) {
        opciones.headers['Authorization'] = 'Bearer ${vigente.accessToken}';
      }
    }

    siguiente.next(opciones);
  }

  Future<void> _alFallar(
    DioException fallo,
    ErrorInterceptorHandler siguiente,
  ) async {
    final esReintento = fallo.requestOptions.extra['reintentado'] == true;
    if (fallo.response?.statusCode != 401 || esReintento || _sesion == null) {
      siguiente.next(fallo);
      return;
    }

    final renovada = await _renovar();
    if (renovada == null) {
      await cerrarSesion();
      alExpirarSesion?.call();
      siguiente.next(fallo);
      return;
    }

    final opciones = fallo.requestOptions;
    opciones.extra['reintentado'] = true;
    opciones.headers['Authorization'] = 'Bearer ${renovada.accessToken}';

    try {
      siguiente.resolve(await _dio.fetch<dynamic>(opciones));
    } on DioException catch (otro) {
      siguiente.next(otro);
    }
  }

  /// Varias llamadas a la vez comparten una sola renovación: el backend rota el
  /// refresh token y el segundo intento fallaría.
  Future<Sesion?> _renovar() {
    _renovacionEnCurso ??= _hacerRenovacion().whenComplete(() {
      _renovacionEnCurso = null;
    });
    return _renovacionEnCurso!;
  }

  Future<Sesion?> _hacerRenovacion() async {
    final refresco = _sesion?.refreshToken;
    if (refresco == null) {
      return null;
    }

    try {
      final respuesta = await _dioSinToken.post<Map<String, dynamic>>(
        '/api/auth/refresh',
        data: {'refreshToken': refresco},
      );
      final renovada = Sesion.desdeJson(respuesta.data!);
      await _guardar(renovada);
      return renovada;
    } on DioException {
      return null;
    }
  }

  String _mensajeDeError(DioException fallo) {
    final cuerpo = fallo.response?.data;
    if (cuerpo is Map && cuerpo['error'] is String) {
      final detalle = cuerpo['error'] as String;
      if (detalle.isNotEmpty) {
        return detalle;
      }
    }

    final estado = fallo.response?.statusCode;
    if (estado == 401) return 'La sesión expiró. Vuelve a iniciar sesión.';
    if (estado == 403) return 'No tienes permiso para esta operación.';
    if (estado == 404) return 'No se encontró el registro.';
    if (estado != null) return 'La API respondió $estado.';
    return 'No se pudo conectar con la API en ${urlBaseApi()}. '
        '¿Está levantada y es esa la dirección correcta?';
  }
}
