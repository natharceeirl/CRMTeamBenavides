import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api/config.dart';
import 'avisos_telefono.dart';
import 'rutas.dart';
import 'tema.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // Una versión de release sin la dirección de la API lo dice en vez de fallar callada.
  final problema = problemaDeConfiguracion();
  if (problema != null) {
    runApp(PantallaErrorConfiguracion(problema: problema));
    return;
  }
  await TelefonoNotificaciones.instancia.iniciar();
  runApp(const ProviderScope(child: AplicacionCrm()));
}

/// Lo único que muestra una compilación mal configurada: qué falta y cómo se arregla.
class PantallaErrorConfiguracion extends StatelessWidget {
  const PantallaErrorConfiguracion({super.key, required this.problema});

  final String problema;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Team Benavides',
      debugShowCheckedModeBanner: false,
      theme: temaTeamBenavides(),
      home: Scaffold(
        body: SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('La app no está bien configurada', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 12),
                Text(problema),
                const SizedBox(height: 12),
                const Text(
                  'Hay que volver a compilarla con --dart-define=API_URL=https://… '
                  'Avisa al equipo que la publicó.',
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
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
