import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/sesion.dart';
import '../tema.dart';
import 'chatbot.dart';
import 'clientes.dart';
import 'inventario.dart';
import 'ordenes.dart';
import 'tablero.dart';
import 'unidades.dart';

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

    final titulos = ['Tablero', 'Órdenes', 'Inventario', 'Clientes', 'Unidades'];
    final subtitulo = sesion.roles.isEmpty
        ? (sesion.usuario?.nombre ?? '')
        : '${sesion.usuario?.nombre ?? ''} · ${sesion.roles.join(', ')}';

    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(titulos[_seccion]),
            Text(
              subtitulo,
              style: const TextStyle(fontSize: 12, color: Colors.white70),
            ),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'Cerrar sesión',
            icon: const Icon(Icons.logout),
            onPressed: () => ref.read(sesionProvider.notifier).salir(),
          ),
        ],
      ),
      body: IndexedStack(
        index: _seccion,
        children: const [
          PantallaTablero(),
          PantallaOrdenes(),
          PantallaInventario(),
          PantallaClientes(),
          PantallaUnidades(),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        tooltip: 'Asistente',
        backgroundColor: Marca.acentoBoton,
        foregroundColor: Colors.white,
        onPressed: () => abrirChatbot(context),
        child: const Icon(Icons.chat_bubble_outline),
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _seccion,
        onDestinationSelected: (indice) => setState(() => _seccion = indice),
        indicatorColor: Marca.acento.withValues(alpha: 0.15),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.dashboard_outlined),
            selectedIcon: Icon(Icons.dashboard),
            label: 'Tablero',
          ),
          NavigationDestination(
            icon: Icon(Icons.build_outlined),
            selectedIcon: Icon(Icons.build),
            label: 'Órdenes',
          ),
          NavigationDestination(
            icon: Icon(Icons.inventory_2_outlined),
            selectedIcon: Icon(Icons.inventory_2),
            label: 'Inventario',
          ),
          NavigationDestination(
            icon: Icon(Icons.people_outline),
            selectedIcon: Icon(Icons.people),
            label: 'Clientes',
          ),
          NavigationDestination(
            icon: Icon(Icons.two_wheeler_outlined),
            selectedIcon: Icon(Icons.two_wheeler),
            label: 'Unidades',
          ),
        ],
      ),
    );
  }
}
