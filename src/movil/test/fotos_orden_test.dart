import 'package:crm_team_benavides/api/estados.dart';
import 'package:crm_team_benavides/api/modelos.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('lee la foto tal como la manda GET /api/ordenes-servicio/{id}/fotos', () {
    final foto = FotoOrdenApi.desdeJson({
      'id': 'f1',
      'ordenServicioId': 'o1',
      'nombreArchivoOriginal': 'IMG_0001.jpg',
      'urlRelativa': '/api/ordenes-servicio/o1/fotos/f1/archivo',
      'contentType': 'image/jpeg',
      'tamanioBytes': 345678,
      'etapa': 2,
      'etapaNombre': 'Reparacion',
      'usuarioId': 'u1',
      'usuarioNombre': 'Mario Técnico',
      'observacion': 'Retén cambiado',
      'fechaCreacion': '2026-10-08T15:30:00Z',
    });

    expect(foto.urlRelativa, '/api/ordenes-servicio/o1/fotos/f1/archivo');
    expect(foto.etapa, EtapaFoto.reparacion);
    expect(foto.nombreEtapa, 'Reparación');
    expect(foto.observacion, 'Retén cambiado');
  });

  test('la etapa sugerida sigue el estado de la orden', () {
    expect(etapaFotoSugerida(EstadoOrden.abierta), EtapaFoto.ingreso);
    expect(etapaFotoSugerida(EstadoOrden.diagnostico), EtapaFoto.diagnostico);
    expect(etapaFotoSugerida(EstadoOrden.aprobada), EtapaFoto.diagnostico);
    expect(etapaFotoSugerida(EstadoOrden.enProceso), EtapaFoto.reparacion);
    expect(etapaFotoSugerida(EstadoOrden.lista), EtapaFoto.entrega);
  });
}
