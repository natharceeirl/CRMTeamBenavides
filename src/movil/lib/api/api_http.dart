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

  Future<void> cerrarSesion() async {
    try {
      final token = _sesion?.accessToken;
      if (token != null) {
        await _dioSinToken.post<void>(
          '/api/auth/logout',
          options: Options(headers: {'Authorization': 'Bearer $token'}),
        );
      }
    } catch (_) {
      // Si la API falla o no hay conexión, el logout local procede de todas formas.
    } finally {
      _sesion = null;
      await _almacen.borrar();
    }
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

  Future<List<OrdenServicioApi>> ordenes({int? estado, String? clienteId}) async {
    final parametros = <String, String>{};
    if (estado != null) {
      parametros['estado'] = estado.toString();
    }
    if (clienteId != null && clienteId.isNotEmpty) {
      parametros['clienteId'] = clienteId;
    }
    final uri = Uri(
      path: '/api/ordenes-servicio',
      queryParameters: parametros.isEmpty ? null : parametros,
    );
    final datos = await _lista(uri.toString());
    return datos.map(OrdenServicioApi.desdeJson).toList();
  }

  Future<OrdenServicioDetalleApi> orden(String id) async {
    final datos = await _pedir<Map<String, dynamic>>('/api/ordenes-servicio/$id');
    return OrdenServicioDetalleApi.desdeJson(datos);
  }

  /// El backend pasa la orden a Diagnóstico si estaba Abierta.
  Future<OrdenServicioApi> registrarDiagnostico(
    String id,
    String diagnostico, {
    String? solucion,
  }) async {
    try {
      final respuesta = await _dio.put<Map<String, dynamic>>(
        '/api/ordenes-servicio/$id/diagnostico',
        data: {
          'diagnostico': diagnostico,
          if (solucion != null && solucion.isNotEmpty) 'solucion': solucion,
        },
      );
      return OrdenServicioApi.desdeJson(respuesta.data!);
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  /// La observación queda en el historial de la orden con el usuario y la hora.
  Future<OrdenServicioApi> cambiarEstado(
    String id,
    int nuevoEstado, {
    String? observaciones,
  }) async {
    try {
      final respuesta = await _dio.put<Map<String, dynamic>>(
        '/api/ordenes-servicio/$id/estado',
        data: {
          'nuevoEstado': nuevoEstado,
          'observaciones': observaciones == null || observaciones.isEmpty ? null : observaciones,
        },
      );
      return OrdenServicioApi.desdeJson(respuesta.data!);
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  /// Respuesta al presupuesto (EstadoPresupuestoCliente). El cliente solo puede
  /// responder sus propias órdenes: el backend lo verifica.
  Future<OrdenServicioApi> responderPresupuesto(
    String id,
    int estado, {
    String? observaciones,
  }) async {
    try {
      final respuesta = await _dio.put<Map<String, dynamic>>(
        '/api/ordenes-servicio/$id/aprobacion-cliente',
        data: {
          'estado': estado,
          'observaciones': observaciones == null || observaciones.isEmpty ? null : observaciones,
        },
      );
      return OrdenServicioApi.desdeJson(respuesta.data!);
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  /// Nombres exactos de CambiarPasswordRequest en el backend.
  Future<void> cambiarPassword(String actual, String nueva) async {
    try {
      await _dio.post<void>(
        '/api/auth/cambiar-password',
        data: {'passwordActual': actual, 'passwordNueva': nueva},
      );
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<List<ProductoApi>> productos() async {
    final datos = await _lista('/api/productos');
    return datos.map(ProductoApi.desdeJson).toList();
  }

  Future<List<ServicioApi>> servicios() async {
    final datos = await _lista('/api/servicios?soloActivos=true');
    return datos.map(ServicioApi.desdeJson).toList();
  }

  /// POST /api/ordenes-servicio/{id}/detalles. Sin precio, el backend usa el
  /// del catálogo; con otro, deja la orden pendiente de aprobación de Gerencia.
  Future<void> agregarItem(
    String ordenId, {
    required int tipoItem,
    required int cantidad,
    String? productoId,
    String? servicioId,
    String? descripcion,
    double? precioUnitario,
    int? tipoAfectacionIgv,
  }) async {
    try {
      await _dio.post<Map<String, dynamic>>(
        '/api/ordenes-servicio/$ordenId/detalles',
        data: {
          'tipoItem': tipoItem,
          'cantidad': cantidad,
          'productoId': productoId,
          'servicioId': servicioId,
          'descripcion': descripcion,
          'precioUnitario': precioUnitario,
          'tipoAfectacionIgv': tipoAfectacionIgv,
        },
      );
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  /// PUT /api/ordenes-servicio/{id}/detalles/{detalleId}: se manda solo lo que
  /// cambia. Otro precio que el de lista deja la orden pendiente de Gerencia.
  Future<void> actualizarItem(
    String ordenId,
    String detalleId, {
    int? cantidad,
    double? precioUnitario,
  }) async {
    try {
      await _dio.put<Map<String, dynamic>>(
        '/api/ordenes-servicio/$ordenId/detalles/$detalleId',
        data: {
          'cantidad': ?cantidad,
          'precioUnitario': ?precioUnitario,
        },
      );
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  /// Un repuesto quitado vuelve al stock.
  Future<void> quitarItem(String ordenId, String detalleId) async {
    try {
      await _dio.delete<void>('/api/ordenes-servicio/$ordenId/detalles/$detalleId');
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<List<FotoOrdenApi>> fotosOrden(String ordenId) async {
    final datos = await _lista('/api/ordenes-servicio/$ordenId/fotos');
    return datos.map(FotoOrdenApi.desdeJson).toList();
  }

  /// Sube una foto como multipart: `archivo`, `etapa` y `observacion`, igual
  /// que la web. La API acepta JPG, PNG o WEBP.
  Future<void> subirFotoOrden(
    String ordenId, {
    required List<int> bytes,
    required String nombreArchivo,
    required int etapa,
    String? observacion,
  }) async {
    final extension = nombreArchivo.split('.').last.toLowerCase();
    final tipo = switch (extension) {
      'png' => 'png',
      'webp' => 'webp',
      _ => 'jpeg',
    };
    final formulario = FormData.fromMap({
      'archivo': MultipartFile.fromBytes(
        bytes,
        filename: nombreArchivo,
        contentType: DioMediaType('image', tipo),
      ),
      'etapa': etapa.toString(),
      if (observacion != null && observacion.trim().isNotEmpty) 'observacion': observacion.trim(),
    });
    try {
      await _dio.post<Map<String, dynamic>>('/api/ordenes-servicio/$ordenId/fotos', data: formulario);
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  /// El archivo de una foto pide token, así que no se carga con una URL suelta:
  /// se descarga por aquí, con la renovación del token incluida.
  Future<List<int>> archivoDeFoto(String urlRelativa) async {
    try {
      final respuesta = await _dio.get<List<int>>(
        urlRelativa,
        options: Options(responseType: ResponseType.bytes),
      );
      return respuesta.data ?? const [];
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<void> eliminarFotoOrden(String ordenId, String fotoId) async {
    try {
      await _dio.delete<void>('/api/ordenes-servicio/$ordenId/fotos/$fotoId');
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<List<CategoriaProductoApi>> categoriasProducto() async {
    final datos = await _lista('/api/categorias-producto');
    return datos.map(CategoriaProductoApi.desdeJson).toList();
  }

  Future<List<MovimientoInventarioApi>> movimientosDeProducto(String id) async {
    final datos = await _lista('/api/productos/$id/movimientos');
    return datos.map(MovimientoInventarioApi.desdeJson).toList();
  }

  Future<List<VentaApi>> ventas({int? estado, String? clienteId}) async {
    final parametros = <String, String>{};
    if (estado != null) {
      parametros['estado'] = estado.toString();
    }
    if (clienteId != null && clienteId.isNotEmpty) {
      parametros['clienteId'] = clienteId;
    }
    final uri = Uri(
      path: '/api/ventas',
      queryParameters: parametros.isEmpty ? null : parametros,
    );
    final datos = await _lista(uri.toString());
    return datos.map(VentaApi.desdeJson).toList();
  }

  Future<VentaDetalleApi> venta(String id) async {
    final datos = await _pedir<Map<String, dynamic>>('/api/ventas/$id');
    return VentaDetalleApi.desdeJson(datos);
  }

  Future<PortalResumenApi> portalResumen() async {
    final datos = await _pedir<Map<String, dynamic>>('/api/portal/resumen');
    return PortalResumenApi.desdeJson(datos);
  }

  Future<List<ComprobantePortalApi>> portalComprobantes() async {
    final datos = await _lista('/api/portal/comprobantes');
    return datos.map(ComprobantePortalApi.desdeJson).toList();
  }

  Future<List<AtencionServicioApi>> historialServicio(String vehiculoId) async {
    final datos = await _lista('/api/vehiculos/$vehiculoId/historial-servicio');
    return datos.map(AtencionServicioApi.desdeJson).toList();
  }

  /// Sin fechas trae todas; la agenda del personal pide solo su semana.
  Future<List<CitaApi>> citas({DateTime? desde, DateTime? hasta}) async {
    final parametros = <String, String>{
      if (desde != null) 'fechaInicio': desde.toUtc().toIso8601String(),
      if (hasta != null) 'fechaFin': hasta.toUtc().toIso8601String(),
    };
    final consulta = parametros.isEmpty ? '' : '?${Uri(queryParameters: parametros).query}';
    final datos = await _lista('/api/citas$consulta');
    return datos.map(CitaApi.desdeJson).toList();
  }

  Future<void> cambiarEstadoCita(String id, String nuevoEstado, {String? observacion}) async {
    try {
      await _dio.put<Map<String, dynamic>>(
        '/api/citas/$id/estado',
        data: {'nuevoEstado': nuevoEstado, 'observacion': observacion == null || observacion.isEmpty ? null : observacion},
      );
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<List<PedidoLimaApi>> pedidosLima() async {
    final datos = await _lista('/api/pedidos-lima');
    return datos.map(PedidoLimaApi.desdeJson).toList();
  }

  /// El cliente no manda su id: la API toma el del usuario y verifica que la
  /// unidad sea suya. Sin duración, el backend reserva una hora.
  Future<void> agendarCita({
    required String vehiculoId,
    required DateTime fechaHora,
    required String motivo,
    String? observaciones,
  }) async {
    try {
      await _dio.post<Map<String, dynamic>>(
        '/api/citas',
        data: {
          'clienteId': null,
          'vehiculoId': vehiculoId,
          'fechaHoraProgramada': fechaHora.toUtc().toIso8601String(),
          'duracionMinutos': null,
          'motivo': motivo,
          'observaciones': observaciones == null || observaciones.isEmpty ? null : observaciones,
        },
      );
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<void> cancelarCita(String id, String motivo) async {
    try {
      await _dio.put<Map<String, dynamic>>('/api/citas/$id/cancelar', data: {'motivoCancelacion': motivo});
    } on DioException catch (fallo) {
      throw ErrorApi(_mensajeDeError(fallo), fallo.response?.statusCode);
    }
  }

  Future<CajaActualApi> cajaActual() async {
    final datos = await _pedir<Map<String, dynamic>>('/api/caja-chica/actual');
    return CajaActualApi.desdeJson(datos);
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
    // Unos módulos responden { error }, otros (citas, pedidos) { mensaje }.
    final cuerpo = fallo.response?.data;
    if (cuerpo is Map) {
      for (final clave in const ['error', 'mensaje']) {
        final detalle = cuerpo[clave];
        if (detalle is String && detalle.isNotEmpty) {
          return detalle;
        }
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
