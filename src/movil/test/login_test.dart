import 'dart:convert';
import 'dart:typed_data';

import 'package:crm_team_benavides/api/almacen_sesion.dart';
import 'package:crm_team_benavides/api/api_http.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  const bloqueo =
      'Correo o contraseña incorrectos. Después de 5 intentos fallidos la cuenta se bloquea 15 minutos.';
  const demasiados =
      'Demasiados intentos de inicio de sesión desde esta conexión. Espera un minuto y vuelve a intentar.';

  ApiHttp apiQueResponde(int estado, Object? cuerpo) => ApiHttp(
        AlmacenSinSesion(),
        dioSinToken: Dio(BaseOptions(baseUrl: 'http://api'))
          ..httpClientAdapter = AdaptadorFijo(estado, cuerpo),
      );

  Future<ErrorApi> errorAlEntrar(ApiHttp api) async {
    try {
      await api.iniciarSesion('tecnico@teambenavides.pe', 'no-es-la-clave');
    } on ErrorApi catch (error) {
      return error;
    }
    fail('El login debía fallar');
  }

  test('muestra el mensaje de la API, que avisa del bloqueo por intentos fallidos', () async {
    final error = await errorAlEntrar(apiQueResponde(401, {'error': bloqueo}));

    expect(error.mensaje, bloqueo);
    expect(error.estado, 401);
  });

  test('un 401 sin cuerpo sigue diciendo que el correo o la contraseña no coinciden', () async {
    final error = await errorAlEntrar(apiQueResponde(401, null));

    expect(error.mensaje, 'Correo o contraseña incorrectos.');
  });

  test('con demasiados intentos desde la misma conexión dice que espere', () async {
    final error = await errorAlEntrar(apiQueResponde(429, {'error': demasiados}));

    expect(error.mensaje, demasiados);
    expect(error.estado, 429);
  });
}

/// Sin sesión guardada, como en la pantalla de login.
class AlmacenSinSesion extends AlmacenSesion {
  @override
  Future<Sesion?> leer() async => null;

  @override
  Future<void> guardar(Sesion sesion) async {}

  @override
  Future<void> borrar() async {}
}

/// Responde siempre lo mismo, con el JSON como lo manda la API.
class AdaptadorFijo implements HttpClientAdapter {
  AdaptadorFijo(this.estado, this.cuerpo);

  final int estado;
  final Object? cuerpo;

  @override
  Future<ResponseBody> fetch(RequestOptions opciones, Stream<Uint8List>? datos, Future<void>? cancelar) async =>
      ResponseBody.fromString(
        cuerpo == null ? '' : jsonEncode(cuerpo),
        estado,
        headers: {
          Headers.contentTypeHeader: [Headers.jsonContentType],
        },
      );

  @override
  void close({bool force = false}) {}
}
