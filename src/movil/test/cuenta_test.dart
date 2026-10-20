import 'dart:convert';
import 'dart:typed_data';

import 'package:crm_team_benavides/api/api_http.dart';
import 'package:crm_team_benavides/api/config.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/eliminar_cuenta.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'login_test.dart' show AlmacenSinSesion;
import 'pantalla_inicio_test.dart' show SesionCliente, SesionTecnico, montarInicio;

void main() {
  group('configuración de release', () {
    test('en debug no exige nada: el emulador usa 10.0.2.2', () {
      expect(problemaDeConfiguracion(release: false, url: ''), isNull);
    });

    test('en release sin API_URL lo dice en vez de llamar a 10.0.2.2', () {
      expect(problemaDeConfiguracion(release: true, url: ''), contains('API_URL'));
    });

    test('en release exige HTTPS', () {
      expect(problemaDeConfiguracion(release: true, url: 'http://192.168.1.50:5021'), contains('https://'));
      expect(problemaDeConfiguracion(release: true, url: 'https://app.teambenavides.com'), isNull);
    });

    test('la política de privacidad está en la web, en el mismo dominio que la API', () {
      expect(urlPrivacidad(base: 'https://app.teambenavides.com').toString(), 'https://app.teambenavides.com/privacidad');
      expect(urlPrivacidad(base: 'https://app.teambenavides.com/').toString(), 'https://app.teambenavides.com/privacidad');
    });
  });

  group('eliminar la cuenta', () {
    testWidgets('el cliente la tiene en el menú, junto a la política de privacidad', (tester) async {
      await montarInicio(tester, sesion: SesionCliente.new);
      await tester.tap(find.byTooltip('Más opciones'));
      await tester.pumpAndSettle();

      expect(find.text('Eliminar mi cuenta'), findsOneWidget);
      expect(find.text('Política de privacidad'), findsOneWidget);
    });

    testWidgets('el personal no: a su cuenta la da de baja Gerencia', (tester) async {
      await montarInicio(tester, sesion: SesionTecnico.new);
      await tester.tap(find.byTooltip('Más opciones'));
      await tester.pumpAndSettle();

      expect(find.text('Eliminar mi cuenta'), findsNothing);
      expect(find.text('Política de privacidad'), findsOneWidget);
    });

    testWidgets('pide la contraseña y la manda a la API', (tester) async {
      final api = ApiQueElimina();
      await montarDialogo(tester, api);

      await tester.tap(find.widgetWithText(FilledButton, 'Eliminar mi cuenta'));
      await tester.pumpAndSettle();
      expect(find.text('Ingresa tu contraseña'), findsOneWidget);
      expect(api.passwords, isEmpty);

      await tester.enterText(find.byType(TextFormField), 'Mi.Clave1');
      await tester.tap(find.widgetWithText(FilledButton, 'Eliminar mi cuenta'));
      await tester.pumpAndSettle();

      expect(api.passwords, ['Mi.Clave1']);
      expect(find.text('Tu cuenta se eliminó'), findsOneWidget);
    });

    testWidgets('con la contraseña equivocada muestra el motivo y sigue abierto', (tester) async {
      final api = ApiQueElimina(error: ErrorApi('La contraseña no es correcta.', 400));
      await montarDialogo(tester, api);

      await tester.enterText(find.byType(TextFormField), 'otra');
      await tester.tap(find.widgetWithText(FilledButton, 'Eliminar mi cuenta'));
      await tester.pumpAndSettle();

      expect(find.text('La contraseña no es correcta.'), findsOneWidget);
      expect(find.byType(DialogoEliminarCuenta), findsOneWidget);
    });

    test('llama al endpoint de la API con la contraseña', () async {
      final adaptador = AdaptadorQueAnota();
      final api = ApiHttp(
        AlmacenSinSesion(),
        dio: Dio(BaseOptions(baseUrl: 'http://api'))..httpClientAdapter = adaptador,
      );

      await api.eliminarCuenta('Mi.Clave1');

      expect(adaptador.llamadas, ['POST /api/auth/eliminar-cuenta {"password":"Mi.Clave1"}']);
    });
  });
}

Future<void> montarDialogo(WidgetTester tester, ApiQueElimina api) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        apiProvider.overrideWithValue(api),
        sesionProvider.overrideWith(SesionCliente.new),
      ],
      child: MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: TextButton(onPressed: () => abrirEliminarCuenta(context), child: const Text('abrir')),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('abrir'));
  await tester.pumpAndSettle();
}

/// Anota las contraseñas recibidas; con [error], falla como la API.
class ApiQueElimina extends ApiHttp {
  ApiQueElimina({this.error}) : super(AlmacenSinSesion());

  final ErrorApi? error;
  final passwords = <String>[];

  @override
  Future<void> eliminarCuenta(String password) async {
    if (error != null) throw error!;
    passwords.add(password);
  }

  @override
  Future<void> cerrarSesion() async {}
}

/// Anota cada llamada y responde 204, como la API al eliminar la cuenta.
class AdaptadorQueAnota implements HttpClientAdapter {
  final llamadas = <String>[];

  @override
  Future<ResponseBody> fetch(RequestOptions opciones, Stream<Uint8List>? cuerpo, Future<void>? cancelar) async {
    llamadas.add('${opciones.method} ${opciones.path} ${jsonEncode(opciones.data)}');
    return ResponseBody.fromString('', 204);
  }

  @override
  void close({bool force = false}) {}
}
