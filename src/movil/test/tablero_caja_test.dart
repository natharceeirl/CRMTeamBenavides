import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/tablero.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'pantalla_inicio_test.dart' show SesionAbierta, SesionTecnico;
import 'tablero_test.dart' show resumenDePrueba;

/// El vendedor ve el tablero (reportes operativos) pero no los montos.
class SesionVendedor extends SesionNotifier {
  @override
  EstadoSesion build() => const EstadoSesion(
        usuario: UsuarioSesion(id: 'v1', email: 'vendedor@teambenavides.pe', nombre: 'Vendedor'),
        roles: ['Vendedor'],
        permisos: ['ventas.ver', 'ventas.crear', 'inventario.ver', 'reportes.ver_operativos'],
      );
}

Future<void> montarTablero(
  WidgetTester tester, {
  required SesionNotifier Function() sesion,
  CajaActualApi caja = const CajaActualApi(abierta: true, saldo: 275),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        sesionProvider.overrideWith(sesion),
        resumenProvider.overrideWith((ref) => ResumenDashboardApi.desdeJson(resumenDePrueba())),
        cajaActualProvider.overrideWith((ref) => caja),
      ],
      child: const MaterialApp(home: Scaffold(body: PantallaTablero())),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  test('lee si hay caja abierta y su saldo', () {
    final caja = CajaActualApi.desdeJson({
      'tieneCajaAbierta': true,
      'caja': {'saldoCalculado': 275.0, 'fechaApertura': '2026-09-30T17:48:00Z'},
    });
    expect(caja.abierta, isTrue);
    expect(caja.saldo, 275);

    expect(CajaActualApi.desdeJson({'tieneCajaAbierta': false, 'caja': null}).abierta, isFalse);
  });

  testWidgets('Gerencia ve el saldo de la caja chica en el tablero', (tester) async {
    await montarTablero(tester, sesion: SesionAbierta.new);

    expect(find.text('CAJA CHICA'), findsOneWidget);
    expect(find.text('S/ 275.00'), findsOneWidget);
  });

  testWidgets('sin caja abierta lo dice', (tester) async {
    await montarTablero(tester, sesion: SesionAbierta.new, caja: const CajaActualApi(abierta: false));

    expect(find.text('Cerrada'), findsOneWidget);
  });

  testWidgets('Gerencia ve cuánto se vendió', (tester) async {
    await montarTablero(tester, sesion: SesionAbierta.new);
    expect(find.text('VENDIDO'), findsOneWidget);
    expect(find.text('S/ 294.50'), findsOneWidget);
  });

  testWidgets('sin reportes financieros se ve cuántas ventas hubo, no el monto', (tester) async {
    await montarTablero(tester, sesion: SesionVendedor.new);
    expect(find.text('VENDIDO'), findsNothing);
    expect(find.text('S/ 294.50'), findsNothing);
    expect(find.text('VENTAS'), findsOneWidget);
  });

  testWidgets('sin caja.consultar no aparece la caja', (tester) async {
    await montarTablero(tester, sesion: SesionTecnico.new);

    expect(find.text('CAJA CHICA'), findsNothing);
  });
}
