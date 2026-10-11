import 'dart:async';
import 'dart:convert';
import 'dart:io' show Platform;

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'auth/sesion.dart';
import 'pantallas/notificaciones.dart';
import 'rutas.dart';
import 'tema.dart';

/// Mismo id que `PushFirebase.CanalAndroid` en la API y que el canal por defecto
/// del AndroidManifest.
const _canal = AndroidNotificationChannel(
  'avisos',
  'Avisos del taller',
  description: 'Aprobaciones, presupuestos y órdenes listas.',
  importance: Importance.high,
);

/// Notificaciones del teléfono por Firebase. Si Firebase no arranca (sin
/// google-services.json, en pruebas o en escritorio) queda inactivo y la app
/// sigue con la campanita.
class TelefonoNotificaciones {
  TelefonoNotificaciones();

  /// La que inicia `main` antes de abrir la app.
  static final instancia = TelefonoNotificaciones();

  final _locales = FlutterLocalNotificationsPlugin();
  final _tocadas = StreamController<Map<String, dynamic>>.broadcast();
  Map<String, dynamic>? _alAbrir;
  bool _activo = false;

  bool get activo => _activo;

  /// 0 Android, 1 iOS, como `PlataformaDispositivo` en la API.
  int get plataforma => Platform.isIOS ? 1 : 0;

  Future<void> iniciar() async {
    if (kIsWeb || !(Platform.isAndroid || Platform.isIOS)) return;
    try {
      await Firebase.initializeApp();
      await _locales.initialize(
        settings: const InitializationSettings(
          android: AndroidInitializationSettings('ic_notificacion'),
          // El permiso se pide al entrar, no al abrir la app.
          iOS: DarwinInitializationSettings(
            requestAlertPermission: false,
            requestBadgePermission: false,
            requestSoundPermission: false,
          ),
        ),
        onDidReceiveNotificationResponse: (respuesta) {
          final datos = _leer(respuesta.payload);
          if (datos != null) _tocadas.add(datos);
        },
      );
      await _locales
          .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>()
          ?.createNotificationChannel(_canal);
      // En iPhone el sistema muestra la notificación aunque la app esté abierta.
      await FirebaseMessaging.instance.setForegroundNotificationPresentationOptions(
        alert: true,
        badge: true,
        sound: true,
      );

      FirebaseMessaging.onMessageOpenedApp.listen((mensaje) => _tocadas.add(mensaje.data));
      final inicial = await FirebaseMessaging.instance.getInitialMessage();
      final lanzamiento = await _locales.getNotificationAppLaunchDetails();
      _alAbrir = inicial?.data ??
          (lanzamiento?.didNotificationLaunchApp == true ? _leer(lanzamiento!.notificationResponse?.payload) : null);
      _activo = true;
    } catch (error) {
      debugPrint('Notificaciones del teléfono desactivadas: $error');
    }
  }

  /// Pide el permiso (Android 13+ e iOS) y devuelve el token, o null si la
  /// persona no lo dio.
  Future<String?> token() async {
    if (!_activo) return null;
    final permiso = await FirebaseMessaging.instance.requestPermission();
    if (permiso.authorizationStatus == AuthorizationStatus.denied) return null;
    return FirebaseMessaging.instance.getToken();
  }

  Stream<String> get tokenRenovado => _activo ? FirebaseMessaging.instance.onTokenRefresh : const Stream.empty();

  /// Las que llegan con la app abierta: el sistema no las muestra solo.
  Stream<RemoteMessage> get recibidas => _activo ? FirebaseMessaging.onMessage : const Stream.empty();

  /// Los datos de cada notificación que la persona toca.
  Stream<Map<String, dynamic>> get tocadas => _tocadas.stream;

  /// La notificación que abrió la app cerrada, una sola vez.
  Map<String, dynamic>? tomarAperturaInicial() {
    final datos = _alAbrir;
    _alAbrir = null;
    return datos;
  }

  Future<void> mostrar(RemoteMessage mensaje) async {
    final aviso = mensaje.notification;
    if (!_activo || aviso == null || !Platform.isAndroid) return;
    final clave = mensaje.data['notificacionId'] ?? mensaje.messageId ?? aviso.title;
    await _locales.show(
      id: clave.hashCode & 0x7fffffff,
      title: aviso.title,
      body: aviso.body,
      notificationDetails: NotificationDetails(
        android: AndroidNotificationDetails(
          _canal.id,
          _canal.name,
          channelDescription: _canal.description,
          importance: Importance.high,
          priority: Priority.high,
          icon: 'ic_notificacion',
          color: Marca.acento,
        ),
      ),
      payload: jsonEncode(mensaje.data),
    );
  }

  static Map<String, dynamic>? _leer(String? payload) {
    if (payload == null || payload.isEmpty) return null;
    try {
      return (jsonDecode(payload) as Map).cast<String, dynamic>();
    } catch (_) {
      return null;
    }
  }
}

final telefonoProvider = Provider<TelefonoNotificaciones>((ref) => TelefonoNotificaciones.instancia);

/// Une las notificaciones del teléfono con la sesión: registra el teléfono al
/// entrar, refresca la campanita cuando llega una y abre su pantalla al tocarla.
/// La baja al salir la hace `ApiHttp.cerrarSesion`.
final avisosTelefonoProvider = Provider<AvisosTelefono>((ref) {
  final avisos = AvisosTelefono(ref, ref.watch(telefonoProvider));
  if (avisos.activo) {
    ref.listen(sesionProvider, (_, sesion) => avisos.alCambiarSesion(sesion), fireImmediately: true);
  }
  ref.onDispose(avisos.cerrar);
  return avisos;
});

class AvisosTelefono {
  AvisosTelefono(this._ref, this._telefono) {
    if (!activo) return;
    _pendiente = _telefono.tomarAperturaInicial();
    _suscripciones.addAll([
      _telefono.tokenRenovado.listen((_) => _registrar()),
      _telefono.recibidas.listen(_alRecibir),
      _telefono.tocadas.listen((datos) {
        _pendiente = datos;
        _abrirPendiente();
      }),
    ]);
  }

  final Ref _ref;
  final TelefonoNotificaciones _telefono;
  final _suscripciones = <StreamSubscription<Object?>>[];

  /// Usuario para el que ya se registró el teléfono en esta apertura.
  String? _registradoPara;

  /// Notificación tocada que espera a que la sesión esté lista para abrirse.
  Map<String, dynamic>? _pendiente;

  bool get activo => _telefono.activo;

  void alCambiarSesion(EstadoSesion sesion) {
    final usuarioId = sesion.usuario?.id;
    if (usuarioId == null) {
      _registradoPara = null;
      return;
    }
    if (usuarioId != _registradoPara) {
      _registradoPara = usuarioId;
      _registrar();
    }
    _abrirPendiente();
  }

  Future<void> _registrar() async {
    if (!_ref.read(sesionProvider).autenticado) return;
    try {
      final token = await _telefono.token();
      if (token == null) return;
      await _ref.read(apiProvider).registrarDispositivo(token, _telefono.plataforma);
    } catch (error) {
      // Sin registro el usuario igual ve sus avisos en la campanita.
      debugPrint('No se registró el teléfono para notificaciones: $error');
    }
  }

  void _alRecibir(RemoteMessage mensaje) {
    _telefono.mostrar(mensaje);
    _refrescarBandeja(mensaje.data['entidad']);
  }

  void _refrescarBandeja(Object? entidad) {
    _ref.invalidate(conteoNotificacionesProvider);
    _ref.invalidate(notificacionesProvider);
    if (entidad == 'Aprobaciones') _ref.invalidate(aprobacionesPendientesProvider);
  }

  /// Abre la pantalla del aviso cuando ya se sabe quién es el usuario: la ruta
  /// depende de si es cliente.
  void _abrirPendiente() {
    final datos = _pendiente;
    final sesion = _ref.read(sesionProvider);
    final rolesListos = sesion.roles.isNotEmpty || sesion.errorPermisos != null;
    if (datos == null || !sesion.autenticado || sesion.cargando || !rolesListos) return;
    _pendiente = null;

    final id = datos['notificacionId'];
    if (id is String && id.isNotEmpty) {
      _ref
          .read(apiProvider)
          .marcarNotificacionLeida(id)
          .then((_) => _refrescarBandeja(datos['entidad']))
          .catchError((Object _) {});
    }

    final ruta = rutaDeEntidad(
      datos['entidad'] as String?,
      datos['entidadId'] as String?,
      esCliente: sesion.soloCliente,
    );
    final rutas = _ref.read(routerProvider);
    if (ruta == null) {
      rutas.go('/');
    } else {
      rutas.push(ruta);
    }
  }

  void cerrar() {
    for (final suscripcion in _suscripciones) {
      suscripcion.cancel();
    }
  }
}
