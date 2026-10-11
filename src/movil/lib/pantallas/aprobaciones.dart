import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

/// Bandeja de Gerencia: las solicitudes de precio pendientes, para aprobar o
/// rechazar desde el teléfono, y el historial de lo resuelto. El rechazo lleva
/// motivo, como en la web.
class PantallaAprobaciones extends ConsumerStatefulWidget {
  const PantallaAprobaciones({super.key});

  @override
  ConsumerState<PantallaAprobaciones> createState() => _PantallaAprobacionesState();
}

class _PantallaAprobacionesState extends ConsumerState<PantallaAprobaciones> {
  bool _verHistorial = false;
  String? _resolviendo;

  Future<void> _resolver(SolicitudAprobacionApi solicitud, int estado) async {
    final aprueba = estado == EstadoGerencia.aprobado;
    final observacion = await showDialog<String>(
      context: context,
      builder: (_) => _DialogoResolucion(aprueba: aprueba),
    );
    if (observacion == null || !mounted) return;

    setState(() => _resolviendo = solicitud.id);
    try {
      await ref.read(apiProvider).resolverAprobacion(solicitud.id, estado, observaciones: observacion);
      ref.invalidate(aprobacionesPendientesProvider);
      ref.invalidate(historialAprobacionesProvider);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(aprueba ? 'Solicitud aprobada' : 'Solicitud rechazada')),
        );
      }
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$error')));
      }
    } finally {
      if (mounted) setState(() => _resolviendo = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final lista = _verHistorial ? ref.watch(historialAprobacionesProvider) : ref.watch(aprobacionesPendientesProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Aprobaciones')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
            child: SegmentedButton<bool>(
              segments: const [
                ButtonSegment(value: false, label: Text('Pendientes')),
                ButtonSegment(value: true, label: Text('Historial')),
              ],
              selected: {_verHistorial},
              showSelectedIcon: false,
              onSelectionChanged: (seleccion) => setState(() => _verHistorial = seleccion.first),
            ),
          ),
          Expanded(
            child: lista.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => AvisoError(
                error: error,
                alReintentar: () => ref.invalidate(
                  _verHistorial ? historialAprobacionesProvider : aprobacionesPendientesProvider,
                ),
              ),
              data: (solicitudes) {
                // El historial trae también las pendientes: ahí solo va lo resuelto.
                final visibles = _verHistorial
                    ? solicitudes.where((s) => s.estadoId != EstadoGerencia.pendiente).toList()
                    : solicitudes;
                if (visibles.isEmpty) {
                  return ListaVacia(
                    mensaje: _verHistorial ? 'Todavía no hay solicitudes resueltas.' : 'No hay solicitudes por aprobar.',
                  );
                }
                return RefreshIndicator(
                  onRefresh: () => ref.refresh(
                    (_verHistorial ? historialAprobacionesProvider : aprobacionesPendientesProvider).future,
                  ),
                  child: ListView.builder(
                    padding: const EdgeInsets.only(bottom: 24),
                    itemCount: visibles.length,
                    itemBuilder: (context, indice) => _TarjetaSolicitud(
                      solicitud: visibles[indice],
                      ocupada: _resolviendo == visibles[indice].id,
                      alResolver: _verHistorial ? null : (estado) => _resolver(visibles[indice], estado),
                    ),
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _TarjetaSolicitud extends StatelessWidget {
  const _TarjetaSolicitud({required this.solicitud, required this.ocupada, this.alResolver});

  final SolicitudAprobacionApi solicitud;
  final bool ocupada;

  /// Null en el historial: lo resuelto ya no se decide.
  final void Function(int estado)? alResolver;

  @override
  Widget build(BuildContext context) {
    final resuelta = solicitud.estadoId != EstadoGerencia.pendiente;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    solicitud.nombreEntidad,
                    style: const TextStyle(fontFamily: Marca.fuenteTitulos, fontSize: 16, fontWeight: FontWeight.w700),
                  ),
                ),
                if (resuelta)
                  Text(
                    nombresEstadoGerencia[solicitud.estadoId] ?? '',
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      color: solicitud.estadoId == EstadoGerencia.aprobado ? Marca.texto : Marca.acento,
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 6),
            Text(solicitud.detalleCambio),
            const SizedBox(height: 6),
            Text(
              [
                if (solicitud.usuarioSolicitanteNombre != null) 'Pedida por ${solicitud.usuarioSolicitanteNombre}',
                fechaHora(solicitud.fechaSolicitud),
              ].join(' · '),
              style: const TextStyle(color: Marca.textoSecundario),
            ),
            if (solicitud.motivo != null && solicitud.motivo!.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text('Motivo: ${solicitud.motivo}', style: const TextStyle(color: Marca.textoSecundario)),
            ],
            if (resuelta) ...[
              const SizedBox(height: 4),
              Text(
                [
                  if (solicitud.usuarioAprobadorNombre != null) solicitud.usuarioAprobadorNombre!,
                  if (solicitud.fechaRespuesta != null) fechaHora(solicitud.fechaRespuesta),
                  if (solicitud.observacionesRespuesta != null && solicitud.observacionesRespuesta!.isNotEmpty)
                    solicitud.observacionesRespuesta!,
                ].join(' · '),
                style: const TextStyle(color: Marca.textoSecundario),
              ),
            ],
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                if (alResolver != null) ...[
                  FilledButton(
                    style: FilledButton.styleFrom(minimumSize: const Size(0, 44)),
                    onPressed: ocupada ? null : () => alResolver!(EstadoGerencia.aprobado),
                    child: const Text('Aprobar'),
                  ),
                  OutlinedButton(
                    style: OutlinedButton.styleFrom(minimumSize: const Size(0, 44)),
                    onPressed: ocupada ? null : () => alResolver!(EstadoGerencia.rechazado),
                    child: const Text('Rechazar'),
                  ),
                ],
                if (solicitud.entidad == 'OrdenServicio' && solicitud.entidadId.isNotEmpty)
                  TextButton(
                    onPressed: () => context.push('/ordenes/${solicitud.entidadId}'),
                    child: const Text('Ver orden'),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// Confirma la decisión. Rechazar exige motivo; aprobar admite una observación.
class _DialogoResolucion extends StatefulWidget {
  const _DialogoResolucion({required this.aprueba});

  final bool aprueba;

  @override
  State<_DialogoResolucion> createState() => _DialogoResolucionState();
}

class _DialogoResolucionState extends State<_DialogoResolucion> {
  final _texto = TextEditingController();

  @override
  void dispose() {
    _texto.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final valido = widget.aprueba || _texto.text.trim().isNotEmpty;

    return AlertDialog(
      title: Text(widget.aprueba ? 'Aprobar solicitud' : 'Rechazar solicitud'),
      content: TextField(
        controller: _texto,
        autofocus: true,
        maxLength: 500,
        maxLines: 3,
        onChanged: (_) => setState(() {}),
        decoration: InputDecoration(
          labelText: widget.aprueba ? 'Observación (opcional)' : 'Motivo del rechazo',
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Volver')),
        FilledButton(
          onPressed: valido ? () => Navigator.of(context).pop(_texto.text.trim()) : null,
          child: Text(widget.aprueba ? 'Aprobar' : 'Rechazar'),
        ),
      ],
    );
  }
}
