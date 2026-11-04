import 'package:crm_team_benavides/api/modelos.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('un repuesto en dólares con IGV trae su precio de lista en soles', () {
    final repuesto = ProductoApi.desdeJson({
      'id': 'p1',
      'codigo': 'FIL-01',
      'nombre': 'Filtro de aceite',
      'unidad': 'UND',
      'precioVenta': 25,
      'monedaPrecio': 1,
      'precioIncluyeIgv': true,
      'precioListaSoles': 94,
      'stockActual': 3,
    });

    expect(repuesto.monedaPrecio, MonedaPrecio.usd);
    expect(repuesto.precioListaSoles, 94);
    expect(repuesto.textoPrecio, r'US$ 25.00');
  });

  test('un repuesto sin IGV lo dice; uno anterior, sin los campos nuevos, queda en soles y sin IGV', () {
    final sinIgv = ProductoApi.desdeJson({'id': 'p2', 'precioVenta': 29.66, 'monedaPrecio': 0, 'precioIncluyeIgv': false});
    final anterior = ProductoApi.desdeJson({'id': 'p3', 'precioVenta': 30});

    expect(sinIgv.textoPrecio, 'S/ 29.66 + IGV');
    expect(anterior.monedaPrecio, MonedaPrecio.pen);
    expect(anterior.precioIncluyeIgv, isFalse);
  });

  test('una línea cobrada con IGV se muestra y se edita con IGV', () {
    final conIgv = DetalleServicioApi.desdeJson({
      'id': 'd1',
      'descripcion': 'Filtro',
      'cantidad': 1,
      'precioUnitario': 8.47,
      'precioUnitarioConIgv': 10,
      'subtotal': 8.47,
      'esRepuesto': true,
    });
    final sinIgv = DetalleServicioApi.desdeJson({
      'id': 'd2',
      'descripcion': 'Mano de obra',
      'cantidad': 1,
      'precioUnitario': 50,
      'subtotal': 50,
      'esRepuesto': false,
    });

    expect(conIgv.precioEscrito, 10);
    expect(sinIgv.precioEscrito, 50);
  });
}
