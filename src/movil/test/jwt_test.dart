import 'dart:convert';

import 'package:crm_team_benavides/auth/jwt.dart';
import 'package:flutter_test/flutter_test.dart';

/// Arma un token con la forma que devuelve el backend, sin firma real: lo que se
/// prueba es la lectura de los claims, no la validación, que es cosa de la API.
String tokenDePrueba(Map<String, dynamic> claims) {
  String parte(Map<String, dynamic> datos) =>
      base64Url.encode(utf8.encode(jsonEncode(datos))).replaceAll('=', '');

  return '${parte({'alg': 'HS256'})}.${parte(claims)}.firma';
}

void main() {
  group('leerUsuarioDelToken', () {
    test('lee el id, el correo y el nombre de los claims', () {
      final usuario = leerUsuarioDelToken(
        tokenDePrueba({
          'sub': '6f9619ff-8b86-d011-b42d-00c04fc964ff',
          'email': 'santiago@teambenavides.pe',
          'name': 'Santiago Callocondo',
        }),
      );

      expect(usuario, isNotNull);
      expect(usuario!.id, '6f9619ff-8b86-d011-b42d-00c04fc964ff');
      expect(usuario.email, 'santiago@teambenavides.pe');
      expect(usuario.nombre, 'Santiago Callocondo');
    });

    test('usa el correo como nombre si el token no trae name', () {
      final usuario = leerUsuarioDelToken(
        tokenDePrueba({'sub': 'abc', 'email': 'asesor@teambenavides.pe'}),
      );

      expect(usuario!.nombre, 'asesor@teambenavides.pe');
    });

    test('devuelve null si el token está mal formado o no tiene sub', () {
      expect(leerUsuarioDelToken('esto-no-es-un-token'), isNull);
      expect(leerUsuarioDelToken(tokenDePrueba({'email': 'x@y.pe'})), isNull);
    });
  });
}
