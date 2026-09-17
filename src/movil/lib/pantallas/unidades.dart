import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/sesion.dart';
import '../tema.dart';
import 'comunes.dart';

class PantallaUnidades extends ConsumerStatefulWidget {
  const PantallaUnidades({super.key});

  @override
  ConsumerState<PantallaUnidades> createState() => _PantallaUnidadesState();
}

class _PantallaUnidadesState extends ConsumerState<PantallaUnidades> {
  String _busqueda = '';

  @override
  Widget build(BuildContext context) {
    final vehiculos = ref.watch(vehiculosProvider(null));

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: TextField(
            decoration: const InputDecoration(
              prefixIcon: Icon(Icons.search),
              hintText: 'Buscar por modelo, placa o propietario',
            ),
            onChanged: (valor) => setState(() => _busqueda = valor),
          ),
        ),
        Expanded(
          child: vehiculos.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () => ref.invalidate(vehiculosProvider(null)),
            ),
            data: (lista) {
              final texto = _busqueda.toLowerCase();
              final visibles = lista.where((vehiculo) {
                final campos = [
                  vehiculo.descripcion,
                  vehiculo.placa,
                  vehiculo.clienteNombre,
                ].join(' ').toLowerCase();
                return campos.contains(texto);
              }).toList();

              if (visibles.isEmpty) {
                return const ListaVacia(
                  mensaje: 'No hay unidades que coincidan con la búsqueda.',
                );
              }

              return RefreshIndicator(
                onRefresh: () async => ref.invalidate(vehiculosProvider(null)),
                child: ListView.builder(
                  itemCount: visibles.length,
                  itemBuilder: (context, indice) {
                    final vehiculo = visibles[indice];
                    return Card(
                      child: ListTile(
                        title: Text(
                          vehiculo.descripcion,
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                        subtitle: Text(
                          'Placa ${vehiculo.placa} · ${vehiculo.clienteNombre}',
                          style: const TextStyle(color: Marca.textoSecundario),
                        ),
                        trailing: Text(
                          vehiculo.kilometraje == null
                              ? '—'
                              : '${vehiculo.kilometraje} km',
                          style: const TextStyle(color: Marca.textoSecundario),
                        ),
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
