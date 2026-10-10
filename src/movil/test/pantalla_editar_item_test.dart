import 'package:crm_team_benavides/api/almacen_sesion.dart';
import 'package:crm_team_benavides/api/api_http.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/editar_item.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'pantalla_inicio_test.dart' show SesionTecnico;

const _ordenId = '64404ca3-0000-0000-0000-0000000000b2';

const _detalle = DetalleServicioApi(
  id: 'd1',
  descripcion: 'Filtro de aceite',
  cantidad: 1,
  precioUnitario: 45,
  subtotal: 45,
  esRepuesto: true,
  productoId: 'p1',
  productoCodigo: 'FIL-001',
);

/// Guarda lo que la hoja manda al backend en lugar de llamarlo.
class ApiFalsa extends ApiHttp {
  ApiFalsa() : super(AlmacenSesion());

  final actualizados = <Map<String, Object?>>[];
  final quitados = <String>[];

  @override
  Future<void> actualizarItem(String ordenId, String detalleId, {int? cantidad, double? precioUnitario}) async {
    actualizados.add({'detalleId': detalleId, 'cantidad': cantidad, 'precioUnitario': precioUnitario});
  }

  @override
  Future<void> quitarItem(String ordenId, String detalleId) async => quitados.add(detalleId);
}

Future<ApiFalsa> montarHoja(WidgetTester tester) async {
  final api = ApiFalsa();
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        sesionProvider.overrideWith(SesionTecnico.new),
        apiProvider.overrideWithValue(api),
        productosProvider.overrideWith((ref) => const [
              ProductoApi(
                id: 'p1',
                codigo: 'FIL-001',
                nombre: 'Filtro de aceite',
                unidad: 'und',
                precioVenta: 45,
                stockActual: 10,
                stockMinimo: 4,
                esBajoStock: false,
                categoriaId: 'cat',
                categoriaNombre: 'Filtros',
                activo: true,
              ),
            ]),
        serviciosProvider.overrideWith((ref) => const <ServicioApi>[]),
      ],
      child: MaterialApp(
        home: Scaffold(
          body: Builder(
            builder: (context) => TextButton(
              onPressed: () => abrirEditarItem(context, _ordenId, _detalle),
              child: const Text('Abrir'),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('Abrir'));
  await tester.pumpAndSettle();
  return api;
}

void main() {
  testWidgets('el técnico cambia el precio con aviso y solo se manda el precio', (tester) async {
    final api = await montarHoja(tester);

    expect(find.text('De lista: S/ 45.00'), findsOneWidget);
    expect(find.textContaining('hasta que Gerencia'), findsNothing);

    await tester.enterText(find.widgetWithText(TextField, 'Precio unitario (S/)'), '50');
    await tester.pump();
    expect(find.textContaining('hasta que Gerencia lo apruebe'), findsOneWidget);

    await tester.tap(find.text('Guardar'));
    await tester.pumpAndSettle();
    expect(api.actualizados, [
      {'detalleId': 'd1', 'cantidad': null, 'precioUnitario': 50.0},
    ]);
  });

  testWidgets('corregir solo la cantidad no manda precio', (tester) async {
    final api = await montarHoja(tester);

    await tester.tap(find.byTooltip('Más'));
    await tester.pump();
    await tester.tap(find.text('Guardar'));
    await tester.pumpAndSettle();

    expect(api.actualizados, [
      {'detalleId': 'd1', 'cantidad': 2, 'precioUnitario': null},
    ]);
  });

  testWidgets('quitar pide confirmación antes de llamar al backend', (tester) async {
    final api = await montarHoja(tester);

    await tester.tap(find.text('Quitar'));
    await tester.pumpAndSettle();
    expect(find.text('El repuesto vuelve al stock.'), findsOneWidget);
    expect(api.quitados, isEmpty);

    await tester.tap(find.widgetWithText(FilledButton, 'Quitar'));
    await tester.pumpAndSettle();
    expect(api.quitados, ['d1']);
  });
}
