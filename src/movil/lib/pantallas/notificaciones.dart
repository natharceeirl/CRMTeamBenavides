import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/modelos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

/// La pantalla a la que lleva un aviso, o null si su lugar es el inicio (el
/// cliente ve sus citas y pedidos ahí).
String? rutaDeNotificacion(NotificacionApi aviso, {required bool esCliente}) =>
    rutaDeEntidad(aviso.entidad, aviso.entidadId, esCliente: esCliente);

/// Lo mismo a partir de los datos que trae la notificación del teléfono.
String? rutaDeEntidad(String? entidad, String? entidadId, {required bool esCliente}) {
  switch (entidad) {
    case 'Aprobaciones':
      return '/aprobaciones';
    case 'OrdenServicio':
      return entidadId == null || entidadId.isEmpty ? null : '/ordenes/$entidadId';
    case 'Cita':
      return esCliente ? null : '/agenda';
    default:
      return null;
  }
}

IconData _iconoDe(int tipo) => switch (tipo) {
      TipoNotificacion.solicitudAprobacion => Icons.price_check,
      TipoNotificacion.presupuestoEnviado => Icons.request_quote_outlined,
      TipoNotificacion.ordenLista => Icons.check_circle_outline,
      TipoNotificacion.pedidoLimaRecibido => Icons.local_shipping_outlined,
      TipoNotificacion.citaConfirmada => Icons.event_available_outlined,
      _ => Icons.build_outlined,
    };

/// Campanita de la barra superior con los avisos sin leer. Mientras la app está
/// abierta vuelve a preguntar cada minuto; con las notificaciones del teléfono
/// el aviso además llega al instante.
class CampanaNotificaciones extends ConsumerStatefulWidget {
  const CampanaNotificaciones({super.key});

  @override
  ConsumerState<CampanaNotificaciones> createState() => _CampanaNotificacionesState();
}

class _CampanaNotificacionesState extends ConsumerState<CampanaNotificaciones> {
  Timer? _reloj;

  @override
  void initState() {
    super.initState();
    _reloj = Timer.periodic(const Duration(minutes: 1), (_) => ref.invalidate(conteoNotificacionesProvider));
  }

  @override
  void dispose() {
    _reloj?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final noLeidas = ref.watch(conteoNotificacionesProvider).value ?? 0;

    return IconButton(
      tooltip: noLeidas > 0 ? 'Notificaciones, $noLeidas sin leer' : 'Notificaciones',
      icon: Badge(
        isLabelVisible: noLeidas > 0,
        label: Text(noLeidas > 99 ? '99+' : '$noLeidas'),
        child: const Icon(Icons.notifications_none),
      ),
      onPressed: () async {
        await context.push('/notificaciones');
        ref.invalidate(conteoNotificacionesProvider);
      },
    );
  }
}

class PantallaNotificaciones extends ConsumerWidget {
  const PantallaNotificaciones({super.key});

  Future<void> _abrir(BuildContext context, WidgetRef ref, NotificacionApi aviso) async {
    if (!aviso.leida) {
      // Si falla, el aviso sigue sin leer: no impide abrir su pantalla.
      await ref.read(apiProvider).marcarNotificacionLeida(aviso.id).catchError((_) {});
      ref.invalidate(notificacionesProvider);
      ref.invalidate(conteoNotificacionesProvider);
    }
    if (!context.mounted) return;

    final ruta = rutaDeNotificacion(aviso, esCliente: ref.read(sesionProvider).soloCliente);
    if (ruta == null) {
      context.go('/');
    } else {
      await context.push(ruta);
    }
  }

  Future<void> _marcarTodas(BuildContext context, WidgetRef ref) async {
    try {
      await ref.read(apiProvider).marcarTodasLeidas();
      ref.invalidate(notificacionesProvider);
      ref.invalidate(conteoNotificacionesProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$error')));
      }
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final avisos = ref.watch(notificacionesProvider);
    final hayNoLeidas = avisos.value?.any((aviso) => !aviso.leida) ?? false;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Notificaciones'),
        actions: [
          if (hayNoLeidas)
            TextButton(
              onPressed: () => _marcarTodas(context, ref),
              style: TextButton.styleFrom(foregroundColor: Colors.white),
              child: const Text('Marcar todas'),
            ),
        ],
      ),
      body: avisos.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => AvisoError(error: error, alReintentar: () => ref.invalidate(notificacionesProvider)),
        data: (lista) => lista.isEmpty
            ? const ListaVacia(mensaje: 'No tienes notificaciones.')
            : RefreshIndicator(
                onRefresh: () => ref.refresh(notificacionesProvider.future),
                child: ListView.separated(
                  itemCount: lista.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (context, indice) {
                    final aviso = lista[indice];
                    return ListTile(
                      leading: Icon(_iconoDe(aviso.tipo), color: aviso.leida ? Marca.textoSecundario : Marca.acento),
                      title: Text(
                        aviso.titulo,
                        style: TextStyle(fontWeight: aviso.leida ? FontWeight.w400 : FontWeight.w700),
                      ),
                      subtitle: Text('${aviso.mensaje}\n${fechaHora(aviso.fechaCreacion)}'),
                      isThreeLine: true,
                      onTap: () => _abrir(context, ref, aviso),
                    );
                  },
                ),
              ),
      ),
    );
  }
}
