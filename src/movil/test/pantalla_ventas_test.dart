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
  double saldo = 0,
  String estadoPago = EstadoPago.pagado,
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
      saldo: saldo,
      totalPagado: total - saldo,
      estadoPago: estadoPago,
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
  testWidgets('una venta confirmada con saldo muestra cuánto se debe', (tester) async {
    await montar(tester, [
      venta(
        id: '22222222-0000-0000-0000-000000000009',
        estadoId: EstadoVenta.confirmada,
        total: 49.56,
        saldo: 29.56,
        estadoPago: EstadoPago.parcial,
      ),
    ]);

    expect(find.text('Parcial'), findsOneWidget);
    expect(find.text('Debe S/ 29.56'), findsOneWidget);
  });

  testWidgets('una cotización no se cobra: sin estado de pago ni saldo', (tester) async {
    await montar(tester, [
      venta(
        id: '22222222-0000-0000-0000-000000000010',
        estadoId: EstadoVenta.cotizacion,
        saldo: 250,
        estadoPago: EstadoPago.pendiente,
      ),
    ]);

    expect(find.text('Pendiente'), findsNothing);
    expect(find.textContaining('Debe'), findsNothing);
  });

  test('lee los pagos, el saldo y el importe con IGV del detalle', () {
    final detalle = VentaDetalleApi.desdeJson({
      'id': '22222222-0000-0000-0000-000000000011',
      'clienteNombre': 'Luis Quispe',
      'estadoId': EstadoVenta.confirmada,
      'fecha': '2026-09-30T17:41:00Z',
      'total': 49.56,
      'montoIgv': 7.56,
      'totalPagado': 20,
      'saldo': 29.56,
      'estadoPago': 'Parcial',
      'detalles': [
        {'id': 'd1', 'productoCodigo': null, 'productoNombre': 'Mano de obra', 'cantidad': 1, 'precioUnitario': 42, 'subtotal': 42, 'total': 49.56},
      ],
      'pagos': [
        {'id': 'p1', 'monto': 20, 'metodoPagoNombre': 'Efectivo', 'fecha': '2026-09-30T17:40:00Z', 'esAnticipo': true},
      ],
    });

    expect(detalle.venta.saldo, 29.56);
    expect(detalle.venta.estadoPago, EstadoPago.parcial);
    expect(detalle.detalles.single.productoCodigo, '');
    expect(detalle.detalles.single.importe, 49.56);
    expect(detalle.pagos.single.esAnticipo, isTrue);
  });

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
