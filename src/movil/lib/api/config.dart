import 'package:flutter/foundation.dart';

/// Dirección de la API.
///
/// `localhost` no significa lo mismo en todos lados: para el emulador de Android
/// el PC es 10.0.2.2, y un teléfono físico necesita la IP del PC en la red. Por
/// eso se puede fijar al arrancar:
///   flutter run --dart-define=API_URL=http://192.168.1.50:5021
String urlBaseApi() {
  const definida = String.fromEnvironment('API_URL');
  if (definida.isNotEmpty) {
    return definida;
  }

  if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
    return 'http://10.0.2.2:5021';
  }

  return 'http://localhost:5021';
}
