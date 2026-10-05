import 'package:crm_team_benavides/api/estados.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/agenda.dart';
import 'package:crm_team_benavides/pantallas/inicio.dart';
import 'package:crm_team_benavides/pantallas/portal_cliente.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'citas_cliente_test.dart' show cita;
import 'pantalla_inicio_test.dart' show SesionCliente;

/// Recepción ve la agenda y cambia el estado de las citas (RolSeeder).
class SesionRecepcion extends SesionNotifier {
  @override
  EstadoSesion build() => const EstadoSesion(
        usuario: UsuarioSesion(id: 'r1', email: 'recepcion@teambenavides.pe', nombre: 'Recepción'),
        roles: ['Recepcion'],
        permisos: ['citas.ver', 'citas.crear', 'citas.editar', 'citas.cancelar'],
      );
}

/// /api/auth/me falló: hay usuario del token, pero ni roles ni permisos.
class SesionSinPermisos extends SesionNotifier {
  @override
  EstadoSesion build() => const EstadoSesion(
        usuario: UsuarioSesion(id: 'u1', email: 'alguien@teambenavides.pe', nombre: 'Alguien'),
        errorPermisos: 'No se pudo conectar con la API.',
      );
}

void main() {
  group('agenda del personal', () {
    test('agrupa por día y nombra hoy y mañana', () {
      final hoy = DateTime(2026, 10, 5, 8);
      final grupos = citasPorDia([
        cita('b', DateTime(2026, 10, 5, 15), EstadoCita.pendiente),
        cita('c', DateTime(2026, 10, 7, 9), EstadoCita.confirmada),
        cita('a', DateTime(2026, 10, 5, 9), EstadoCita.confirmada),
      ]);
      expect(grupos, hasLength(2));
      expect(grupos.first.citas.map((c) => c.id), ['a', 'b']);
      expect(tituloDia(grupos.first.dia, hoy), 'Hoy');
      expect(tituloDia(DateTime(2026, 10, 6), hoy), 'Mañana');
      expect(tituloDia(grupos.last.dia, hoy), 'Mié 07/10');
    });

    test('sigue las transiciones del backend', () {
      expect(siguientesEstadosCita(EstadoCita.pendiente), [EstadoCita.confirmada, EstadoCita.enTaller, EstadoCita.noAsistio]);
      expect(siguientesEstadosCita(EstadoCita.enTaller), [EstadoCita.completada]);
      expect(siguientesEstadosCita(EstadoCita.cancelada), isEmpty);
    });

    testWidgets('recepción confirma desde la hoja de la cita', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sesionProvider.overrideWith(SesionRecepcion.new),
            agendaProvider.overrideWith((ref) => [cita('a', DateTime.now(), EstadoCita.pendiente)]),
          ],
          child: const MaterialApp(home: PantallaAgenda()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Hoy'), findsOneWidget);
      expect(find.text('Luis Quispe'), findsOneWidget);
      await tester.tap(find.text('Luis Quispe'));
      await tester.pumpAndSettle();
      expect(find.widgetWithText(FilledButton, 'Confirmar'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'No asistió'), findsOneWidget);
    });
  });

  group('pedidos de Lima del cliente', () {
    final llegado = PedidoLimaApi.desdeJson({
      'id': 'p1',
      'numeroPedido': 'PL-000007',
      'fecha': '2026-10-01T15:00:00Z',
      'estado': 'Recibido',
      'total': 49.56,
      'fechaEstimadaLlegada': null,
      'detalles': [
        {'cantidad': 2, 'productoNombre': 'Filtro de aceite'},
      ],
    });

    test('lee el pedido con sus repuestos', () {
      expect(llegado.estado, EstadoPedidoLima.recibido);
      expect(llegado.repuestos, ['2 × Filtro de aceite']);
      expect(esPedidoFinal(EstadoPedidoLima.entregado), isTrue);
    });

    testWidgets('el inicio del cliente avisa que el pedido ya llegó', (tester) async {
      final entregado = PedidoLimaApi(
        id: 'p2',
        numeroPedido: 'PL-000003',
        fecha: DateTime.utc(2026, 9, 1),
        estado: EstadoPedidoLima.entregado,
        total: 10,
        repuestos: const ['1 × Bujía'],
      );
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sesionProvider.overrideWith(SesionCliente.new),
            portalResumenProvider.overrideWith(
              (ref) => const PortalResumenApi(
                clienteNombre: 'Luis Quispe',
                unidades: 0,
                ordenesActivas: 0,
                presupuestosPendientes: 0,
                saldoPendiente: 0,
              ),
            ),
            ordenesProvider.overrideWith((ref) => []),
            vehiculosProvider(null).overrideWith((ref) => []),
            pedidosLimaProvider.overrideWith((ref) => [llegado, entregado]),
          ],
          child: const MaterialApp(home: Scaffold(body: PantallaInicioCliente())),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Repuestos encargados'), findsOneWidget);
      expect(find.text('PL-000007'), findsOneWidget);
      expect(find.text('Ya llegó: puedes recogerlo en el taller'), findsOneWidget);
      expect(find.text('PL-000003'), findsNothing, reason: 'el entregado ya no se muestra');
    });
  });

  testWidgets('si /api/auth/me falla, lo dice y deja reintentar', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [sesionProvider.overrideWith(SesionSinPermisos.new)],
        child: const MaterialApp(home: PantallaInicio()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('No se pudieron cargar tus permisos: No se pudo conectar con la API.'), findsOneWidget);
    expect(find.text('Reintentar'), findsOneWidget);
    expect(find.textContaining('no tiene pantallas asignadas'), findsNothing);
  });
}
