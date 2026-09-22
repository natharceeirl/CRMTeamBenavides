import 'package:crm_team_benavides/api/estados.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/ventas.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

VentaApi venta({
  required String id,
  required int estadoId,
  String cliente = 'Luis Quispe',
  double total = 250,
  int items = 2,
}) =>
    VentaApi(
      id: id,
      clienteId: 'c1',
      clienteNombre: cliente,
      estado: nombreEstadoVenta(estadoId),
      estadoId: estadoId,
      fecha: DateTime.utc(2026, 9, 21, 15, 30),
      total: total,
      cantidadItems: items,
      activo: true,
    );

Future<void> montar(WidgetTester tester, List<VentaApi> ventas) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [ventasProvider.overrideWith((ref) => ventas)],
      child: const MaterialApp(home: Scaffold(body: PantallaVentas())),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('lista las ventas con su cliente, estado y total', (tester) async {
    await montar(tester, [
      venta(
        id: '22222222-0000-0000-0000-000000000001',
        estadoId: EstadoVenta.confirmada,
        total: 450.5,
      ),
    ]);

    expect(find.text('Luis Quispe'), findsOneWidget);
    expect(find.text('S/ 450.50'), findsOneWidget);

    // «Confirmada» también es un chip del filtro: se busca solo la etiqueta
    // de la fila.
    expect(
      find.descendant(
        of: find.byType(EtiquetaEstadoVenta),
        matching: find.text('Confirmada'),
      ),
      findsOneWidget,
    );
  });

  testWidgets('el total del resumen no cuenta las anuladas', (tester) async {
    await montar(tester, [
      venta(
        id: '22222222-0000-0000-0000-000000000001',
        estadoId: EstadoVenta.confirmada,
        total: 300,
      ),
      venta(
        id: '22222222-0000-0000-0000-000000000002',
        estadoId: EstadoVenta.cotizacion,
        total: 200,
        cliente: 'Carla Zegarra',
      ),
      venta(
        id: '22222222-0000-0000-0000-000000000003',
        estadoId: EstadoVenta.anulada,
        total: 999,
        cliente: 'Marco Pinto',
      ),
    ]);

    expect(find.text('3 ventas'), findsOneWidget);
    expect(find.text('S/ 500.00 vigentes'), findsOneWidget);
  });

  testWidgets('el filtro por estado deja solo las cotizaciones', (tester) async {
    await montar(tester, [
      venta(
        id: '22222222-0000-0000-0000-000000000001',
        estadoId: EstadoVenta.confirmada,
      ),
      venta(
        id: '22222222-0000-0000-0000-000000000002',
        estadoId: EstadoVenta.cotizacion,
        cliente: 'Carla Zegarra',
      ),
    ]);

    await tester.tap(find.widgetWithText(ChoiceChip, 'Cotización'));
    await tester.pumpAndSettle();

    expect(find.text('Carla Zegarra'), findsOneWidget);
    expect(find.text('Luis Quispe'), findsNothing);
  });

  testWidgets('la búsqueda filtra por cliente', (tester) async {
    await montar(tester, [
      venta(
        id: '22222222-0000-0000-0000-000000000001',
        estadoId: EstadoVenta.confirmada,
      ),
      venta(
        id: '22222222-0000-0000-0000-000000000002',
        estadoId: EstadoVenta.confirmada,
        cliente: 'Carla Zegarra',
      ),
    ]);

    await tester.enterText(find.byType(TextField), 'Zegarra');
    await tester.pumpAndSettle();

    expect(find.text('Carla Zegarra'), findsOneWidget);
    expect(find.text('Luis Quispe'), findsNothing);
  });

  testWidgets('una sola venta se escribe en singular', (tester) async {
    await montar(tester, [
      venta(
        id: '22222222-0000-0000-0000-000000000001',
        estadoId: EstadoVenta.confirmada,
        items: 1,
      ),
    ]);

    expect(find.text('1 venta'), findsOneWidget);
    expect(find.textContaining('1 ítem'), findsOneWidget);
  });

  testWidgets('sin resultados avisa en vez de quedarse en blanco',
      (tester) async {
    await montar(tester, [
      venta(
        id: '22222222-0000-0000-0000-000000000001',
        estadoId: EstadoVenta.confirmada,
      ),
    ]);

    await tester.enterText(find.byType(TextField), 'no existe');
    await tester.pumpAndSettle();

    expect(find.textContaining('No hay ventas'), findsOneWidget);
  });
}
