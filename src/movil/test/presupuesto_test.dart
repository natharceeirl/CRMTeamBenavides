import 'package:crm_team_benavides/api/estados.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('respuestas al presupuesto', () {
    test('con la orden en diagnóstico y el presupuesto armado se puede aprobar o rechazar', () {
      expect(
        respuestasPresupuesto(EstadoOrden.diagnostico, EstadoPresupuesto.pendiente, 120),
        [EstadoPresupuesto.aprobado, EstadoPresupuesto.rechazado],
      );
    });

    test('antes del diagnóstico o sin ítems no se responde', () {
      expect(respuestasPresupuesto(EstadoOrden.abierta, EstadoPresupuesto.pendiente, 120), isEmpty);
      expect(respuestasPresupuesto(EstadoOrden.diagnostico, EstadoPresupuesto.pendiente, 0), isEmpty);
    });

    test('después de aprobado no se ofrece nada, ni para cambiar a rechazado', () {
      expect(respuestasPresupuesto(EstadoOrden.diagnostico, EstadoPresupuesto.aprobado, 120), isEmpty);
    });

    test('rechazado y aún en diagnóstico, solo se puede aprobar', () {
      expect(
        respuestasPresupuesto(EstadoOrden.diagnostico, EstadoPresupuesto.rechazado, 120),
        [EstadoPresupuesto.aprobado],
      );
    });

    test('con la orden aprobada, en proceso, lista o cerrada no se responde', () {
      for (final estado in [
        EstadoOrden.aprobada,
        EstadoOrden.enProceso,
        EstadoOrden.lista,
        EstadoOrden.entregada,
        EstadoOrden.cancelada,
      ]) {
        expect(respuestasPresupuesto(estado, EstadoPresupuesto.pendiente, 120), isEmpty);
      }
    });
  });
}
