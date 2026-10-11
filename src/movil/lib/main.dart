import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'avisos_telefono.dart';
import 'rutas.dart';
import 'tema.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await TelefonoNotificaciones.instancia.iniciar();
  runApp(const ProviderScope(child: AplicacionCrm()));
}

class AplicacionCrm extends ConsumerWidget {
  const AplicacionCrm({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Registra el teléfono al entrar y abre la pantalla de cada notificación.
    ref.watch(avisosTelefonoProvider);
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
