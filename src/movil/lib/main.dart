import 'package:flutter/material.dart';
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
      title: 'CRM Team Benavides',
      debugShowCheckedModeBanner: false,
      theme: temaTeamBenavides(),
      routerConfig: ref.watch(routerProvider),
    );
  }
}
