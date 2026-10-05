import 'package:crm_team_benavides/api/almacen_sesion.dart';
import 'package:crm_team_benavides/api/api_http.dart';
import 'package:crm_team_benavides/api/estados.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/agregar_item.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'pantalla_inicio_test.dart' show SesionAbierta, SesionTecnico;

const _ordenId = '64404ca3-0000-0000-0000-0000000000a1';

ProductoApi _producto(String id, String codigo, String nombre, {required int stock}) => ProductoApi(
      id: id,
      codigo: codigo,
      nombre: nombre,
      unidad: 'und',
      precioVenta: 45,
      stockActual: stock,
      stockMinimo: 4,
      esBajoStock: stock < 4,
      categoriaId: 'cat',
      categoriaNombre: 'Filtros',
      activo: true,
    );

/// Guarda lo que la hoja manda al backend en lugar de llamarlo.
class ApiFalsa extends ApiHttp {
  ApiFalsa() : super(AlmacenSesion());

  final enviados = <Map<String, Object?>>[];

  @override
  Future<void> agregarItem(
    String ordenId, {
    required int tipoItem,
    required int cantidad,
    String? productoId,
    String? servicioId,
    String? descripcion,
    double? precioUnitario,
    int? tipoAfectacionIgv,
  }) async {
    enviados.add({
      'ordenId': ordenId,
      'tipoItem': tipoItem,
      'cantidad': cantidad,
      'productoId': productoId,
      'servicioId': servicioId,
      'precioUnitario': precioUnitario,
    });
  }
}

/// Lo que devolvió la hoja al cerrarse.
bool? resultado;

Future<ApiFalsa> montarHoja(
  WidgetTester tester, {
  required SesionNotifier Function() sesion,
  List<ServicioApi> servicios = const [
    ServicioApi(id: 's1', nombre: 'Mantenimiento 1000 km', precioSugerido: 120, tipoAfectacionIgv: 0),
  ],
}) async {
  final api = ApiFalsa();
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        sesionProvider.overrideWith(sesion),
        apiProvider.overrideWithValue(api),
        productosProvider.overrideWith((ref) => [
              _producto('p1', 'FIL-001', 'Filtro de aceite', stock: 3),
              _producto('p2', 'BUJ-002', 'Bujía iridium', stock: 0),
            ]),
        serviciosProvider.overrideWith((ref) => servicios),
      ],
      child: MaterialApp(
        home: Scaffold(
          body: Builder(
            builder: (context) => TextButton(
              onPressed: () async => resultado = await abrirAgregarItem(context, _ordenId),
              child: const Text('Abrir'),
            ),
          ),
        ),
      ),
    ),
  );
  resultado = null;
  await tester.tap(find.text('Abrir'));
  await tester.pumpAndSettle();
  return api;
}

void main() {
  group('tipos de ítem', () {
    test('sin permiso de precios solo se agrega lo que tiene precio de catálogo', () {
      expect(tiposDeItemPermitidos(puedeFijarPrecios: false), [TipoItem.repuesto, TipoItem.servicio]);
      expect(tiposDeItemPermitidos(puedeFijarPrecios: true), hasLength(4));
    });

    test('igual que el backend, no se agregan ítems en Lista ni en órdenes cerradas', () {
      expect(permiteEditarItems(EstadoOrden.abierta), isTrue);
      expect(permiteEditarItems(EstadoOrden.enProceso), isTrue);
      expect(permiteEditarItems(EstadoOrden.lista), isFalse);
      expect(permiteEditarItems(EstadoOrden.entregada), isFalse);
      expect(permiteEditarItems(EstadoOrden.cancelada), isFalse);
      // Liquidada, la venta tiene que coincidir con la orden.
      expect(permiteEditarItems(EstadoOrden.enProceso, liquidada: true), isFalse);
    });
  });

  testWidgets('el técnico solo ve repuestos y servicios, con el precio del catálogo',
      (tester) async {
    await montarHoja(tester, sesion: SesionTecnico.new);

    expect(find.widgetWithText(ChoiceChip, 'Repuesto'), findsOneWidget);
    expect(find.widgetWithText(ChoiceChip, 'Servicio'), findsOneWidget);
    expect(find.widgetWithText(ChoiceChip, 'Mano de obra'), findsNothing);
    expect(find.widgetWithText(ChoiceChip, 'Terceros'), findsNothing);
    expect(find.text('S/ 45.00'), findsNWidgets(2));
    // No hay campo de precio que el técnico pueda tocar.
    expect(find.byType(TextField), findsOneWidget);
  });

  testWidgets('un repuesto sin stock no se puede elegir', (tester) async {
    await montarHoja(tester, sesion: SesionTecnico.new);

    final agotado = tester.widget<ListTile>(find.widgetWithText(ListTile, 'Bujía iridium'));
    expect(agotado.enabled, isFalse);
    expect(find.textContaining('sin stock'), findsOneWidget);
  });

  testWidgets('sin servicios cargados lo dice en vez de «sin coincidencias»', (tester) async {
    await montarHoja(tester, sesion: SesionTecnico.new, servicios: const []);

    await tester.tap(find.widgetWithText(ChoiceChip, 'Servicio'));
    await tester.pumpAndSettle();

    expect(find.text('Todavía no hay servicios en el catálogo.'), findsOneWidget);
  });

  testWidgets('agrega el repuesto sin mandar precio y no pasa del stock', (tester) async {
    final api = await montarHoja(tester, sesion: SesionTecnico.new);

    await tester.tap(find.text('Filtro de aceite'));
    await tester.pumpAndSettle();
    for (var i = 0; i < 5; i++) {
      await tester.tap(find.byTooltip('Más'));
      await tester.pump();
    }
    await tester.tap(find.text('Agregar'));
    await tester.pumpAndSettle();

    expect(resultado, isTrue);
    expect(api.enviados, [
      {
        'ordenId': _ordenId,
        'tipoItem': TipoItem.repuesto,
        'cantidad': 3,
        'productoId': 'p1',
        'servicioId': null,
        'precioUnitario': null,
      },
    ]);
  });

  testWidgets('Gerencia puede cargar mano de obra con precio propio', (tester) async {
    final api = await montarHoja(tester, sesion: SesionAbierta.new);

    await tester.tap(find.widgetWithText(ChoiceChip, 'Mano de obra'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Trabajo realizado'), 'Regulación de válvulas');
    await tester.enterText(find.widgetWithText(TextField, 'Precio unitario (S/)'), '80,50');
    await tester.pump();
    await tester.tap(find.text('Agregar'));
    await tester.pumpAndSettle();

    expect(api.enviados.single['tipoItem'], TipoItem.manoDeObra);
    expect(api.enviados.single['precioUnitario'], 80.5);
  });
}
