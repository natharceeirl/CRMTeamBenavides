import 'package:crm_team_benavides/api/estados.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/pantallas/portal_cliente.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('avance que ve el cliente', () {
    final ingreso = DateTime.utc(2026, 9, 24, 14, 15);
    final diagnostico = DateTime.utc(2026, 9, 24, 16, 40);
    final aprobada = DateTime.utc(2026, 9, 24, 20, 2);
    final estimada = DateTime.utc(2026, 9, 26, 17);

    test('marca lo hecho con su fecha, el paso actual y la entrega estimada', () {
      final pasos = pasosDeAvance(
        estadoId: EstadoOrden.aprobada,
        cambios: [
          (estado: EstadoOrden.diagnostico, fecha: diagnostico),
          (estado: EstadoOrden.aprobada, fecha: aprobada),
        ],
        fechaIngreso: ingreso,
        fechaEstimadaEntrega: estimada,
      );

      expect(pasos.map((paso) => paso.titulo), [
        'Recibida',
        'Diagnóstico',
        'Presupuesto aprobado',
        'En reparación',
        'Lista para recoger',
        'Entregada',
      ]);
      expect(pasos[0].situacion, SituacionPaso.hecho);
      expect(pasos[0].fecha, ingreso);
      expect(pasos[1].fecha, diagnostico);
      expect(pasos[2].situacion, SituacionPaso.actual);
      expect(pasos[3].situacion, SituacionPaso.pendiente);
      expect(pasos[3].fecha, isNull);
      expect(pasos[4].fecha, estimada);
      expect(pasos[4].estimada, isTrue);
    });

    test('en un reingreso «Lista» vuelve a quedar pendiente', () {
      final pasos = pasosDeAvance(
        estadoId: EstadoOrden.enProceso,
        cambios: [
          (estado: EstadoOrden.enProceso, fecha: DateTime.utc(2026, 9, 25, 13)),
          (estado: EstadoOrden.lista, fecha: DateTime.utc(2026, 9, 25, 18)),
          (estado: EstadoOrden.enProceso, fecha: DateTime.utc(2026, 9, 26, 9)),
        ],
      );

      expect(pasos[3].situacion, SituacionPaso.actual);
      expect(pasos[3].fecha, DateTime.utc(2026, 9, 26, 9));
      expect(pasos[4].situacion, SituacionPaso.pendiente);
    });
  });

  test('lee el resumen y los comprobantes del portal', () {
    final resumen = PortalResumenApi.desdeJson({
      'clienteNombre': 'Carlos Mamani',
      'cantidadUnidades': 3,
      'cantidadOrdenesActivas': 1,
      'cantidadPresupuestosPendientes': 1,
      'saldoPendienteTotal': 735.0,
    });
    expect(resumen.presupuestosPendientes, 1);
    expect(resumen.saldoPendiente, 735);

    final comprobante = ComprobantePortalApi.desdeJson({
      'id': 'c1',
      'tipo': 'Boleta',
      'serie': 'B001',
      'numero': '004512',
      'fecha': '2026-05-12T15:00:00Z',
      'total': 690.0,
      'estado': 'Emitido',
      'numeroOrden': 'OS-000087',
    });
    expect(comprobante.referencia, 'Boleta B001-004512');
    expect(comprobante.anulado, isFalse);
  });

  testWidgets('Documentos lista los comprobantes y marca el anulado', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          portalComprobantesProvider.overrideWith(
            (ref) => [
              ComprobantePortalApi(
                id: 'c1',
                tipo: 'Boleta',
                serie: 'B001',
                numero: '004512',
                fecha: DateTime.utc(2026, 5, 12, 15),
                total: 690,
                estado: 'Emitido',
                numeroOrden: 'OS-000087',
              ),
              ComprobantePortalApi(
                id: 'c2',
                tipo: 'Boleta',
                serie: 'B001',
                numero: '003977',
                fecha: DateTime.utc(2026, 3, 8, 15),
                total: 385,
                estado: 'Anulado',
              ),
            ],
          ),
        ],
        child: const MaterialApp(home: Scaffold(body: PantallaDocumentos())),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Boleta B001-004512'), findsOneWidget);
    expect(find.text('S/ 690.00'), findsOneWidget);
    expect(find.textContaining('Anulado'), findsOneWidget);
  });
}
