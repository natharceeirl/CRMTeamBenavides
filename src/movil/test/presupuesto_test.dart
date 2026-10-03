import 'package:crm_team_benavides/api/estados.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('respuestas al presupuesto', () {
    test('mientras la orden está en su etapa se puede aprobar o rechazar', () {
      expect(
        respuestasPresupuesto(EstadoOrden.diagnostico, EstadoPresupuesto.pendiente),
        [EstadoPresupuesto.aprobado, EstadoPresupuesto.rechazado],
      );
      expect(respuestasPresupuesto(EstadoOrden.abierta, EstadoPresupuesto.pendiente), hasLength(2));
    });

    test('después de aprobado no se ofrece nada, ni para cambiar a rechazado', () {
      expect(respuestasPresupuesto(EstadoOrden.diagnostico, EstadoPresupuesto.aprobado), isEmpty);
    });

    test('rechazado y aún en diagnóstico, solo se puede aprobar', () {
      expect(
        respuestasPresupuesto(EstadoOrden.diagnostico, EstadoPresupuesto.rechazado),
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
        expect(respuestasPresupuesto(estado, EstadoPresupuesto.pendiente), isEmpty);
      }
    });
  });
}
