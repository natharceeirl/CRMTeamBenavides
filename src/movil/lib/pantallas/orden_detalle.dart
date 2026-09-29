import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/permisos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

class PantallaOrdenDetalle extends ConsumerStatefulWidget {
  const PantallaOrdenDetalle({required this.ordenId, super.key});

  final String ordenId;

  @override
  ConsumerState<PantallaOrdenDetalle> createState() => _PantallaOrdenDetalleState();
}

class _PantallaOrdenDetalleState extends ConsumerState<PantallaOrdenDetalle> {
  final _diagnostico = TextEditingController();
  final _solucion = TextEditingController();

  bool _cargadoDelServidor = false;
  bool _guardando = false;
  String? _error;

  @override
  void dispose() {
    _diagnostico.dispose();
    _solucion.dispose();
    super.dispose();
  }

  void _refrescar() {
    ref.invalidate(ordenProvider(widget.ordenId));
    ref.invalidate(ordenesProvider);
  }

  Future<void> _guardarDiagnostico() async {
    setState(() {
      _error = null;
      _guardando = true;
    });

    try {
      await ref.read(apiProvider).registrarDiagnostico(
            widget.ordenId,
            _diagnostico.text.trim(),
            solucion: _solucion.text.trim(),
          );
      _refrescar();

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Diagnóstico guardado')),
        );
      }
    } catch (fallo) {
      if (mounted) {
        setState(() => _error = '$fallo');
      }
    } finally {
      if (mounted) {
        setState(() => _guardando = false);
      }
    }
  }

  Future<void> _cambiarEstado(int destino) async {
    final anulando = destino == EstadoOrden.cancelada;
    final observacion = await showDialog<String>(
      context: context,
      builder: (_) => _DialogoCambioDeEstado(destino: destino, obligatoria: anulando),
    );
    if (observacion == null) {
      return;
    }

    setState(() {
      _error = null;
      _guardando = true;
    });

    try {
      await ref
          .read(apiProvider)
          .cambiarEstado(widget.ordenId, destino, observaciones: observacion);
      _refrescar();

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('La orden pasó a «${nombreEstadoOrden(destino)}»')),
        );
      }
    } catch (fallo) {
      if (mounted) {
        setState(() => _error = '$fallo');
      }
    } finally {
      if (mounted) {
        setState(() => _guardando = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final orden = ref.watch(ordenProvider(widget.ordenId));
    final sesion = ref.watch(sesionProvider);

    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/'),
        ),
        title: Text(orden.value?.orden.referencia ?? 'Orden'),
      ),
      body: orden.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => AvisoError(
          error: error,
          alReintentar: () => ref.invalidate(ordenProvider(widget.ordenId)),
        ),
        data: (datos) {
          final datosOrden = datos.orden;
          // El texto del servidor se carga una sola vez para no pisar lo que
          // el técnico esté escribiendo cuando la consulta se refresca.
          if (!_cargadoDelServidor) {
            _diagnostico.text = datosOrden.diagnostico ?? '';
            _solucion.text = datosOrden.solucion ?? '';
            _cargadoDelServidor = true;
          }

          final puedeDiagnosticar = sesion.tienePermiso(Permisos.ordenesDiagnostico) &&
              permiteDiagnostico(datosOrden.estadoId);
          final destinos = sesion.tienePermiso(Permisos.ordenesCambiarEstado)
              ? (transicionesOrden[datosOrden.estadoId] ?? const <int>[])
                  .where((destino) =>
                      !sesion.soloTecnico || !estadosVedadosAlTecnico.contains(destino))
                  .toList()
              : const <int>[];

          return ListView(
            padding: const EdgeInsets.only(bottom: 32),
            children: [
              _FichaOrden(datos: datos),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                  child: Text(_error!, style: const TextStyle(color: Marca.acento)),
                ),
              if (destinos.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                  child: Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (final destino in destinos)
                        destino == EstadoOrden.cancelada
                            ? OutlinedButton(
                                onPressed: _guardando ? null : () => _cambiarEstado(destino),
                                child: const Text('Anular'),
                              )
                            : FilledButton(
                                style: FilledButton.styleFrom(minimumSize: const Size(0, 44)),
                                onPressed: _guardando ? null : () => _cambiarEstado(destino),
                                child: Text('Pasar a ${nombreEstadoOrden(destino)}'),
                              ),
                    ],
                  ),
                ),
              const _Titulo('Diagnóstico'),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextField(
                      controller: _diagnostico,
                      maxLines: 4,
                      enabled: puedeDiagnosticar && !_guardando,
                      decoration: const InputDecoration(
                        hintText: 'Qué encontraste en la unidad',
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _solucion,
                      maxLines: 3,
                      enabled: puedeDiagnosticar && !_guardando,
                      decoration: const InputDecoration(
                        hintText: 'Solución propuesta o aplicada',
                      ),
                    ),
                    if (puedeDiagnosticar) ...[
                      const SizedBox(height: 12),
                      FilledButton(
                        onPressed: _guardando ? null : _guardarDiagnostico,
                        child: _guardando
                            ? const SizedBox(
                                height: 20,
                                width: 20,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white,
                                ),
                              )
                            : const Text('Guardar diagnóstico'),
                      ),
                    ],
                    if (esEstadoTerminal(datosOrden.estadoId))
                      const Padding(
                        padding: EdgeInsets.only(top: 8),
                        child: Text(
                          'La orden ya está cerrada y no admite cambios.',
                          style: TextStyle(color: Marca.textoSecundario),
                        ),
                      ),
                  ],
                ),
              ),
              const _Titulo('Trabajos y repuestos'),
              if (datos.detalles.isEmpty)
                const ListaVacia(mensaje: 'Todavía no hay trabajos ni repuestos.')
              else
                ...datos.detalles.map(
                  (detalle) => Card(
                    child: ListTile(
                      title: Text(detalle.descripcion),
                      subtitle: Text(
                        '${detalle.tipoItemNombre ?? (detalle.esRepuesto ? 'Repuesto' : 'Mano de obra')} · '
                        '${detalle.cantidad} x ${soles(detalle.precioUnitario)}'
                        '${detalle.tipoAfectacionIgvNombre != null ? ' (${detalle.tipoAfectacionIgvNombre})' : ''}',
                        style: const TextStyle(color: Marca.textoSecundario),
                      ),
                      trailing: Text(
                        soles(detalle.total ?? detalle.subtotal),
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                    ),
                  ),
                ),
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 0),
                child: Column(
                  children: [
                    if ((datos.subtotalGravado ?? 0) > 0 || (datos.montoIgv ?? 0) > 0) ...[
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Text('Subtotal gravado', style: TextStyle(color: Marca.textoSecundario)),
                          Text(soles(datos.subtotalGravado ?? 0)),
                        ],
                      ),
                      const SizedBox(height: 4),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            'IGV (${(datos.porcentajeIgv ?? 18).toStringAsFixed(0)} %)',
                            style: const TextStyle(color: Marca.textoSecundario),
                          ),
                          Text(soles(datos.montoIgv ?? 0)),
                        ],
                      ),
                      const Divider(height: 16),
                    ],
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        const Text('Total', style: TextStyle(fontWeight: FontWeight.w600)),
                        Text(
                          soles(datos.total),
                          style: const TextStyle(
                            fontFamily: Marca.fuenteTitulos,
                            fontSize: 18,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              if (sesion.esPersonal)
                const Padding(
                  padding: EdgeInsets.fromLTRB(20, 16, 20, 0),
                  child: Text(
                    'Los repuestos y la mano de obra se registran desde la web.',
                    style: TextStyle(color: Marca.textoSecundario),
                  ),
                ),
              const _Titulo('Historial'),
              if (datos.historial.isEmpty)
                const ListaVacia(mensaje: 'Sin cambios de estado registrados.')
              else
                ...datos.historial.map((cambio) => _CambioDeEstado(cambio: cambio)),
            ],
          );
        },
      ),
    );
  }
}

/// Unidad, cliente y datos de recepción.
class _FichaOrden extends StatelessWidget {
  const _FichaOrden({required this.datos});

  final OrdenServicioDetalleApi datos;

  @override
  Widget build(BuildContext context) {
    final orden = datos.orden;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              orden.unidadConPlaca,
              style: const TextStyle(
                fontFamily: Marca.fuenteTitulos,
                fontSize: 18,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 8),
            _Dato('Estado', nombreEstadoOrden(orden.estadoId)),
            _Dato('Cliente', orden.clienteNombre),
            _Dato('Documento', datos.clienteDocumento),
            _Dato('Teléfono', datos.clienteTelefono),
            _Dato('Técnico', orden.tecnicoNombre ?? 'Sin asignar'),
            _Dato('Serie', datos.numeroSerieVIN),
            _Dato('Ingreso', fechaHora(orden.fechaIngreso ?? orden.fechaApertura)),
            _Dato('Entrega est.', fechaHora(orden.fechaEstimadaEntrega)),
            _Dato(
              'Medidor',
              orden.lecturaIngreso ??
                  (datos.vehiculoKilometraje == null
                      ? null
                      : '${entero(datos.vehiculoKilometraje!)} km'),
            ),
            _Dato('Atención', nombresTipoAtencion[orden.tipoAtencionId]),
            _Dato('Falla', orden.motivoFalla),
            _Dato('Tipo de falla', nombresTipoFalla[orden.tipoFallaId]),
            _Dato('Observaciones', orden.observaciones),
          ],
        ),
      ),
    );
  }
}

class _CambioDeEstado extends StatelessWidget {
  const _CambioDeEstado({required this.cambio});

  final HistorialEstadoApi cambio;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 6, 20, 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Padding(
            padding: EdgeInsets.only(top: 5),
            child: Icon(Icons.circle, size: 10, color: Marca.acento),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  nombreEstadoOrden(cambio.estadoNuevoId),
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                Text(
                  '${fechaHora(cambio.fechaCambio)} · ${cambio.usuarioNombre ?? 'Sistema'}',
                  style: const TextStyle(color: Marca.textoSecundario, fontSize: 12),
                ),
                if (cambio.observaciones != null && cambio.observaciones!.isNotEmpty)
                  Text(cambio.observaciones!),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Pide la observación del cambio. Devuelve null si se cancela.
class _DialogoCambioDeEstado extends StatefulWidget {
  const _DialogoCambioDeEstado({required this.destino, required this.obligatoria});

  final int destino;
  final bool obligatoria;

  @override
  State<_DialogoCambioDeEstado> createState() => _DialogoCambioDeEstadoState();
}

class _DialogoCambioDeEstadoState extends State<_DialogoCambioDeEstado> {
  final _observacion = TextEditingController();

  @override
  void dispose() {
    _observacion.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final anulando = widget.destino == EstadoOrden.cancelada;

    return AlertDialog(
      title: Text(anulando ? 'Anular la orden' : 'Pasar a ${nombreEstadoOrden(widget.destino)}'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (anulando)
            const Padding(
              padding: EdgeInsets.only(bottom: 8),
              child: Text('Los repuestos asignados vuelven al stock.'),
            ),
          TextField(
            controller: _observacion,
            maxLines: 3,
            onChanged: (_) => setState(() {}),
            decoration: InputDecoration(
              hintText: widget.obligatoria ? 'Motivo (obligatorio)' : 'Observación (opcional)',
              helperText: 'Queda en el historial con tu usuario y la hora.',
            ),
          ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancelar'),
        ),
        FilledButton(
          style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
          onPressed: widget.obligatoria && _observacion.text.trim().isEmpty
              ? null
              : () => Navigator.of(context).pop(_observacion.text.trim()),
          child: const Text('Confirmar'),
        ),
      ],
    );
  }
}

class _Titulo extends StatelessWidget {
  const _Titulo(this.texto);

  final String texto;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 4),
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
    final texto = valor == null || valor!.isEmpty || valor == '—' ? null : valor;
    // Los datos vacíos no se muestran: la ficha queda corta en órdenes simples.
    if (texto == null && etiqueta != 'Estado') {
      return const SizedBox.shrink();
    }

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(
              etiqueta,
              style: const TextStyle(color: Marca.textoSecundario),
            ),
          ),
          Expanded(child: Text(texto ?? '—')),
        ],
      ),
    );
  }
}
