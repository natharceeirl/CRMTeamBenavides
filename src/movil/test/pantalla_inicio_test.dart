import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/inicio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

const _usuario = UsuarioSesion(
  id: '00000000-0000-0000-0000-000000000001',
  email: 'prueba@teambenavides.pe',
  nombre: 'Santiago',
);

/// Sesión ya resuelta: sin esto el inicio se queda en el indicador de carga y
/// además `SesionNotifier.build` iría a buscar la sesión guardada al
/// dispositivo, que en un test no existe. Gerencia ve todo sin listar permisos.
class SesionAbierta extends SesionNotifier {
  @override
  EstadoSesion build() => const EstadoSesion(usuario: _usuario, roles: ['Gerencia/Admin']);
}

/// Los permisos que da el backend al técnico (RolSeeder).
class SesionTecnico extends SesionNotifier {
  @override
  EstadoSesion build() => const EstadoSesion(
        usuario: _usuario,
        roles: ['Tecnico'],
        permisos: [
          'ordenes.ver_asignadas',
          'ordenes.diagnostico',
          'ordenes.agregar_items',
          'ordenes.cambiar_estado',
          'unidades.ver',
          'inventario.ver',
          'servicios.ver',
        ],
      );
}

/// Los permisos que da el backend al cliente (RolSeeder).
class SesionCliente extends SesionNotifier {
  @override
  EstadoSesion build() => const EstadoSesion(
        usuario: _usuario,
        roles: ['Cliente'],
        permisos: [
          'portal.acceso',
          'clientes.ver',
          'unidades.ver',
          'ordenes.ver_asignadas',
          'citas.ver',
          'citas.crear',
          'citas.cancelar',
          'pedidos_lima.ver',
        ],
        clienteId: '00000000-0000-0000-0000-0000000000c1',
      );
}

Future<void> montarInicio(
  WidgetTester tester, {
  SesionNotifier Function() sesion = SesionAbierta.new,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        sesionProvider.overrideWith(sesion),
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
        portalResumenProvider.overrideWith(
          (ref) => const PortalResumenApi(
            clienteNombre: 'Luis Quispe',
            unidades: 0,
            ordenesActivas: 0,
            presupuestosPendientes: 0,
            saldoPendiente: 0,
          ),
        ),
        portalComprobantesProvider.overrideWith((ref) => []),
        citasProvider.overrideWith((ref) => []),
        pedidosLimaProvider.overrideWith((ref) => []),
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

  testWidgets('el técnico solo ve sus órdenes, la tienda y las unidades', (tester) async {
    await montarInicio(tester, sesion: SesionTecnico.new);

    final barra = find.byType(NavigationBar);
    expect(tester.widget<NavigationBar>(barra).destinations, hasLength(3));
    for (final destino in ['Órdenes', 'Tienda', 'Unidades']) {
      expect(find.descendant(of: barra, matching: find.text(destino)), findsOneWidget);
    }
    for (final oculto in ['Tablero', 'Clientes']) {
      expect(find.descendant(of: barra, matching: find.text(oculto)), findsNothing);
    }

    // Sin permiso de ventas, la tienda abre directo en repuestos, sin segmentado.
    expect(find.descendant(of: find.byType(AppBar), matching: find.text('Mis órdenes')),
        findsOneWidget);
  });

  testWidgets('el cliente tiene su app: inicio, sus órdenes, sus citas y sus documentos', (tester) async {
    await montarInicio(tester, sesion: SesionCliente.new);

    final barra = find.byType(NavigationBar);
    expect(tester.widget<NavigationBar>(barra).destinations, hasLength(4));
    for (final destino in ['Inicio', 'Órdenes', 'Citas', 'Documentos']) {
      expect(find.descendant(of: barra, matching: find.text(destino)), findsOneWidget);
    }
    expect(find.text('Hola, Luis'), findsOneWidget);
    expect(find.descendant(of: barra, matching: find.text('Clientes')), findsNothing);
    expect(find.descendant(of: barra, matching: find.text('Tienda')), findsNothing);
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
