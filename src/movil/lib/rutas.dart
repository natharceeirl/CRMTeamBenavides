import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'auth/sesion.dart';
import 'pantallas/cliente_detalle.dart';
import 'pantallas/inicio.dart';
import 'pantallas/login.dart';

/// Rutas de la app. El guardia vive acá: sin sesión solo se puede estar en el
/// login, y con sesión el login redirige al inicio.
final routerProvider = Provider<GoRouter>((ref) {
  final aviso = ValueNotifier<int>(0);
  ref.listen(sesionProvider, (_, _) => aviso.value++);
  ref.onDispose(aviso.dispose);

  return GoRouter(
    refreshListenable: aviso,
    initialLocation: '/',
    redirect: (context, estado) {
      final sesion = ref.read(sesionProvider);

      // Mientras se lee la sesión guardada no se decide nada: el inicio muestra
      // un indicador de carga.
      if (sesion.cargando) {
        return null;
      }

      final enLogin = estado.matchedLocation == '/login';
      if (!sesion.autenticado) {
        return enLogin ? null : '/login';
      }
      return enLogin ? '/' : null;
    },
    routes: [
      GoRoute(
        path: '/login',
        builder: (context, estado) => const PantallaLogin(),
      ),
      GoRoute(
        path: '/',
        builder: (context, estado) => const PantallaInicio(),
        routes: [
          GoRoute(
            path: 'clientes/:id',
            builder: (context, estado) => PantallaClienteDetalle(
              clienteId: estado.pathParameters['id']!,
            ),
          ),
        ],
      ),
    ],
  );
});
