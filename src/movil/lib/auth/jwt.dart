import 'dart:convert';

import '../api/modelos.dart';

/// Saca el usuario de los claims del access token.
///
/// Provisional, igual que en la web: cuando el backend agregue GET /api/auth/me
/// o mande el usuario en la respuesta del login, esto se reemplaza.
UsuarioSesion? leerUsuarioDelToken(String token) {
  final partes = token.split('.');
  if (partes.length < 2) {
    return null;
  }

  try {
    final cuerpo = utf8.decode(base64Url.decode(base64Url.normalize(partes[1])));
    final claims = jsonDecode(cuerpo) as Map<String, dynamic>;
    final id = claims['sub'] as String?;
    if (id == null) {
      return null;
    }

    final email = claims['email'] as String? ?? '';
    return UsuarioSesion(
      id: id,
      email: email,
      nombre: claims['name'] as String? ?? (email.isEmpty ? 'Usuario' : email),
    );
  } catch (_) {
    return null;
  }
}
