import 'package:crm_team_benavides/api/almacen_sesion.dart';
import 'package:crm_team_benavides/api/api_http.dart';
import 'package:crm_team_benavides/api/estados.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/aprobaciones.dart';
import 'package:crm_team_benavides/pantallas/notificaciones.dart';
import 'package:crm_team_benavides/pantallas/orden_detalle.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'pantalla_inicio_test.dart' show SesionAbierta, SesionCliente;

/// Guarda lo que las pantallas mandan al backend en lugar de llamarlo.
class ApiFalsa extends ApiHttp {
  ApiFalsa() : super(AlmacenSesion());

  final leidas = <String>[];
  final resueltas = <Map<String, Object?>>[];
  final presupuestosEnviados = <String>[];

  @override
  Future<void> marcarNotificacionLeida(String id) async => leidas.add(id);

  @override
  Future<void> resolverAprobacion(String id, int estado, {String? observaciones}) async =>
      resueltas.add({'id': id, 'estado': estado, 'observaciones': observaciones});

  @override
  Future<OrdenServicioApi> enviarPresupuesto(String ordenId) async {
    presupuestosEnviados.add(ordenId);
    return _orden().orden;
  }
}

/// Recepción: edita órdenes, pero no decide los precios.
class SesionRecepcion extends SesionNotifier {
  @override
  EstadoSesion build() => const EstadoSesion(
        usuario: UsuarioSesion(id: 'r', email: 'recepcion@teambenavides.pe', nombre: 'Rosa'),
        roles: ['Recepcion'],
        permisos: ['ordenes.ver_todas', 'ordenes.editar', 'ordenes.cambiar_estado', 'ordenes.agregar_items'],
      );
}

NotificacionApi _aviso(String id, {required String entidad, String? entidadId, bool leida = false}) => NotificacionApi(
      id: id,
      tipo: entidad == 'Aprobaciones' ? TipoNotificacion.solicitudAprobacion : TipoNotificacion.ordenLista,
      titulo: entidad == 'Aprobaciones' ? 'Precio por aprobar' : 'Tu unidad está lista',
      mensaje: 'OS-000123 · pedida por Tito',
      fechaCreacion: DateTime(2026, 10, 11, 9, 30),
      entidad: entidad,
      entidadId: entidadId,
      fechaLeida: leida ? DateTime(2026, 10, 11, 10) : null,
    );

OrdenServicioDetalleApi _orden({int estadoId = EstadoOrden.diagnostico, int gerencia = EstadoGerencia.noAplica, double total = 94.4}) =>
    OrdenServicioDetalleApi.desdeJson({
      'id': 'os-1',
      'numeroOrden': 'OS-000123',
      'vehiculoPlaca': '',
      'vehiculoMarca': 'Yamaha',
      'vehiculoModelo': 'XMAX 300',
      'clienteId': 'c1',
      'clienteNombre': 'Luis Quispe',
      'estado': 'Diagnostico',
      'estadoId': estadoId,
      'fechaApertura': '2026-10-11T14:00:00Z',
      'total': total,
      'estadoPresupuestoClienteId': EstadoPresupuesto.pendiente,
      'estadoAprobacionGerenciaId': gerencia,
      'detalles': <Map<String, dynamic>>[],
      'historial': <Map<String, dynamic>>[],
    });

void main() {
  group('rutaDeNotificacion', () {
    test('las solicitudes llevan a la bandeja de aprobaciones y las órdenes a su detalle', () {
      expect(rutaDeNotificacion(_aviso('1', entidad: 'Aprobaciones'), esCliente: false), '/aprobaciones');
      expect(rutaDeNotificacion(_aviso('2', entidad: 'OrdenServicio', entidadId: 'os-1'), esCliente: true), '/ordenes/os-1');
    });

    test('la cita lleva a la agenda del personal; el cliente la ve en su inicio', () {
      expect(rutaDeNotificacion(_aviso('3', entidad: 'Cita'), esCliente: false), '/agenda');
      expect(rutaDeNotificacion(_aviso('4', entidad: 'Cita'), esCliente: true), isNull);
      expect(rutaDeNotificacion(_aviso('5', entidad: 'PedidoLima'), esCliente: true), isNull);
    });
  });

  testWidgets('la campanita muestra cuántos avisos hay sin leer', (tester) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [conteoNotificacionesProvider.overrideWith((ref) => 3)],
      child: MaterialApp(home: Scaffold(appBar: AppBar(actions: const [CampanaNotificaciones()]))),
    ));
    await tester.pump();

    expect(find.text('3'), findsOneWidget);
    expect(find.byTooltip('Notificaciones, 3 sin leer'), findsOneWidget);
  });

  testWidgets('tocar un aviso lo marca como leído y abre su pantalla', (tester) async {
    final api = ApiFalsa();
    final rutas = GoRouter(routes: [
      GoRoute(path: '/', builder: (context, estado) => const PantallaNotificaciones()),
      GoRoute(path: '/aprobaciones', builder: (context, estado) => const Scaffold(body: Text('BANDEJA DE APROBACIONES'))),
    ]);
    await tester.pumpWidget(ProviderScope(
      overrides: [
        sesionProvider.overrideWith(SesionAbierta.new),
        apiProvider.overrideWithValue(api),
        notificacionesProvider.overrideWith((ref) => [
              _aviso('n1', entidad: 'Aprobaciones'),
              _aviso('n2', entidad: 'OrdenServicio', entidadId: 'os-1', leida: true),
            ]),
        conteoNotificacionesProvider.overrideWith((ref) => 1),
      ],
      child: MaterialApp.router(routerConfig: rutas),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Marcar todas'), findsOneWidget);
    await tester.tap(find.text('Precio por aprobar'));
    await tester.pumpAndSettle();

    expect(api.leidas, ['n1']);
    expect(find.text('BANDEJA DE APROBACIONES'), findsOneWidget);
  });

  group('bandeja de aprobaciones', () {
    final solicitud = SolicitudAprobacionApi(
      id: 's1',
      tipo: 'CambioPrecio',
      entidad: 'OrdenServicio',
      entidadId: 'os-1',
      fechaSolicitud: DateTime(2026, 10, 11, 9),
      estadoId: EstadoGerencia.pendiente,
      detalleCambio: 'Aceite 10W40: S/ 45.00 → S/ 40.00',
      usuarioSolicitanteNombre: 'Tito Técnico',
    );

    Future<ApiFalsa> montar(WidgetTester tester) async {
      final api = ApiFalsa();
      await tester.pumpWidget(ProviderScope(
        overrides: [
          sesionProvider.overrideWith(SesionAbierta.new),
          apiProvider.overrideWithValue(api),
          aprobacionesPendientesProvider.overrideWith((ref) => [solicitud]),
          historialAprobacionesProvider.overrideWith((ref) => [solicitud]),
        ],
        child: const MaterialApp(home: PantallaAprobaciones()),
      ));
      await tester.pumpAndSettle();
      return api;
    }

    testWidgets('muestra la solicitud con quién la pidió', (tester) async {
      await montar(tester);

      expect(find.text('Orden de servicio'), findsOneWidget);
      expect(find.text('Aceite 10W40: S/ 45.00 → S/ 40.00'), findsOneWidget);
      expect(find.textContaining('Pedida por Tito Técnico'), findsOneWidget);
    });

    testWidgets('rechazar exige motivo y lo manda', (tester) async {
      final api = await montar(tester);

      await tester.tap(find.widgetWithText(OutlinedButton, 'Rechazar'));
      await tester.pumpAndSettle();
      final confirmar = find.widgetWithText(FilledButton, 'Rechazar');
      expect(tester.widget<FilledButton>(confirmar).onPressed, isNull, reason: 'sin motivo no se puede rechazar');

      await tester.enterText(find.byType(TextField), 'El precio está fuera de rango');
      await tester.pump();
      await tester.tap(confirmar);
      await tester.pumpAndSettle();

      expect(api.resueltas, [
        {'id': 's1', 'estado': EstadoGerencia.rechazado, 'observaciones': 'El precio está fuera de rango'},
      ]);
    });

    testWidgets('aprobar no exige observación', (tester) async {
      final api = await montar(tester);

      await tester.tap(find.widgetWithText(FilledButton, 'Aprobar'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Aprobar').last);
      await tester.pumpAndSettle();

      expect(api.resueltas.single['estado'], EstadoGerencia.aprobado);
    });

    testWidgets('el historial no muestra las pendientes ni ofrece decidir', (tester) async {
      await montar(tester);

      await tester.tap(find.text('Historial'));
      await tester.pumpAndSettle();

      expect(find.text('Todavía no hay solicitudes resueltas.'), findsOneWidget);
    });
  });

  group('enviar presupuesto', () {
    Future<ApiFalsa> montarOrden(WidgetTester tester, OrdenServicioDetalleApi orden,
        {SesionNotifier Function() sesion = SesionRecepcion.new}) async {
      final api = ApiFalsa();
      await tester.pumpWidget(ProviderScope(
        overrides: [
          sesionProvider.overrideWith(sesion),
          apiProvider.overrideWithValue(api),
          ordenProvider('os-1').overrideWith((ref) => orden),
        ],
        child: const MaterialApp(home: PantallaOrdenDetalle(ordenId: 'os-1')),
      ));
      await tester.pumpAndSettle();
      return api;
    }

    testWidgets('Recepción lo envía en diagnóstico con ítems', (tester) async {
      final api = await montarOrden(tester, _orden());

      final boton = find.text('Enviar presupuesto al cliente');
      await tester.scrollUntilVisible(boton, 200);
      await tester.tap(boton);
      await tester.pumpAndSettle();

      expect(api.presupuestosEnviados, ['os-1']);
    });

    testWidgets('con precios esperando a Gerencia está deshabilitado y lo explica', (tester) async {
      await montarOrden(tester, _orden(gerencia: EstadoGerencia.pendiente));

      final aviso = find.text('Se envía cuando Gerencia resuelva los precios.');
      await tester.scrollUntilVisible(aviso, 200);
      final boton = tester.widget<ButtonStyleButton>(
        find.ancestor(of: find.text('Enviar presupuesto al cliente'), matching: find.bySubtype<ButtonStyleButton>()),
      );
      expect(boton.onPressed, isNull);
    });

    testWidgets('el cliente no ve el botón', (tester) async {
      await montarOrden(tester, _orden(), sesion: SesionCliente.new);

      expect(find.text('Enviar presupuesto al cliente'), findsNothing);
    });
  });
}
