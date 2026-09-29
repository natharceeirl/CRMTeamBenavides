import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/estados.dart';
import '../auth/permisos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

class PantallaClienteDetalle extends ConsumerWidget {
  const PantallaClienteDetalle({required this.clienteId, super.key});

  final String clienteId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sesion = ref.watch(sesionProvider);
    // El vendedor ve clientes pero no unidades ni órdenes: esas secciones no se
    // piden para no recibir 403.
    final veUnidades = sesion.tienePermiso(Permisos.unidadesVer);
    final veOrdenes = sesion.tieneAlgunPermiso(Permisos.verOrdenes);
    final cliente = ref.watch(clienteProvider(clienteId));

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
                    _Dato('Documento', datos.documento),
                    _Dato('Teléfono', datos.telefono),
                    _Dato('Correo', datos.email),
                    _Dato('Dirección', datos.direccion),
                    _Dato('Razón social', datos.razonSocial),
                    _Dato('Observaciones', datos.observaciones),
                  ],
                ),
              ),
            ),
            if (veUnidades) ...[
              const _Titulo('Unidades'),
              ref.watch(vehiculosProvider(clienteId)).when(
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
                                  '${vehiculo.tipoNombre} · ${vehiculo.identificador}',
                                  style: const TextStyle(color: Marca.textoSecundario),
                                ),
                                trailing: Text(
                                  vehiculo.lectura,
                                  style: const TextStyle(color: Marca.textoSecundario),
                                ),
                              ),
                            ),
                        ],
                      );
                    },
                  ),
            ],
            if (veOrdenes) ...[
              const _Titulo('Órdenes de servicio'),
              ref.watch(ordenesClienteProvider(clienteId)).when(
                    loading: () => const Padding(
                      padding: EdgeInsets.all(24),
                      child: Center(child: CircularProgressIndicator()),
                    ),
                    error: (error, _) => AvisoError(
                      error: error,
                      alReintentar: () => ref.invalidate(ordenesClienteProvider(clienteId)),
                    ),
                    data: (lista) {
                      if (lista.isEmpty) {
                        return const Padding(
                          padding: EdgeInsets.all(16),
                          child: ListaVacia(
                            mensaje: 'Este cliente no tiene órdenes de servicio.',
                          ),
                        );
                      }

                      return Column(
                        children: [
                          for (final orden in lista)
                            Card(
                              child: ListTile(
                                title: Text(
                                  orden.unidadConPlaca,
                                  style: const TextStyle(fontWeight: FontWeight.w600),
                                ),
                                subtitle: Text(
                                  '${orden.referencia} · ${nombreEstadoOrden(orden.estadoId)} · '
                                  '${fechaHora(orden.fechaIngreso ?? orden.fechaApertura)}',
                                  style: const TextStyle(color: Marca.textoSecundario),
                                ),
                                trailing: const Icon(Icons.chevron_right),
                                onTap: () => context.go('/ordenes/${orden.id}'),
                              ),
                            ),
                        ],
                      );
                    },
                  ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Titulo extends StatelessWidget {
  const _Titulo(this.texto);

  final String texto;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 4),
      child: Text(
        texto,
        style: const TextStyle(
          fontFamily: Marca.fuenteTitulos,
          fontSize: 18,
          fontWeight: FontWeight.w700,
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
