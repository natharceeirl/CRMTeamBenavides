import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../auth/sesion.dart';
import '../tema.dart';
import 'comunes.dart';

class PantallaClientes extends ConsumerStatefulWidget {
  const PantallaClientes({super.key});

  @override
  ConsumerState<PantallaClientes> createState() => _PantallaClientesState();
}

class _PantallaClientesState extends ConsumerState<PantallaClientes> {
  String _busqueda = '';

  @override
  Widget build(BuildContext context) {
    final clientes = ref.watch(clientesProvider);

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: TextField(
            decoration: const InputDecoration(
              prefixIcon: Icon(Icons.search),
              hintText: 'Buscar por nombre, documento o teléfono',
            ),
            onChanged: (valor) => setState(() => _busqueda = valor),
          ),
        ),
        Expanded(
          child: clientes.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () => ref.invalidate(clientesProvider),
            ),
            data: (lista) {
              final texto = _busqueda.toLowerCase();
              final visibles = lista.where((cliente) {
                final campos = [
                  cliente.nombreCompleto,
                  cliente.razonSocial ?? '',
                  cliente.documentoIdentidad ?? '',
                  cliente.telefono ?? '',
                ].join(' ').toLowerCase();
                return campos.contains(texto);
              }).toList();

              if (visibles.isEmpty) {
                return const ListaVacia(
                  mensaje: 'No hay clientes que coincidan con la búsqueda.',
                );
              }

              return RefreshIndicator(
                onRefresh: () async => ref.invalidate(clientesProvider),
                child: ListView.builder(
                  itemCount: visibles.length,
                  itemBuilder: (context, indice) {
                    final cliente = visibles[indice];
                    return Card(
                      child: ListTile(
                        title: Text(
                          cliente.nombreCompleto,
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                        subtitle: Text(
                          [
                            cliente.documentoIdentidad,
                            cliente.telefono,
                          ].whereType<String>().join(' · '),
                          style: const TextStyle(color: Marca.textoSecundario),
                        ),
                        trailing: const Icon(Icons.chevron_right),
                        onTap: () => context.go('/clientes/${cliente.id}'),
                      ),
                    );
                  },
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}
