import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/inicio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

/// Sesión ya resuelta: sin esto el inicio se queda en el indicador de carga y
/// además `SesionNotifier.build` iría a buscar la sesión guardada al
/// dispositivo, que en un test no existe.
class SesionAbierta extends SesionNotifier {
  @override
  EstadoSesion build() => const EstadoSesion(
        usuario: UsuarioSesion(
          id: '00000000-0000-0000-0000-000000000001',
          email: 'prueba@teambenavides.pe',
          nombre: 'Santiago',
        ),
        roles: ['Administrador'],
      );
}

Future<void> montarInicio(WidgetTester tester) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        sesionProvider.overrideWith(SesionAbierta.new),
        // Cada pestaña pide sus datos apenas se construye: el IndexedStack las
        // arma todas de una.
        resumenProvider.overrideWith(
          (ref) => ResumenDashboardApi.desdeJson(const {
            'ordenesServicio': <String, dynamic>{},
            'ventas': <String, dynamic>{},
            'inventario': <String, dynamic>{},
            'crmActivos': <String, dynamic>{},
          }),
        ),
        ordenesProvider.overrideWith((ref) => []),
        productosProvider.overrideWith((ref) => []),
        categoriasProductoProvider.overrideWith((ref) => []),
        ventasProvider.overrideWith((ref) => []),
        clientesProvider.overrideWith((ref) => []),
        vehiculosProvider(null).overrideWith((ref) => []),
      ],
      child: const MaterialApp(home: PantallaInicio()),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('la barra inferior tiene las cinco pestañas, con Tienda',
      (tester) async {
    await montarInicio(tester);

    final barra = find.byType(NavigationBar);
    expect(barra, findsOneWidget);

    for (final destino in ['Tablero', 'Órdenes', 'Tienda', 'Clientes', 'Unidades']) {
      expect(
        find.descendant(of: barra, matching: find.text(destino)),
        findsOneWidget,
        reason: 'falta el destino «$destino» en la barra inferior',
      );
    }

    expect(tester.widget<NavigationBar>(barra).destinations, hasLength(5));
  });

  testWidgets('Tienda abre en Repuestos y cambia a Ventas', (tester) async {
    await montarInicio(tester);

    await tester.tap(find.descendant(
      of: find.byType(NavigationBar),
      matching: find.text('Tienda'),
    ));
    await tester.pumpAndSettle();

    // El segmentado de la tienda, no la barra inferior.
    expect(find.widgetWithText(SegmentedButton<Object?>, 'Repuestos'), findsNothing);
    expect(find.text('Repuestos'), findsOneWidget);
    expect(find.text('Ventas'), findsOneWidget);
    expect(find.text('Buscar por código, nombre o categoría'), findsOneWidget);

    await tester.tap(find.text('Ventas'));
    await tester.pumpAndSettle();

    expect(find.text('Buscar por cliente o referencia'), findsOneWidget);
  });

  testWidgets('el título de la barra superior sigue a la pestaña', (tester) async {
    await montarInicio(tester);

    expect(find.descendant(of: find.byType(AppBar), matching: find.text('Tablero')),
        findsOneWidget);

    await tester.tap(find.descendant(
      of: find.byType(NavigationBar),
      matching: find.text('Tienda'),
    ));
    await tester.pumpAndSettle();

    expect(
      find.descendant(of: find.byType(AppBar), matching: find.text('Tienda')),
      findsOneWidget,
    );
  });
}
