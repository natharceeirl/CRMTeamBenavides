import 'dart:convert';
import 'dart:typed_data';

import 'package:crm_team_benavides/api/api_http.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/activar_cuenta.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'login_test.dart' show AlmacenSinSesion;

void main() {
  group('API', () {
    test('manda el DNI, el código y la contraseña y queda con la sesión que responde', () async {
      final adaptador = AdaptadorDeSesion();
      final api = ApiHttp(
        AlmacenSinSesion(),
        dioSinToken: Dio(BaseOptions(baseUrl: 'http://api'))..httpClientAdapter = adaptador,
      );

      final sesion = await api.activarCuenta(documento: '40123456', codigo: '042917', password: 'Mi.Clave1');

      expect(adaptador.ruta, '/api/auth/activar-cuenta');
      expect(adaptador.cuerpo, {'numeroDocumento': '40123456', 'codigo': '042917', 'password': 'Mi.Clave1', 'email': null});
      expect(sesion.accessToken, 'token-de-acceso');
      expect(api.sesion?.refreshToken, 'token-de-refresco');
    });
  });

  group('pantalla', () {
    testWidgets('pide el código de 6 dígitos y la misma contraseña dos veces antes de llamar a la API', (tester) async {
      final api = ApiQueActiva();
      await montar(tester, api);

      await tester.enterText(find.widgetWithText(TextFormField, 'DNI'), '40123456');
      await tester.enterText(find.widgetWithText(TextFormField, 'Código de activación'), '0429');
      await tester.enterText(find.widgetWithText(TextFormField, 'Contraseña nueva'), 'Mi.Clave1');
      await tester.enterText(find.widgetWithText(TextFormField, 'Repite la contraseña'), 'otra');
      await tester.tap(find.text('Activar y entrar'));
      await tester.pumpAndSettle();

      expect(find.text('El código tiene 6 dígitos'), findsOneWidget);
      expect(find.text('Las contraseñas no coinciden'), findsOneWidget);
      expect(api.llamadas, isEmpty);
    });

    testWidgets('sin correo lo manda vacío y muestra el motivo si la API no acepta el código', (tester) async {
      final api = ApiQueActiva(error: ErrorApi('El DNI o el código no son válidos, o el código ya venció.', 400));
      await montar(tester, api);

      await tester.enterText(find.widgetWithText(TextFormField, 'DNI'), ' 40123456 ');
      await tester.enterText(find.widgetWithText(TextFormField, 'Código de activación'), '042917');
      await tester.enterText(find.widgetWithText(TextFormField, 'Contraseña nueva'), 'Mi.Clave1');
      await tester.enterText(find.widgetWithText(TextFormField, 'Repite la contraseña'), 'Mi.Clave1');
      await tester.tap(find.text('Activar y entrar'));
      await tester.pumpAndSettle();

      expect(api.llamadas, ['40123456 042917 Mi.Clave1 null']);
      expect(find.textContaining('El DNI o el código no son válidos'), findsOneWidget);
    });
  });
}

Future<void> montar(WidgetTester tester, ApiHttp api) async {
  // La pantalla es larga: con el alto por defecto el botón queda fuera de la vista.
  tester.view.physicalSize = const Size(1080, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);

  await tester.pumpWidget(
    ProviderScope(
      overrides: [apiProvider.overrideWithValue(api)],
      child: const MaterialApp(home: PantallaActivarCuenta()),
    ),
  );
  await tester.pumpAndSettle();
}

/// Anota lo que recibe; con [error], falla como la API.
class ApiQueActiva extends ApiHttp {
  ApiQueActiva({this.error}) : super(AlmacenSinSesion());

  final ErrorApi? error;
  final llamadas = <String>[];

  @override
  Future<Sesion> activarCuenta({
    required String documento,
    required String codigo,
    required String password,
    String? email,
  }) async {
    llamadas.add('$documento $codigo $password $email');
    throw error ?? ErrorApi('sin respuesta', 500);
  }
}

/// Responde la sesión como la API al activar la cuenta y anota la petición.
class AdaptadorDeSesion implements HttpClientAdapter {
  String? ruta;
  Object? cuerpo;

  @override
  Future<ResponseBody> fetch(RequestOptions opciones, Stream<Uint8List>? datos, Future<void>? cancelar) async {
    ruta = opciones.path;
    cuerpo = opciones.data;
    return ResponseBody.fromString(
      jsonEncode({
        'accessToken': 'token-de-acceso',
        'accessTokenExpiration': '2026-11-03T15:00:00Z',
        'refreshToken': 'token-de-refresco',
        'refreshTokenExpiration': '2026-11-10T15:00:00Z',
      }),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
