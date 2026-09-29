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
    // El cliente solo recibe sus unidades: no tiene sentido mostrarle el propietario.
    final soloCliente = ref.watch(sesionProvider).soloCliente;

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: TextField(
            decoration: const InputDecoration(
              prefixIcon: Icon(Icons.search),
              hintText: 'Buscar por tipo, modelo, placa, serie o propietario',
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
                  vehiculo.tipoNombre,
                  vehiculo.descripcion,
                  vehiculo.placa,
                  vehiculo.numeroSerieVIN ?? '',
                  vehiculo.numeroMotor ?? '',
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
                          soloCliente
                              ? '${vehiculo.tipoNombre} · ${vehiculo.identificador}'
                              : '${vehiculo.tipoNombre} · ${vehiculo.identificador} · '
                                  '${vehiculo.clienteNombre}',
                          style: const TextStyle(color: Marca.textoSecundario),
                        ),
                        trailing: Text(
                          vehiculo.lectura,
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
