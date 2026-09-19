import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/almacen_sesion.dart';
import '../api/api_http.dart';
import '../api/modelos.dart';
import 'jwt.dart';

class EstadoSesion {
  const EstadoSesion({this.usuario, this.roles = const [], this.cargando = false});

  final UsuarioSesion? usuario;

  /// Roles del usuario, desde GET /api/auth/me. Vacío mientras carga.
  final List<String> roles;
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
    if (sesion == null) {
      state = const EstadoSesion();
      return;
    }

    await _cargarUsuario(sesion.accessToken);
  }

  /// El token da el nombre al instante; /api/auth/me confirma y trae los roles.
  Future<void> _cargarUsuario(String accessToken) async {
    final delToken = leerUsuarioDelToken(accessToken);
    state = EstadoSesion(usuario: delToken);

    try {
      final yo = await ref.read(apiProvider).usuarioActual();
      state = EstadoSesion(
        usuario: UsuarioSesion(id: yo.id, email: yo.email, nombre: yo.nombreCompleto),
        roles: yo.roles,
      );
    } on ErrorApi {
      // Si /me falla se sigue con lo que trae el token: no vale la pena
      // echar al usuario de la app por esto.
    }
  }

  Future<void> entrar(String email, String password) async {
    final sesion = await ref.read(apiProvider).iniciarSesion(email, password);
    await _cargarUsuario(sesion.accessToken);
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

final ordenesProvider = FutureProvider.autoDispose<List<OrdenServicioApi>>(
  (ref) => ref.watch(apiProvider).ordenes(),
);

final ordenProvider =
    FutureProvider.autoDispose.family<OrdenServicioDetalleApi, String>(
  (ref, id) => ref.watch(apiProvider).orden(id),
);

final resumenProvider = FutureProvider.autoDispose<ResumenDashboardApi>(
  (ref) => ref.watch(apiProvider).resumenDashboard(),
);

final faqsProvider = FutureProvider.autoDispose<List<FaqApi>>(
  (ref) => ref.watch(apiProvider).faqs(),
);
