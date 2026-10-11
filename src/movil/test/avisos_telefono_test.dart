import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:crm_team_benavides/api/almacen_sesion.dart';
import 'package:crm_team_benavides/api/api_http.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/avisos_telefono.dart';
import 'package:crm_team_benavides/rutas.dart';
import 'package:dio/dio.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'pantalla_inicio_test.dart' show SesionAbierta;

/// Firebase sin Firebase: el token, las que llegan y las que se tocan las pone la prueba.
class TelefonoFalso extends TelefonoNotificaciones {
  TelefonoFalso({this.tokenActual = 'tok-1', this.alAbrir});

  String? tokenActual;
  Map<String, dynamic>? alAbrir;
  final renovaciones = StreamController<String>.broadcast();
  final llegadas = StreamController<RemoteMessage>.broadcast();
  final toques = StreamController<Map<String, dynamic>>.broadcast();
  final mostradas = <RemoteMessage>[];

  @override
  bool get activo => true;

  @override
  int get plataforma => 0;

  @override
  Future<String?> token() async => tokenActual;

  @override
  Stream<String> get tokenRenovado => renovaciones.stream;

  @override
  Stream<RemoteMessage> get recibidas => llegadas.stream;

  @override
  Stream<Map<String, dynamic>> get tocadas => toques.stream;

  @override
  Map<String, dynamic>? tomarAperturaInicial() {
    final datos = alAbrir;
    alAbrir = null;
    return datos;
  }

  @override
  Future<void> mostrar(RemoteMessage mensaje) async => mostradas.add(mensaje);
}

class ApiAvisos extends ApiHttp {
  ApiAvisos() : super(AlmacenSesion());

  final registrados = <(String, int)>[];
  final leidas = <String>[];

  @override
  Future<void> registrarDispositivo(String token, int plataforma) async => registrados.add((token, plataforma));

  @override
  Future<void> marcarNotificacionLeida(String id) async => leidas.add(id);
}

/// Como al abrir la app desde una notificación: primero lee la sesión guardada.
class SesionCargando extends SesionNotifier {
  @override
  EstadoSesion build() => const EstadoSesion(cargando: true);

  void abrir() => state = const EstadoSesion(
        usuario: UsuarioSesion(id: 'u1', email: 'gerencia@teambenavides.pe', nombre: 'Gerencia'),
        roles: ['Gerencia/Admin'],
      );
}

Map<String, dynamic> _datos({String entidad = 'Aprobaciones', String entidadId = ''}) =>
    {'notificacionId': 'n1', 'tipo': 'SolicitudAprobacion', 'entidad': entidad, 'entidadId': entidadId};

GoRouter _rutas() => GoRouter(routes: [
      GoRoute(path: '/', builder: (context, estado) => const Text('INICIO')),
      GoRoute(path: '/aprobaciones', builder: (context, estado) => const Text('BANDEJA DE APROBACIONES')),
      GoRoute(path: '/ordenes/:id', builder: (context, estado) => Text('ORDEN ${estado.pathParameters['id']}')),
    ]);

/// La app mínima: el router de la prueba y los avisos del teléfono escuchando.
Future<void> _montar(
  WidgetTester tester, {
  required TelefonoFalso telefono,
  required ApiAvisos api,
  SesionNotifier Function() sesion = SesionAbierta.new,
  int Function()? conteo,
}) async {
  final rutas = _rutas();
  await tester.pumpWidget(ProviderScope(
    overrides: [
      telefonoProvider.overrideWithValue(telefono),
      apiProvider.overrideWithValue(api),
      sesionProvider.overrideWith(sesion),
      routerProvider.overrideWithValue(rutas),
      conteoNotificacionesProvider.overrideWith((ref) => conteo?.call() ?? 0),
    ],
    child: Consumer(builder: (context, ref, _) {
      ref.watch(avisosTelefonoProvider);
      ref.watch(conteoNotificacionesProvider);
      return MaterialApp.router(routerConfig: rutas);
    }),
  ));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('al entrar registra el teléfono con su token', (tester) async {
    final api = ApiAvisos();
    await _montar(tester, telefono: TelefonoFalso(), api: api);

    expect(api.registrados, [('tok-1', 0)]);
  });

  testWidgets('sin permiso de notificaciones no registra nada', (tester) async {
    final api = ApiAvisos();
    await _montar(tester, telefono: TelefonoFalso(tokenActual: null), api: api);

    expect(api.registrados, isEmpty);
  });

  testWidgets('si Firebase renueva el token lo vuelve a registrar', (tester) async {
    final api = ApiAvisos();
    final telefono = TelefonoFalso();
    await _montar(tester, telefono: telefono, api: api);

    telefono.tokenActual = 'tok-2';
    telefono.renovaciones.add('tok-2');
    await tester.pumpAndSettle();

    expect(api.registrados, [('tok-1', 0), ('tok-2', 0)]);
  });

  testWidgets('con la app abierta la muestra y refresca la campanita', (tester) async {
    final telefono = TelefonoFalso();
    var consultas = 0;
    await _montar(tester, telefono: telefono, api: ApiAvisos(), conteo: () => ++consultas);
    final antes = consultas;

    telefono.llegadas.add(RemoteMessage(
      data: _datos(),
      notification: const RemoteNotification(title: 'Precio por aprobar', body: 'OS-000123 · pedida por Tito'),
    ));
    await tester.pumpAndSettle();

    expect(telefono.mostradas, hasLength(1));
    expect(consultas, greaterThan(antes));
  });

  testWidgets('tocarla la marca como leída y abre su pantalla', (tester) async {
    final api = ApiAvisos();
    final telefono = TelefonoFalso();
    await _montar(tester, telefono: telefono, api: api);

    telefono.toques.add(_datos(entidad: 'OrdenServicio', entidadId: 'os-1'));
    await tester.pumpAndSettle();

    expect(find.text('ORDEN os-1'), findsOneWidget);
    expect(api.leidas, ['n1']);
  });

  testWidgets('si la abrió con la app cerrada espera a la sesión y después la abre', (tester) async {
    final api = ApiAvisos();
    await _montar(
      tester,
      telefono: TelefonoFalso(alAbrir: _datos()),
      api: api,
      sesion: SesionCargando.new,
    );
    expect(find.text('BANDEJA DE APROBACIONES'), findsNothing);
    expect(api.registrados, isEmpty);

    final contenedor = ProviderScope.containerOf(tester.element(find.byType(MaterialApp)));
    (contenedor.read(sesionProvider.notifier) as SesionCargando).abrir();
    await tester.pumpAndSettle();

    expect(find.text('BANDEJA DE APROBACIONES'), findsOneWidget);
    expect(api.registrados, [('tok-1', 0)]);
    expect(api.leidas, ['n1']);
  });

  group('cerrar sesión', () {
    test('da de baja el teléfono antes del logout y olvida el token', () async {
      final adaptador = AdaptadorFalso();
      final api = ApiHttp(
        AlmacenFalso(),
        dio: Dio(BaseOptions(baseUrl: 'http://api'))..httpClientAdapter = adaptador,
        dioSinToken: Dio(BaseOptions(baseUrl: 'http://api'))..httpClientAdapter = adaptador,
      );
      await api.recuperarSesionGuardada();

      await api.registrarDispositivo('tok-1', 0);
      await api.cerrarSesion();

      expect(adaptador.llamadas, [
        'POST /api/notificaciones/dispositivos {"token":"tok-1","plataforma":0}',
        'POST /api/notificaciones/dispositivos/baja {"token":"tok-1"}',
        'POST /api/auth/logout ',
      ]);
      expect(api.tokenDispositivo, isNull);
    });

    test('sin teléfono registrado solo cierra la sesión', () async {
      final adaptador = AdaptadorFalso();
      final api = ApiHttp(
        AlmacenFalso(),
        dioSinToken: Dio(BaseOptions(baseUrl: 'http://api'))..httpClientAdapter = adaptador,
      );
      await api.recuperarSesionGuardada();

      await api.cerrarSesion();

      expect(adaptador.llamadas, ['POST /api/auth/logout ']);
    });
  });
}

/// Una sesión vigente guardada, sin el almacén seguro del teléfono.
class AlmacenFalso extends AlmacenSesion {
  @override
  Future<Sesion?> leer() async => Sesion(
        accessToken: 'acceso',
        accessTokenExpiration: DateTime.now().toUtc().add(const Duration(hours: 1)),
        refreshToken: 'refresco',
        refreshTokenExpiration: DateTime.now().toUtc().add(const Duration(days: 7)),
      );

  @override
  Future<void> guardar(Sesion sesion) async {}

  @override
  Future<void> borrar() async {}
}

/// Anota cada llamada y responde 200 sin cuerpo.
class AdaptadorFalso implements HttpClientAdapter {
  final llamadas = <String>[];

  @override
  Future<ResponseBody> fetch(RequestOptions opciones, Stream<Uint8List>? cuerpo, Future<void>? cancelar) async {
    final datos = opciones.data == null ? '' : jsonEncode(opciones.data);
    llamadas.add('${opciones.method} ${opciones.path} $datos');
    return ResponseBody.fromString('', 200);
  }

  @override
  void close({bool force = false}) {}
}
