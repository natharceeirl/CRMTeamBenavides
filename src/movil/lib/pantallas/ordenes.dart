import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/estados.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

class PantallaOrdenes extends ConsumerStatefulWidget {
  const PantallaOrdenes({super.key});

  @override
  ConsumerState<PantallaOrdenes> createState() => _PantallaOrdenesState();
}

class _PantallaOrdenesState extends ConsumerState<PantallaOrdenes> {
  String _busqueda = '';
  bool _soloEnTaller = true;

  @override
  Widget build(BuildContext context) {
    final ordenes = ref.watch(ordenesProvider);

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
          child: TextField(
            decoration: const InputDecoration(
              prefixIcon: Icon(Icons.search),
              hintText: 'Buscar por placa, cliente o referencia',
            ),
            onChanged: (valor) => setState(() => _busqueda = valor),
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16),
          child: Row(
            children: [
              FilterChip(
                label: const Text('Solo en taller'),
                selected: _soloEnTaller,
                onSelected: (valor) => setState(() => _soloEnTaller = valor),
              ),
            ],
          ),
        ),
        Expanded(
          child: ordenes.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () => ref.invalidate(ordenesProvider),
            ),
            data: (lista) {
              final texto = _busqueda.toLowerCase();
              final visibles = lista.where((orden) {
                if (_soloEnTaller && !estaEnTaller(orden.estadoId)) {
                  return false;
                }
                final campos = [
                  orden.referencia,
                  orden.vehiculoPlaca,
                  orden.unidad,
                  orden.clienteNombre,
                  orden.tecnicoNombre ?? '',
                ].join(' ').toLowerCase();
                return campos.contains(texto);
              }).toList();

              if (visibles.isEmpty) {
                return const ListaVacia(mensaje: 'No hay órdenes que coincidan.');
              }

              return RefreshIndicator(
                onRefresh: () async => ref.invalidate(ordenesProvider),
                child: ListView.builder(
                  itemCount: visibles.length,
                  itemBuilder: (context, indice) {
                    final orden = visibles[indice];
                    return Card(
                      child: ListTile(
                        title: Text(
                          '${orden.unidad} · ${orden.vehiculoPlaca}',
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                        subtitle: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              orden.clienteNombre,
                              style: const TextStyle(color: Marca.textoSecundario),
                            ),
                            const SizedBox(height: 4),
                            Row(
                              children: [
                                _EtiquetaEstado(estadoId: orden.estadoId),
                                const SizedBox(width: 8),
                                Text(
                                  fechaHora(orden.fechaApertura),
                                  style: const TextStyle(
                                    color: Marca.textoSecundario,
                                    fontSize: 12,
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                        isThreeLine: true,
                        trailing: const Icon(Icons.chevron_right),
                        onTap: () => context.go('/ordenes/${orden.id}'),
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

class _EtiquetaEstado extends StatelessWidget {
  const _EtiquetaEstado({required this.estadoId});

  final int estadoId;

  @override
  Widget build(BuildContext context) {
    final terminal = esEstadoTerminal(estadoId);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: terminal ? Marca.borde : Marca.acento.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        nombreEstadoOrden(estadoId),
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: terminal ? Marca.textoSecundario : Marca.acento,
        ),
      ),
    );
  }
}
