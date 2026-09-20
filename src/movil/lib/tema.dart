import 'package:flutter/material.dart';

/// Colores y tipografías de Team Benavides. Igual que en la web
/// (src/web/src/theme/tokens.ts): viven en un solo archivo y nunca se escriben
/// sueltos en las pantallas.
class Marca {
  const Marca._();

  static const fondo = Color(0xFFF3F2F2);
  static const superficie = Color(0xFFFFFFFF);
  static const texto = Color(0xFF201E1D);
  static const textoSecundario = Color(0xFF6B6664);
  static const borde = Color(0xFFE3E1E0);
  static const acento = Color(0xFFEC3013);
  static const acentoBoton = Color(0xFFDD2B0F);

  /// Space Grotesk para títulos, IBM Plex Sans para texto, como la web.
  static const fuenteTitulos = 'Space Grotesk';
  static const fuenteTexto = 'IBM Plex Sans';
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
    fontFamily: Marca.fuenteTexto,
  );

  TextStyle? conTitulo(TextStyle? estilo) =>
      estilo?.copyWith(fontFamily: Marca.fuenteTitulos, fontWeight: FontWeight.w700);

  return base.copyWith(
    textTheme: base.textTheme.copyWith(
      displaySmall: conTitulo(base.textTheme.displaySmall),
      headlineLarge: conTitulo(base.textTheme.headlineLarge),
      headlineMedium: conTitulo(base.textTheme.headlineMedium),
      headlineSmall: conTitulo(base.textTheme.headlineSmall),
      titleLarge: conTitulo(base.textTheme.titleLarge),
    ),
    appBarTheme: const AppBarTheme(
      backgroundColor: Marca.texto,
      foregroundColor: Colors.white,
      elevation: 0,
      titleTextStyle: TextStyle(
        fontFamily: Marca.fuenteTitulos,
        fontSize: 20,
        fontWeight: FontWeight.w700,
        color: Colors.white,
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: Marca.acentoBoton,
        foregroundColor: Colors.white,
        minimumSize: const Size.fromHeight(48),
        textStyle: const TextStyle(
          fontFamily: Marca.fuenteTexto,
          fontSize: 15,
          fontWeight: FontWeight.w600,
        ),
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
