import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../auth/sesion.dart';
import '../tema.dart';
import 'comunes.dart';

class PantallaClienteDetalle extends ConsumerWidget {
  const PantallaClienteDetalle({required this.clienteId, super.key});

  final String clienteId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cliente = ref.watch(clienteProvider(clienteId));
    final vehiculos = ref.watch(vehiculosProvider(clienteId));

    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/'),
        ),
        title: Text(cliente.value?.nombreCompleto ?? 'Cliente'),
      ),
      body: cliente.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => AvisoError(
          error: error,
          alReintentar: () => ref.invalidate(clienteProvider(clienteId)),
        ),
        data: (datos) => ListView(
          padding: const EdgeInsets.only(bottom: 24),
          children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _Dato('Documento', datos.documentoIdentidad),
                    _Dato('Teléfono', datos.telefono),
                    _Dato('Correo', datos.email),
                    _Dato('Dirección', datos.direccion),
                    _Dato('Razón social', datos.razonSocial),
                    _Dato('Observaciones', datos.observaciones),
                  ],
                ),
              ),
            ),
            const Padding(
              padding: EdgeInsets.fromLTRB(20, 16, 20, 4),
              child: Text(
                'Unidades',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
              ),
            ),
            vehiculos.when(
              loading: () => const Padding(
                padding: EdgeInsets.all(24),
                child: Center(child: CircularProgressIndicator()),
              ),
              error: (error, _) => AvisoError(
                error: error,
                alReintentar: () => ref.invalidate(vehiculosProvider(clienteId)),
              ),
              data: (lista) {
                if (lista.isEmpty) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: ListaVacia(
                      mensaje: 'Este cliente todavía no tiene unidades.',
                    ),
                  );
                }

                return Column(
                  children: [
                    for (final vehiculo in lista)
                      Card(
                        child: ListTile(
                          title: Text(vehiculo.descripcion),
                          subtitle: Text(
                            'Placa ${vehiculo.placa}',
                            style: const TextStyle(
                              color: Marca.textoSecundario,
                            ),
                          ),
                          trailing: Text(
                            vehiculo.kilometraje == null
                                ? '—'
                                : '${vehiculo.kilometraje} km',
                            style: const TextStyle(
                              color: Marca.textoSecundario,
                            ),
                          ),
                        ),
                      ),
                  ],
                );
              },
            ),
            const Padding(
              padding: EdgeInsets.fromLTRB(20, 16, 20, 0),
              child: Text(
                'El historial de órdenes se conecta cuando salga esa API.',
                style: TextStyle(color: Marca.textoSecundario),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Dato extends StatelessWidget {
  const _Dato(this.etiqueta, this.valor);

  final String etiqueta;
  final String? valor;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 120,
            child: Text(
              etiqueta,
              style: const TextStyle(color: Marca.textoSecundario),
            ),
          ),
          Expanded(child: Text(valor ?? '—')),
        ],
      ),
    );
  }
}
