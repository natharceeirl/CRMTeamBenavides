import 'package:crm_team_benavides/api/modelos.dart';
import 'package:flutter_test/flutter_test.dart';

/// Respuesta tal como la manda GET /api/dashboard/resumen.
Map<String, dynamic> resumenDePrueba() => {
      'fechaDesde': null,
      'fechaHasta': null,
      'ordenesServicio': {
        'abierta': 1,
        'diagnostico': 2,
        'aprobada': 3,
        'enProceso': 4,
        'lista': 5,
        'entregada': 6,
        'cancelada': 7,
        'total': 28,
      },
      'ventas': {
        'confirmadas': 2,
        'cotizaciones': 1,
        'anuladas': 1,
        'montoConfirmadas': 294.5,
        'ticketPromedio': 147.25,
      },
      'inventario': {
        'productosConStockBajo': 3,
        'productosSinStock': 1,
        'valorEstimadoInventario': 630.0,
      },
      'crmActivos': {'clientesActivos': 9, 'vehiculosActivos': 11},
    };

void main() {
  group('resumen del tablero', () {
    test('suma como «en taller» todo lo que no está entregado ni anulado', () {
      final resumen = ResumenDashboardApi.desdeJson(resumenDePrueba());

      // 1 + 2 + 3 + 4 + 5, sin contar las 6 entregadas ni las 7 anuladas.
      expect(resumen.enTaller, 15);
      expect(resumen.entregadas, 6);
      expect(resumen.totalOrdenes, 28);
    });

    test('lee ventas, inventario y clientes', () {
      final resumen = ResumenDashboardApi.desdeJson(resumenDePrueba());

      expect(resumen.ventasConfirmadas, 2);
      expect(resumen.montoVentas, 294.5);
      expect(resumen.productosStockBajo, 3);
      expect(resumen.clientesActivos, 9);
    });

    test('con secciones vacías no revienta: todo en cero', () {
      final resumen = ResumenDashboardApi.desdeJson({});

      expect(resumen.enTaller, 0);
      expect(resumen.montoVentas, 0);
      expect(resumen.clientesActivos, 0);
    });
  });

  group('respuesta del chatbot', () {
    test('lee el mensaje, la consulta y las sugerencias', () {
      final respuesta = RespuestaChatbotApi.desdeJson({
        'resueltoPorFaq': false,
        'faq': null,
        'sugerencias': [
          {
            'id': 'f1',
            'categoria': 'Horarios',
            'pregunta': '¿Cuál es el horario?',
            'respuesta': 'De 8 a 18.',
            'palabrasClave': 'horario',
            'orden': 1,
            'vecesConsultada': 3,
            'activo': true,
          },
        ],
        'requiereAgente': true,
        'consultaId': 'c1',
        'mensajeRespuesta': 'No encontré una respuesta exacta.',
      });

      expect(respuesta.resueltoPorFaq, isFalse);
      expect(respuesta.requiereAgente, isTrue);
      expect(respuesta.consultaId, 'c1');
      expect(respuesta.sugerencias, hasLength(1));
      expect(respuesta.sugerencias.first.pregunta, '¿Cuál es el horario?');
    });

    test('sin sugerencias devuelve lista vacía, no null', () {
      final respuesta = RespuestaChatbotApi.desdeJson({
        'consultaId': 'c2',
        'mensajeRespuesta': 'Listo.',
      });

      expect(respuesta.sugerencias, isEmpty);
      expect(respuesta.requiereAgente, isFalse);
    });
  });
}
