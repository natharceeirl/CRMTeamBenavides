import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';
import 'ordenes.dart' show EtiquetaEstadoOrden;

/// Inicio del cliente (referencia visual: docs/referencias/app-cliente.html):
/// lo que debe, sus órdenes en curso y sus unidades. El backend ya filtra todo
/// a lo suyo.
class PantallaInicioCliente extends ConsumerWidget {
  const PantallaInicioCliente({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final resumen = ref.watch(portalResumenProvider);
    final ordenes = ref.watch(ordenesProvider);
    final unidades = ref.watch(vehiculosProvider(null));
    final nombre = resumen.value?.clienteNombre ?? ref.watch(sesionProvider).usuario?.nombre ?? '';

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(portalResumenProvider);
        ref.invalidate(ordenesProvider);
        ref.invalidate(vehiculosProvider(null));
      },
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
        children: [
          Text(
            nombre.isEmpty ? 'Hola' : 'Hola, ${nombre.split(' ').first}',
            style: const TextStyle(fontFamily: Marca.fuenteTitulos, fontSize: 24, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 12),
          resumen.when(
            loading: () => const SizedBox(height: 72),
            error: (error, _) => AvisoError(error: error, alReintentar: () => ref.invalidate(portalResumenProvider)),
            data: (datos) => Row(
              children: [
                _Dato(etiqueta: 'En taller', valor: '${datos.ordenesActivas}'),
                const SizedBox(width: 8),
                _Dato(
                  etiqueta: 'Por responder',
                  valor: '${datos.presupuestosPendientes}',
                  alerta: datos.presupuestosPendientes > 0,
                ),
                const SizedBox(width: 8),
                _Dato(etiqueta: 'Saldo', valor: soles(datos.saldoPendiente), alerta: datos.saldoPendiente > 0),
              ],
            ),
          ),
          const _Titulo('Orden en curso'),
          ordenes.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(error: error, alReintentar: () => ref.invalidate(ordenesProvider)),
            data: (lista) {
              final enCurso = lista.where((orden) => !esEstadoTerminal(orden.estadoId)).toList();
              if (enCurso.isEmpty) {
                return const Padding(
                  padding: EdgeInsets.symmetric(vertical: 8),
                  child: Text('No tienes unidades en el taller.', style: TextStyle(color: Marca.textoSecundario)),
                );
              }
              return Column(children: [for (final orden in enCurso) _TarjetaOrden(orden: orden)]);
            },
          ),
          const _Titulo('Tus unidades'),
          unidades.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(error: error, alReintentar: () => ref.invalidate(vehiculosProvider(null))),
            data: (lista) => lista.isEmpty
                ? const Text('Todavía no hay unidades registradas.', style: TextStyle(color: Marca.textoSecundario))
                : Column(
                    children: [
                      for (final unidad in lista)
                        Card(
                          child: ListTile(
                            title: Text('${unidad.marca} ${unidad.modelo}${unidad.anio == null ? '' : ' · ${unidad.anio}'}'),
                            subtitle: Text(
                              '${unidad.tipoNombre} · ${unidad.identificador}${unidad.lectura == '—' ? '' : ' · ${unidad.lectura}'}',
                              style: const TextStyle(color: Marca.textoSecundario),
                            ),
                            trailing: const Icon(Icons.history),
                            onTap: () => abrirHistorialDeServicio(context, unidad),
                          ),
                        ),
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}

class _TarjetaOrden extends StatelessWidget {
  const _TarjetaOrden({required this.orden});

  final OrdenServicioApi orden;

  @override
  Widget build(BuildContext context) {
    // El presupuesto se responde cuando ya hay diagnóstico y la orden no avanzó.
    final porResponder = orden.estadoPresupuestoId == EstadoPresupuesto.pendiente &&
        orden.estadoId == EstadoOrden.diagnostico &&
        (orden.total ?? 0) > 0;

    return Card(
      child: InkWell(
        onTap: () => context.go('/ordenes/${orden.id}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Text(orden.referencia, style: const TextStyle(fontWeight: FontWeight.w700)),
                  const Spacer(),
                  EtiquetaEstadoOrden(estadoId: orden.estadoId),
                ],
              ),
              const SizedBox(height: 6),
              Text('${orden.vehiculoMarca} ${orden.vehiculoModelo}'
                  '${orden.vehiculoPlaca.isEmpty ? '' : ' · ${orden.vehiculoPlaca}'}'),
              if (orden.fechaEstimadaEntrega != null)
                Text(
                  'Entrega estimada: ${fechaHora(orden.fechaEstimadaEntrega)}',
                  style: const TextStyle(color: Marca.textoSecundario),
                ),
              if (porResponder)
                const Padding(
                  padding: EdgeInsets.only(top: 8),
                  child: Text(
                    'Tu presupuesto espera respuesta',
                    style: TextStyle(color: Marca.acento, fontWeight: FontWeight.w600),
                  ),
                ),
              const SizedBox(height: 8),
              const Text('Ver avance', style: TextStyle(color: Marca.acentoBoton, fontWeight: FontWeight.w600)),
            ],
          ),
        ),
      ),
    );
  }
}

/// Los servicios anteriores de una unidad, con lo que se le hizo.
void abrirHistorialDeServicio(BuildContext context, VehiculoApi unidad) {
  showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (_) => _HojaHistorial(unidad: unidad),
  );
}

class _HojaHistorial extends ConsumerWidget {
  const _HojaHistorial({required this.unidad});

  final VehiculoApi unidad;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final historial = ref.watch(historialServicioProvider(unidad.id));

    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.7,
      maxChildSize: 0.95,
      builder: (context, control) => ListView(
        controller: control,
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          Text('${unidad.marca} ${unidad.modelo}', style: Theme.of(context).textTheme.titleLarge),
          Text(unidad.identificador, style: const TextStyle(color: Marca.textoSecundario)),
          const Divider(height: 24),
          historial.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () => ref.invalidate(historialServicioProvider(unidad.id)),
            ),
            data: (atenciones) => atenciones.isEmpty
                ? const ListaVacia(mensaje: 'Esta unidad todavía no tiene servicios.')
                : Column(
                    children: [
                      for (final atencion in atenciones)
                        ListTile(
                          contentPadding: EdgeInsets.zero,
                          title: Text(atencion.motivoFalla?.isNotEmpty ?? false
                              ? atencion.motivoFalla!
                              : (atencion.numeroOrden ?? 'Servicio')),
                          subtitle: Text(
                            [
                              atencion.numeroOrden,
                              fechaHora(atencion.fechaIngreso).split(' ').first,
                              nombreEstadoOrden(atencion.estadoId),
                              if (atencion.trabajos.isNotEmpty) atencion.trabajos.join(', '),
                            ].whereType<String>().join(' · '),
                            style: const TextStyle(color: Marca.textoSecundario),
                          ),
                          trailing: Text(soles(atencion.total), style: const TextStyle(fontWeight: FontWeight.w600)),
                          onTap: () {
                            Navigator.of(context).pop();
                            context.go('/ordenes/${atencion.ordenId}');
                          },
                        ),
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}

/// Comprobantes del cliente, del más reciente al más antiguo.
class PantallaDocumentos extends ConsumerWidget {
  const PantallaDocumentos({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final comprobantes = ref.watch(portalComprobantesProvider);

    return comprobantes.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => AvisoError(error: error, alReintentar: () => ref.invalidate(portalComprobantesProvider)),
      data: (lista) {
        if (lista.isEmpty) {
          return const ListaVacia(mensaje: 'Todavía no tienes comprobantes.');
        }
        final ordenados = [...lista]..sort((a, b) => b.fecha.compareTo(a.fecha));
        return RefreshIndicator(
          onRefresh: () async => ref.invalidate(portalComprobantesProvider),
          child: ListView.builder(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 96),
            itemCount: ordenados.length,
            itemBuilder: (context, indice) {
              final comprobante = ordenados[indice];
              final apagado = comprobante.anulado ? Marca.textoSecundario : null;
              return Card(
                child: ListTile(
                  title: Text(
                    comprobante.referencia,
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      color: apagado,
                      decoration: comprobante.anulado ? TextDecoration.lineThrough : null,
                    ),
                  ),
                  subtitle: Text(
                    [
                      fechaHora(comprobante.fecha).split(' ').first,
                      comprobante.numeroOrden,
                      if (comprobante.anulado) 'Anulado' else comprobante.metodoPago,
                    ].whereType<String>().join(' · '),
                    style: const TextStyle(color: Marca.textoSecundario),
                  ),
                  trailing: Text(soles(comprobante.total), style: TextStyle(fontWeight: FontWeight.w600, color: apagado)),
                ),
              );
            },
          ),
        );
      },
    );
  }
}

class _Dato extends StatelessWidget {
  const _Dato({required this.etiqueta, required this.valor, this.alerta = false});

  final String etiqueta;
  final String valor;
  final bool alerta;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Card(
        margin: EdgeInsets.zero,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                etiqueta.toUpperCase(),
                style: const TextStyle(fontSize: 10, letterSpacing: 1, fontWeight: FontWeight.w600, color: Marca.textoSecundario),
              ),
              const SizedBox(height: 6),
              FittedBox(
                fit: BoxFit.scaleDown,
                alignment: Alignment.centerLeft,
                child: Text(
                  valor,
                  style: TextStyle(
                    fontFamily: Marca.fuenteTitulos,
                    fontSize: 20,
                    fontWeight: FontWeight.w700,
                    color: alerta ? Marca.acento : Marca.texto,
                  ),
                ),
              ),
            ],
          ),
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
      padding: const EdgeInsets.only(top: 24, bottom: 8),
      child: Text(
        texto,
        style: const TextStyle(fontFamily: Marca.fuenteTitulos, fontSize: 18, fontWeight: FontWeight.w700),
      ),
    );
  }
}
