import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../api/config.dart';

/// Abre en el navegador la política de privacidad de la web. Las tiendas piden
/// que se pueda leer desde la app, también antes de iniciar sesión.
Future<void> abrirPrivacidad(BuildContext context) async {
  final url = urlPrivacidad();
  final avisos = ScaffoldMessenger.of(context);
  final abierta = await launchUrl(url, mode: LaunchMode.externalApplication).catchError((Object _) => false);
  if (!abierta) {
    avisos.showSnackBar(SnackBar(content: Text('No se pudo abrir $url')));
  }
}
