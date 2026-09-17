import 'package:flutter/material.dart';

/// Colores de Team Benavides. Igual que en la web (src/web/src/theme/tokens.ts):
/// viven en un solo archivo y nunca se escriben sueltos en las pantallas.
///
/// Pendiente: las tipografías Space Grotesk e IBM Plex Sans todavía no están
/// empaquetadas en la app; hay que agregarlas como fuentes en pubspec.yaml.
class Marca {
  const Marca._();

  static const fondo = Color(0xFFF3F2F2);
  static const superficie = Color(0xFFFFFFFF);
  static const texto = Color(0xFF201E1D);
  static const textoSecundario = Color(0xFF6B6664);
  static const borde = Color(0xFFE3E1E0);
  static const acento = Color(0xFFEC3013);
  static const acentoBoton = Color(0xFFDD2B0F);
}

ThemeData temaTeamBenavides() {
  final base = ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: Marca.acento,
      primary: Marca.acentoBoton,
      surface: Marca.superficie,
      brightness: Brightness.light,
    ),
    scaffoldBackgroundColor: Marca.fondo,
  );

  return base.copyWith(
    appBarTheme: const AppBarTheme(
      backgroundColor: Marca.texto,
      foregroundColor: Colors.white,
      elevation: 0,
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: Marca.acentoBoton,
        foregroundColor: Colors.white,
        minimumSize: const Size.fromHeight(48),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(6)),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Marca.superficie,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(6)),
    ),
    cardTheme: CardThemeData(
      color: Marca.superficie,
      elevation: 0,
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
        side: const BorderSide(color: Marca.borde),
      ),
    ),
  );
}
