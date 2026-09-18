import 'package:crm_team_benavides/api/estados.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/ordenes.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

OrdenServicioApi orden({
  required String id,
  required String placa,
  required int estadoId,
  String cliente = 'Luis Quispe',
}) =>
    OrdenServicioApi(
      id: id,
      vehiculoPlaca: placa,
      vehiculoMarca: 'Yamaha',
      vehiculoModelo: 'MT-03',
      clienteId: 'c1',
      clienteNombre: cliente,
      estado: nombreEstadoOrden(estadoId),
      estadoId: estadoId,
      fechaApertura: DateTime.utc(2026, 9, 18, 13, 45),
    );

Future<void> montar(WidgetTester tester, List<OrdenServicioApi> ordenes) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [ordenesProvider.overrideWith((ref) => ordenes)],
      child: const MaterialApp(home: Scaffold(body: PantallaOrdenes())),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('lista las órdenes con su placa, cliente y estado', (tester) async {
    await montar(tester, [
      orden(id: '64404ca3-0000-0000-0000-000000000001', placa: 'TEST-001', estadoId: EstadoOrden.diagnostico),
    ]);

    expect(find.textContaining('TEST-001'), findsOneWidget);
    expect(find.text('Luis Quispe'), findsOneWidget);
    expect(find.text('Diagnóstico'), findsOneWidget);
  });

  testWidgets('el filtro «solo en taller» esconde las entregadas', (tester) async {
    await montar(tester, [
      orden(id: '64404ca3-0000-0000-0000-000000000001', placa: 'EN-TALLER', estadoId: EstadoOrden.enProceso),
      orden(id: '64404ca3-0000-0000-0000-000000000002', placa: 'ENTREGADA', estadoId: EstadoOrden.entregada),
    ]);

    expect(find.textContaining('EN-TALLER'), findsOneWidget);
    expect(find.textContaining('ENTREGADA'), findsNothing);

    // Al soltar el filtro aparecen todas.
    await tester.tap(find.text('Solo en taller'));
    await tester.pumpAndSettle();

    expect(find.textContaining('EN-TALLER'), findsOneWidget);
    expect(find.textContaining('ENTREGADA'), findsOneWidget);
  });

  testWidgets('la búsqueda filtra por placa', (tester) async {
    await montar(tester, [
      orden(id: '64404ca3-0000-0000-0000-000000000001', placa: 'AAA-111', estadoId: EstadoOrden.abierta),
      orden(
        id: '64404ca3-0000-0000-0000-000000000002',
        placa: 'BBB-222',
        estadoId: EstadoOrden.abierta,
        cliente: 'Carla Zegarra',
      ),
    ]);

    await tester.enterText(find.byType(TextField), 'BBB');
    await tester.pumpAndSettle();

    expect(find.textContaining('BBB-222'), findsOneWidget);
    expect(find.textContaining('AAA-111'), findsNothing);
  });

  testWidgets('sin resultados avisa en vez de quedarse en blanco', (tester) async {
    await montar(tester, [
      orden(id: '64404ca3-0000-0000-0000-000000000001', placa: 'AAA-111', estadoId: EstadoOrden.abierta),
    ]);

    await tester.enterText(find.byType(TextField), 'no existe');
    await tester.pumpAndSettle();

    expect(find.textContaining('No hay órdenes'), findsOneWidget);
  });
}
