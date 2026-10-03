import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../auth/permisos.dart';
import '../auth/sesion.dart';
import '../tema.dart';
import 'cambiar_password.dart';
import 'chatbot.dart';
import 'citas_cliente.dart';
import 'clientes.dart';
import 'comunes.dart';
import 'tienda.dart';
import 'ordenes.dart';
import 'portal_cliente.dart';
import 'tablero.dart';
import 'unidades.dart';

/// Una pestaña de la barra inferior.
class _Seccion {
  const _Seccion({
    required this.etiqueta,
    required this.titulo,
    required this.icono,
    required this.iconoActivo,
    required this.pantalla,
  });

  final String etiqueta;
  final String titulo;
  final IconData icono;
  final IconData iconoActivo;
  final Widget pantalla;
}

/// Solo las pestañas que el usuario puede ver. Así ninguna pide datos que la
/// API le negaría con 403: el técnico, por ejemplo, no ve el tablero ni clientes.
List<_Seccion> _seccionesPara(EstadoSesion sesion) {
  // El cliente tiene su propia app: inicio con lo suyo, sus órdenes, sus citas
  // y sus comprobantes (docs/referencias/app-cliente.html).
  if (sesion.soloCliente) {
    return [
      if (sesion.tienePermiso(Permisos.portalAcceso))
        const _Seccion(
          etiqueta: 'Inicio',
          titulo: 'Inicio',
          icono: Icons.home_outlined,
          iconoActivo: Icons.home,
          pantalla: PantallaInicioCliente(),
        ),
      if (sesion.tieneAlgunPermiso(Permisos.verOrdenes))
        const _Seccion(
          etiqueta: 'Órdenes',
          titulo: 'Mis órdenes',
          icono: Icons.build_outlined,
          iconoActivo: Icons.build,
          pantalla: PantallaOrdenes(),
        ),
      if (sesion.tienePermiso(Permisos.citasVer))
        const _Seccion(
          etiqueta: 'Citas',
          titulo: 'Mis citas',
          icono: Icons.event_outlined,
          iconoActivo: Icons.event,
          pantalla: PantallaCitasCliente(),
        ),
      if (sesion.tienePermiso(Permisos.portalAcceso))
        const _Seccion(
          etiqueta: 'Documentos',
          titulo: 'Documentos',
          icono: Icons.receipt_long_outlined,
          iconoActivo: Icons.receipt_long,
          pantalla: PantallaDocumentos(),
        ),
    ];
  }

  final soloAsignadas = !sesion.tienePermiso(Permisos.ordenesVerTodas);

  return [
    if (sesion.tienePermiso(Permisos.reportesVerOperativos))
      const _Seccion(
        etiqueta: 'Tablero',
        titulo: 'Tablero',
        icono: Icons.dashboard_outlined,
        iconoActivo: Icons.dashboard,
        pantalla: PantallaTablero(),
      ),
    if (sesion.tieneAlgunPermiso(Permisos.verOrdenes))
      _Seccion(
        etiqueta: 'Órdenes',
        titulo: soloAsignadas ? 'Mis órdenes' : 'Órdenes',
        icono: Icons.build_outlined,
        iconoActivo: Icons.build,
        pantalla: const PantallaOrdenes(),
      ),
    if (sesion.tieneAlgunPermiso([Permisos.inventarioVer, Permisos.ventasVer]))
      const _Seccion(
        etiqueta: 'Tienda',
        titulo: 'Tienda',
        icono: Icons.storefront_outlined,
        iconoActivo: Icons.storefront,
        pantalla: PantallaTienda(),
      ),
    if (sesion.tienePermiso(Permisos.clientesVer))
      const _Seccion(
        etiqueta: 'Clientes',
        titulo: 'Clientes',
        icono: Icons.people_outline,
        iconoActivo: Icons.people,
        pantalla: PantallaClientes(),
      ),
    if (sesion.tienePermiso(Permisos.unidadesVer))
      _Seccion(
        etiqueta: 'Unidades',
        titulo: 'Unidades',
        icono: Icons.two_wheeler_outlined,
        iconoActivo: Icons.two_wheeler,
        pantalla: const PantallaUnidades(),
      ),
  ];
}

class PantallaInicio extends ConsumerStatefulWidget {
  const PantallaInicio({super.key});

  @override
  ConsumerState<PantallaInicio> createState() => _PantallaInicioState();
}

class _PantallaInicioState extends ConsumerState<PantallaInicio> {
  int _seccion = 0;

  @override
  Widget build(BuildContext context) {
    final sesion = ref.watch(sesionProvider);

    if (sesion.cargando) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    final secciones = _seccionesPara(sesion);
    // Los roles llegan después del token: el índice puede quedar fuera de rango.
    final indice = secciones.isEmpty ? 0 : _seccion.clamp(0, secciones.length - 1);
    final subtitulo = sesion.roles.isEmpty
        ? (sesion.usuario?.nombre ?? '')
        : '${sesion.usuario?.nombre ?? ''} · ${sesion.roles.join(', ')}';

    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(secciones.isEmpty ? 'Team Benavides' : secciones[indice].titulo),
            Text(
              subtitulo,
              style: const TextStyle(fontSize: 12, color: Colors.white70),
            ),
          ],
        ),
        actions: [
          // La agenda del personal va arriba y no en la barra inferior, que ya
          // tiene cinco pestañas para Gerencia.
          if (!sesion.soloCliente && sesion.tienePermiso(Permisos.citasVer))
            IconButton(
              tooltip: 'Agenda',
              icon: const Icon(Icons.event_outlined),
              onPressed: () => context.go('/agenda'),
            ),
          IconButton(
            tooltip: 'Cambiar contraseña',
            icon: const Icon(Icons.key_outlined),
            onPressed: () => abrirCambioDePassword(context),
          ),
          IconButton(
            tooltip: 'Cerrar sesión',
            icon: const Icon(Icons.logout),
            onPressed: () => ref.read(sesionProvider.notifier).salir(),
          ),
        ],
      ),
      body: secciones.isEmpty
          ? (sesion.errorPermisos != null
              ? AvisoError(
                  error: 'No se pudieron cargar tus permisos: ${sesion.errorPermisos}',
                  alReintentar: () => ref.read(sesionProvider.notifier).reintentarPermisos(),
                )
              : const ListaVacia(
                  mensaje: 'Tu usuario todavía no tiene pantallas asignadas. '
                      'Pide a Gerencia que revise tus permisos.',
                ))
          : IndexedStack(
              index: indice,
              children: [for (final seccion in secciones) seccion.pantalla],
            ),
      floatingActionButton: FloatingActionButton(
        tooltip: 'Asistente',
        backgroundColor: Marca.acentoBoton,
        foregroundColor: Colors.white,
        onPressed: () => abrirChatbot(context),
        child: const Icon(Icons.chat_bubble_outline),
      ),
      // La barra inferior necesita al menos dos destinos.
      bottomNavigationBar: secciones.length < 2
          ? null
          : NavigationBar(
              selectedIndex: indice,
              onDestinationSelected: (nuevo) => setState(() => _seccion = nuevo),
              indicatorColor: Marca.acento.withValues(alpha: 0.15),
              destinations: [
                for (final seccion in secciones)
                  NavigationDestination(
                    icon: Icon(seccion.icono),
                    selectedIcon: Icon(seccion.iconoActivo),
                    label: seccion.etiqueta,
                  ),
              ],
            ),
    );
  }
}
