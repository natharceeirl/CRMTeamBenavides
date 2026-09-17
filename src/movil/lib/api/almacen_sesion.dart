import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'modelos.dart';

/// Guarda los tokens en el almacenamiento seguro del sistema, no en preferencias
/// en claro: el refresh token vale siete días.
class AlmacenSesion {
  AlmacenSesion([FlutterSecureStorage? almacen])
      : _almacen = almacen ?? const FlutterSecureStorage();

  static const _clave = 'tb.sesion';

  final FlutterSecureStorage _almacen;

  Future<Sesion?> leer() async {
    try {
      final guardada = await _almacen.read(key: _clave);
      if (guardada == null) {
        return null;
      }
      return Sesion.desdeJson(jsonDecode(guardada) as Map<String, dynamic>);
    } catch (_) {
      // Si el almacén no está disponible o el contenido quedó corrupto, se
      // empieza sin sesión en vez de romper el arranque.
      return null;
    }
  }

  Future<void> guardar(Sesion sesion) async {
    await _almacen.write(key: _clave, value: jsonEncode(sesion.aJson()));
  }

  Future<void> borrar() async {
    await _almacen.delete(key: _clave);
  }
}
