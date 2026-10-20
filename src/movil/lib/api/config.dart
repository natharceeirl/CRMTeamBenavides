import 'package:flutter/foundation.dart';

const _urlDefinida = String.fromEnvironment('API_URL');

/// Dirección de la API.
///
/// `localhost` no significa lo mismo en todos lados: para el emulador de Android
/// el PC es 10.0.2.2, y un teléfono físico necesita la IP del PC en la red. Por
/// eso se puede fijar al arrancar:
///   flutter run --dart-define=API_URL=http://192.168.1.50:5021
String urlBaseApi() {
  if (_urlDefinida.isNotEmpty) {
    return _urlDefinida;
  }

  if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
    return 'http://10.0.2.2:5021';
  }

  return 'http://localhost:5021';
}

/// Por qué esta compilación no puede funcionar, o null si puede.
///
/// Una versión de release necesita la API publicada con HTTPS. Sin `API_URL`
/// caería en 10.0.2.2, que en un teléfono de verdad no lleva a ningún lado, y la
/// app fallaría sin decir por qué. Para Android ya lo frena Gradle al compilar
/// (ver android/app/build.gradle.kts); esto cubre iOS, que se compila en un Mac.
String? problemaDeConfiguracion({bool release = kReleaseMode, String url = _urlDefinida}) {
  if (!release) return null;
  if (url.isEmpty) {
    return 'Esta versión se compiló sin la dirección de la API (API_URL).';
  }
  if (!url.startsWith('https://')) {
    return 'La dirección de la API ($url) tiene que empezar con https://.';
  }
  return null;
}

/// La política de privacidad está en la web, que comparte dominio con la API.
Uri urlPrivacidad({String? base}) => Uri.parse(base ?? urlBaseApi()).resolve('/privacidad');
