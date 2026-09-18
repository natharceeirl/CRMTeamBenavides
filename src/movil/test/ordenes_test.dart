import 'package:crm_team_benavides/api/estados.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:crm_team_benavides/formato.dart';
import 'package:flutter_test/flutter_test.dart';

/// Respuesta tal como la manda GET /api/ordenes-servicio/{id}.
Map<String, dynamic> ordenDePrueba({int estadoId = 1}) => {
      'id': '64404ca3-6464-486a-b0aa-14d157fa1375',
      'vehiculoId': 'v1',
      'vehiculoPlaca': 'TEST-001',
      'vehiculoMarca': 'Yamaha',
      'vehiculoModelo': 'MT-03',
      'vehiculoAnio': 2023,
      'vehiculoKilometraje': 1200,
      'vehiculoColor': null,
      'clienteId': 'c1',
      'clienteNombre': 'Luis Quispe',
      'clienteTelefono': '959 214 380',
      'clienteDocumentoIdentidad': '45872310',
      'tecnicoAsignadoId': null,
      'tecnicoNombre': null,
      'estado': 'Diagnostico',
      'estadoId': estadoId,
      'fechaApertura': '2026-09-18T13:45:00Z',
      'fechaCierre': null,
      'diagnostico': 'Pastillas gastadas',
      'observaciones': null,
      'activo': true,
      'detalles': [
        {
          'id': 'd1',
          'productoId': null,
          'productoCodigo': null,
          'descripcion': 'Cambio de pastillas',
          'cantidad': 1,
          'precioUnitario': 120.5,
          'subtotal': 120.5,
          'esRepuesto': false,
        },
        {
          'id': 'd2',
          'productoId': 'p1',
          'productoCodigo': 'REP-0001',
          'descripcion': 'Aceite Yamalube',
          'cantidad': 3,
          'precioUnitario': 42.0,
          'subtotal': 126.0,
          'esRepuesto': true,
        },
      ],
      'total': 246.5,
    };

void main() {
  group('estados de la orden', () {
    test('los números coinciden con el enum del backend', () {
      expect(EstadoOrden.abierta, 0);
      expect(EstadoOrden.diagnostico, 1);
      expect(EstadoOrden.entregada, 5);
      expect(EstadoOrden.cancelada, 6);
      expect(nombreEstadoOrden(EstadoOrden.enProceso), 'En proceso');
    });

    test('entregada y cancelada son terminales', () {
      expect(esEstadoTerminal(EstadoOrden.entregada), isTrue);
      expect(esEstadoTerminal(EstadoOrden.cancelada), isTrue);
      expect(esEstadoTerminal(EstadoOrden.lista), isFalse);
    });

    test('no deja diagnosticar una orden cerrada', () {
      // El backend responde 400 en ese caso; la app ni lo ofrece.
      expect(permiteDiagnostico(EstadoOrden.abierta), isTrue);
      expect(permiteDiagnostico(EstadoOrden.entregada), isFalse);
      expect(permiteDiagnostico(EstadoOrden.cancelada), isFalse);
    });

    test('un estado desconocido no rompe la pantalla', () {
      expect(nombreEstadoOrden(99), 'Desconocido');
    });
  });

  group('lectura de la orden que manda la API', () {
    test('arma la orden con su referencia y su unidad', () {
      final detalle = OrdenServicioDetalleApi.desdeJson(ordenDePrueba());

      expect(detalle.orden.referencia, '#64404CA3');
      expect(detalle.orden.unidad, 'Yamaha MT-03');
      expect(detalle.orden.clienteNombre, 'Luis Quispe');
      expect(detalle.clienteTelefono, '959 214 380');
      expect(detalle.vehiculoKilometraje, 1200);
    });

    test('separa repuestos de mano de obra y suma el total', () {
      final detalle = OrdenServicioDetalleApi.desdeJson(ordenDePrueba());

      expect(detalle.detalles, hasLength(2));
      expect(detalle.detalles.where((d) => d.esRepuesto), hasLength(1));
      expect(detalle.detalles.firstWhere((d) => d.esRepuesto).productoCodigo, 'REP-0001');
      expect(detalle.total, 246.5);
    });

    test('aguanta los campos nulos u omitidos', () {
      final crudo = ordenDePrueba()
        ..remove('detalles')
        ..remove('total')
        ..['tecnicoNombre'] = null;

      final detalle = OrdenServicioDetalleApi.desdeJson(crudo);

      expect(detalle.detalles, isEmpty);
      expect(detalle.total, 0);
      expect(detalle.orden.tecnicoNombre, isNull);
    });
  });

  group('formato', () {
    test('escribe los montos en soles', () {
      expect(soles(246.5), 'S/ 246.50');
    });

    test('muestra la fecha como dia/mes y hora de 24 horas', () {
      expect(fechaHora(DateTime.utc(2026, 9, 18, 13, 45)), matches(r'^\d{2}/\d{2} \d{2}:\d{2}$'));
    });

    test('sin fecha muestra una raya', () {
      expect(fechaHora(null), '—');
    });
  });
}
