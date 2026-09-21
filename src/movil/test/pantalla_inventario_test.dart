import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/inventario.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

ProductoApi producto({
  required String id,
  required String codigo,
  required String nombre,
  int stockActual = 10,
  int stockMinimo = 2,
  bool esBajoStock = false,
  String categoriaId = 'cat-1',
  String categoriaNombre = 'Filtros',
}) =>
    ProductoApi(
      id: id,
      codigo: codigo,
      nombre: nombre,
      unidad: 'und',
      precioVenta: 45.5,
      stockActual: stockActual,
      stockMinimo: stockMinimo,
      esBajoStock: esBajoStock,
      categoriaId: categoriaId,
      categoriaNombre: categoriaNombre,
      activo: true,
    );

CategoriaProductoApi categoria(String id, String nombre) => CategoriaProductoApi(
      id: id,
      nombre: nombre,
      cantidadProductos: 3,
      activo: true,
    );

Future<void> montar(
  WidgetTester tester,
  List<ProductoApi> productos, {
  List<CategoriaProductoApi> categorias = const [],
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        productosProvider.overrideWith((ref) => productos),
        categoriasProductoProvider.overrideWith((ref) => categorias),
      ],
      child: const MaterialApp(home: Scaffold(body: PantallaInventario())),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('lista los repuestos con su código, categoría y stock',
      (tester) async {
    await montar(tester, [
      producto(
        id: '11111111-0000-0000-0000-000000000001',
        codigo: 'FIL-001',
        nombre: 'Filtro de aceite',
        stockActual: 12,
      ),
    ]);

    expect(find.text('Filtro de aceite'), findsOneWidget);
    expect(find.textContaining('FIL-001'), findsOneWidget);
    expect(find.textContaining('Filtros'), findsWidgets);
    expect(find.text('12 und'), findsOneWidget);
  });

  testWidgets('marca agotado, stock bajo y disponible', (tester) async {
    await montar(tester, [
      producto(
        id: '11111111-0000-0000-0000-000000000001',
        codigo: 'AGO-001',
        nombre: 'Bujía',
        stockActual: 0,
        esBajoStock: true,
      ),
      producto(
        id: '11111111-0000-0000-0000-000000000002',
        codigo: 'BAJ-002',
        nombre: 'Cadena',
        stockActual: 1,
        esBajoStock: true,
      ),
      producto(
        id: '11111111-0000-0000-0000-000000000003',
        codigo: 'OK-003',
        nombre: 'Llanta',
        stockActual: 20,
      ),
    ]);

    // Agotado manda sobre stock bajo: sin unidades no hay nada que vender.
    expect(find.text('Agotado'), findsOneWidget);
    expect(find.text('Stock bajo'), findsOneWidget);
    expect(find.text('Disponible'), findsOneWidget);
  });

  testWidgets('el filtro «solo bajo stock» esconde los que están bien',
      (tester) async {
    await montar(tester, [
      producto(
        id: '11111111-0000-0000-0000-000000000001',
        codigo: 'BAJ-001',
        nombre: 'Cadena',
        stockActual: 1,
        esBajoStock: true,
      ),
      producto(
        id: '11111111-0000-0000-0000-000000000002',
        codigo: 'OK-002',
        nombre: 'Llanta',
        stockActual: 20,
      ),
    ]);

    await tester.tap(find.text('Solo bajo stock'));
    await tester.pumpAndSettle();

    expect(find.text('Cadena'), findsOneWidget);
    expect(find.text('Llanta'), findsNothing);
  });

  testWidgets('la búsqueda filtra por código', (tester) async {
    await montar(tester, [
      producto(
        id: '11111111-0000-0000-0000-000000000001',
        codigo: 'FIL-001',
        nombre: 'Filtro de aceite',
      ),
      producto(
        id: '11111111-0000-0000-0000-000000000002',
        codigo: 'BUJ-002',
        nombre: 'Bujía',
      ),
    ]);

    await tester.enterText(find.byType(TextField), 'BUJ');
    await tester.pumpAndSettle();

    expect(find.text('Bujía'), findsOneWidget);
    expect(find.text('Filtro de aceite'), findsNothing);
  });

  testWidgets('elegir una categoría deja solo sus repuestos', (tester) async {
    await montar(
      tester,
      [
        producto(
          id: '11111111-0000-0000-0000-000000000001',
          codigo: 'FIL-001',
          nombre: 'Filtro de aceite',
          categoriaId: 'cat-1',
          categoriaNombre: 'Filtros',
        ),
        producto(
          id: '11111111-0000-0000-0000-000000000002',
          codigo: 'LLA-002',
          nombre: 'Llanta',
          categoriaId: 'cat-2',
          categoriaNombre: 'Llantas',
        ),
      ],
      categorias: [categoria('cat-1', 'Filtros'), categoria('cat-2', 'Llantas')],
    );

    await tester.tap(find.widgetWithText(ChoiceChip, 'Llantas'));
    await tester.pumpAndSettle();

    expect(find.text('Llanta'), findsOneWidget);
    expect(find.text('Filtro de aceite'), findsNothing);
  });

  testWidgets('sin resultados avisa en vez de quedarse en blanco',
      (tester) async {
    await montar(tester, [
      producto(
        id: '11111111-0000-0000-0000-000000000001',
        codigo: 'FIL-001',
        nombre: 'Filtro de aceite',
      ),
    ]);

    await tester.enterText(find.byType(TextField), 'no existe');
    await tester.pumpAndSettle();

    expect(find.textContaining('No hay repuestos'), findsOneWidget);
  });
}
