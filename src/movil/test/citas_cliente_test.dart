import 'package:crm_team_benavides/api/estados.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/auth/sesion.dart';
import 'package:crm_team_benavides/formato.dart';
import 'package:crm_team_benavides/pantallas/citas_cliente.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'pantalla_inicio_test.dart' show SesionCliente;

CitaApi cita(String id, DateTime fecha, String estado) => CitaApi(
      id: id,
      numeroCita: 'CT-$id',
      vehiculoId: 'v1',
      vehiculoPlaca: 'ABC-123',
      vehiculoModelo: 'YZF-R3',
      fechaHoraProgramada: fecha,
      duracionMinutos: 60,
      motivo: 'Mantenimiento de 5,000 km',
      estado: estado,
    );

void main() {
  test('lee la cita del listado', () {
    final leida = CitaApi.desdeJson({
      'id': 'c1',
      'numeroCita': 'CT-000004',
      'clienteId': 'k1',
      'clienteNombre': 'Luis Quispe',
      'vehiculoId': 'v1',
      'vehiculoPlaca': null,
      'vehiculoModelo': 'VX Cruiser',
      'fechaHoraProgramada': '2026-10-05T14:00:00Z',
      'duracionMinutos': 90,
      'motivo': 'Revisión',
      'estado': 'Confirmada',
      'estadoDescripcion': 'Confirmada',
    });
    expect(leida.unidad, 'VX Cruiser');
    expect(leida.estado, EstadoCita.confirmada);
    expect(leida.duracionMinutos, 90);
    expect(leida.fechaHoraProgramada, DateTime.utc(2026, 10, 5, 14));
  });

  test('separa las citas en curso de las cerradas, cada grupo en su orden', () {
    final (:proximas, :anteriores) = separarCitas([
      cita('a', DateTime.utc(2026, 10, 9, 14), EstadoCita.pendiente),
      cita('b', DateTime.utc(2026, 10, 5, 14), EstadoCita.confirmada),
      cita('c', DateTime.utc(2026, 9, 1, 14), EstadoCita.completada),
      cita('d', DateTime.utc(2026, 9, 20, 14), EstadoCita.cancelada),
    ]);
    expect(proximas.map((c) => c.id), ['b', 'a']);
    expect(anteriores.map((c) => c.id), ['d', 'c']);
  });

  test('pide unidad, día, hora y motivo, y no deja agendar en el pasado', () {
    final ahora = DateTime(2026, 10, 2, 10);
    final manana = DateTime(2026, 10, 3);
    const nueve = TimeOfDay(hour: 9, minute: 0);

    String? falta({String? vehiculoId = 'v1', DateTime? fecha, TimeOfDay? hora = nueve, String motivo = 'Revisión'}) =>
        faltaParaAgendar(vehiculoId: vehiculoId, fecha: fecha ?? manana, hora: hora, motivo: motivo, ahora: ahora);

    expect(falta(vehiculoId: null), 'Elige la unidad.');
    expect(falta(hora: null), 'Elige el día y la hora.');
    expect(falta(fecha: DateTime(2026, 10, 2)), 'La cita no puede quedar en el pasado.');
    expect(falta(motivo: '  '), 'Cuéntanos el motivo de la cita.');
    expect(falta(), isNull);
  });

  test('escribe la fecha de la cita con el día de la semana', () {
    expect(fechaConDia(DateTime(2026, 10, 5, 9)), 'Lun 05/10 · 09:00');
    expect(dia(DateTime(2026, 10, 5)), '05/10/2026');
  });

  testWidgets('el cliente ve sus citas y puede agendar una', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sesionProvider.overrideWith(SesionCliente.new),
          citasProvider.overrideWith(
            (ref) => [
              cita('a', DateTime(2026, 10, 5, 9), EstadoCita.pendiente),
              cita('b', DateTime(2026, 9, 1, 9), EstadoCita.completada),
            ],
          ),
          vehiculosProvider(null).overrideWith((ref) => []),
        ],
        child: const MaterialApp(home: Scaffold(body: PantallaCitasCliente())),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Próximas'), findsOneWidget);
    expect(find.text('Anteriores'), findsOneWidget);
    expect(find.text('Lun 05/10 · 09:00'), findsOneWidget);
    expect(find.text('Pendiente'), findsOneWidget);

    await tester.tap(find.text('Agendar cita'));
    await tester.pumpAndSettle();
    expect(find.text('No tienes unidades registradas. Pide al taller que las registre.'), findsOneWidget);
  });

  testWidgets('una cita pendiente se puede cancelar con motivo', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sesionProvider.overrideWith(SesionCliente.new),
          citasProvider.overrideWith((ref) => [cita('a', DateTime(2026, 10, 5, 9), EstadoCita.pendiente)]),
        ],
        child: const MaterialApp(home: Scaffold(body: PantallaCitasCliente())),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Lun 05/10 · 09:00'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(OutlinedButton, 'Cancelar cita'));
    await tester.pumpAndSettle();

    final confirmar = find.widgetWithText(FilledButton, 'Cancelar cita');
    expect(tester.widget<FilledButton>(confirmar).onPressed, isNull, reason: 'sin motivo no se cancela');
    await tester.enterText(find.byType(TextField), 'Viajo ese día');
    await tester.pump();
    expect(tester.widget<FilledButton>(confirmar).onPressed, isNotNull);
  });
}
