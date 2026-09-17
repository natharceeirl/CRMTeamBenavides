import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/almacen_sesion.dart';
import '../api/api_http.dart';
import '../api/modelos.dart';
import 'jwt.dart';

class EstadoSesion {
  const EstadoSesion({this.usuario, this.cargando = false});

  final UsuarioSesion? usuario;
  final bool cargando;

  bool get autenticado => usuario != null;
}

final almacenSesionProvider = Provider<AlmacenSesion>((ref) => AlmacenSesion());

final apiProvider = Provider<ApiHttp>((ref) {
  final api = ApiHttp(ref.watch(almacenSesionProvider));
  api.alExpirarSesion = () {
    ref.read(sesionProvider.notifier).marcarSesionExpirada();
  };
  return api;
});

final sesionProvider =
    NotifierProvider<SesionNotifier, EstadoSesion>(SesionNotifier.new);

class SesionNotifier extends Notifier<EstadoSesion> {
  @override
  EstadoSesion build() {
    // Al arrancar se intenta recuperar la sesión guardada en el dispositivo.
    Future.microtask(_recuperar);
    return const EstadoSesion(cargando: true);
  }

  Future<void> _recuperar() async {
    final sesion = await ref.read(apiProvider).recuperarSesionGuardada();
    state = EstadoSesion(
      usuario: sesion == null ? null : leerUsuarioDelToken(sesion.accessToken),
    );
  }

  Future<void> entrar(String email, String password) async {
    final sesion = await ref.read(apiProvider).iniciarSesion(email, password);
    state = EstadoSesion(usuario: leerUsuarioDelToken(sesion.accessToken));
  }

  Future<void> salir() async {
    await ref.read(apiProvider).cerrarSesion();
    state = const EstadoSesion();
  }

  /// La llama la capa de API cuando el refresh ya no sirve.
  void marcarSesionExpirada() {
    state = const EstadoSesion();
  }
}

final clientesProvider = FutureProvider.autoDispose<List<ClienteApi>>(
  (ref) => ref.watch(apiProvider).clientes(),
);

final clienteProvider = FutureProvider.autoDispose.family<ClienteApi, String>(
  (ref, id) => ref.watch(apiProvider).cliente(id),
);

final vehiculosProvider =
    FutureProvider.autoDispose.family<List<VehiculoApi>, String?>(
  (ref, clienteId) => ref.watch(apiProvider).vehiculos(clienteId: clienteId),
);
