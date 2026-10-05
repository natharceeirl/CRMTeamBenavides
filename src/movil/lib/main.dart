import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'rutas.dart';
import 'tema.dart';

void main() {
  runApp(const ProviderScope(child: AplicacionCrm()));
}

class AplicacionCrm extends ConsumerWidget {
  const AplicacionCrm({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return MaterialApp.router(
      title: 'Team Benavides',
      debugShowCheckedModeBanner: false,
      theme: temaTeamBenavides(),
      // La app es en español: calendario, reloj y textos de Material incluidos.
      locale: const Locale('es'),
      supportedLocales: const [Locale('es')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      routerConfig: ref.watch(routerProvider),
    );
  }
}
