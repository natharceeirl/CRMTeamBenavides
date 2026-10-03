import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/permisos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'agregar_item.dart';
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

  Future<void> _agregarItem() async {
    final agregado = await abrirAgregarItem(context, widget.ordenId);
    if (agregado != true || !mounted) return;

    _refrescar();
    // El stock bajó: la tienda debe verlo al volver.
    ref.invalidate(productosProvider);
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Agregado a la orden')),
    );
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

  /// Pide la observación, ejecuta la acción y avisa. El motivo obligatorio queda
  /// en el historial para quien retome la orden.
  Future<void> _conObservacion({
    required String titulo,
    required bool obligatoria,
    String? aviso,
    required Future<void> Function(String observacion) accion,
    required String exito,
  }) async {
    final observacion = await showDialog<String>(
      context: context,
      builder: (_) => _DialogoObservacion(titulo: titulo, obligatoria: obligatoria, aviso: aviso),
    );
    if (observacion == null) {
      return;
    }

    setState(() {
      _error = null;
      _guardando = true;
    });

    try {
      await accion(observacion);
      _refrescar();

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(exito)));
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

  Future<void> _cambiarEstado(int destino) {
    final anulando = destino == EstadoOrden.cancelada;
    return _conObservacion(
      titulo: anulando ? 'Anular la orden' : 'Pasar a ${nombreEstadoOrden(destino)}',
      obligatoria: anulando,
      aviso: anulando ? 'Los repuestos asignados vuelven al stock.' : null,
      accion: (observacion) => ref
          .read(apiProvider)
          .cambiarEstado(widget.ordenId, destino, observaciones: observacion),
      exito: 'La orden pasó a «${nombreEstadoOrden(destino)}»',
    );
  }

  Future<void> _responderPresupuesto(int estado, {required bool esCliente}) {
    final aprueba = estado == EstadoPresupuesto.aprobado;
    final String titulo;
    if (esCliente) {
      titulo = aprueba ? 'Aprobar el presupuesto' : 'Rechazar el presupuesto';
    } else {
      titulo = aprueba ? 'El cliente aprobó el presupuesto' : 'El cliente rechazó el presupuesto';
    }
    return _conObservacion(
      titulo: titulo,
      obligatoria: !aprueba,
      accion: (observacion) => ref
          .read(apiProvider)
          .responderPresupuesto(widget.ordenId, estado, observaciones: observacion),
      exito: aprueba ? 'Presupuesto aprobado' : 'Presupuesto rechazado',
    );
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
          final bloqueoAprobacion = motivoBloqueoAprobacion(
            presupuesto: datosOrden.estadoPresupuestoId,
            gerencia: datosOrden.estadoGerenciaId,
          );
          final posibles = sesion.tienePermiso(Permisos.ordenesCambiarEstado)
              ? (transicionesOrden[datosOrden.estadoId] ?? const <int>[])
                  .where((destino) =>
                      !sesion.soloTecnico || !estadosVedadosAlTecnico.contains(destino))
                  .toList()
              : const <int>[];
          // «Aprobada» no se ofrece si el presupuesto o Gerencia lo impiden: se
          // explica el motivo en su lugar.
          final destinos = bloqueoAprobacion == null
              ? posibles
              : posibles.where((destino) => destino != EstadoOrden.aprobada).toList();
          final aprobadaBloqueada = bloqueoAprobacion != null &&
              posibles.contains(EstadoOrden.aprobada);

          final esCliente = sesion.soloCliente;

          return ListView(
            padding: const EdgeInsets.only(bottom: 32),
            children: [
              _FichaOrden(datos: datos),
              if (esCliente) _AvanceOrden(datos: datos),
              _Aprobaciones(
                orden: datosOrden,
                ocupado: _guardando,
                puedeResponder: sesion.tieneAlgunPermiso(Permisos.responderPresupuesto),
                esCliente: sesion.soloCliente,
                alResponder: (estado) =>
                    _responderPresupuesto(estado, esCliente: sesion.soloCliente),
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                  child: Text(_error!, style: const TextStyle(color: Marca.acento)),
                ),
              if (aprobadaBloqueada)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                  child: Text(
                    'No puede pasar a «Aprobada»: $bloqueoAprobacion',
                    style: const TextStyle(color: Marca.textoSecundario),
                  ),
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
              if (esCliente)
                _DiagnosticoCliente(orden: datosOrden)
              else
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
              _Titulo(esCliente ? 'Presupuesto' : 'Trabajos y repuestos'),
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
                    // Lo pagado lo ve el cliente y quien vende; el técnico no.
                    if (datos.total > 0 &&
                        (esCliente || sesion.tieneAlgunPermiso(const [Permisos.ventasVer, Permisos.ventasCrear]))) ...[
                      const SizedBox(height: 8),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Text('Pagado', style: TextStyle(color: Marca.textoSecundario)),
                          Text(soles(datosOrden.totalPagado)),
                        ],
                      ),
                      const SizedBox(height: 4),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Text('Saldo', style: TextStyle(fontWeight: FontWeight.w600)),
                          Text(
                            soles(datosOrden.saldo ?? datos.total),
                            style: TextStyle(
                              fontWeight: FontWeight.w700,
                              color: (datosOrden.saldo ?? datos.total) > 0 ? Marca.acento : null,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
              if (sesion.tienePermiso(Permisos.ordenesAgregarItems) &&
                  permiteEditarItems(datosOrden.estadoId, liquidada: datosOrden.ventaId != null))
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
                  child: OutlinedButton.icon(
                    style: OutlinedButton.styleFrom(minimumSize: const Size(0, 44)),
                    icon: const Icon(Icons.add),
                    label: const Text('Agregar repuesto o servicio'),
                    onPressed: _guardando ? null : _agregarItem,
                  ),
                )
              else if (datosOrden.estadoId == EstadoOrden.lista &&
                  sesion.tienePermiso(Permisos.ordenesAgregarItems))
                const Padding(
                  padding: EdgeInsets.fromLTRB(20, 16, 20, 0),
                  child: Text(
                    'Para agregar trabajos, la orden debe volver a «En proceso».',
                    style: TextStyle(color: Marca.textoSecundario),
                  ),
                ),
              // El cliente sigue el avance arriba; el historial detallado es del personal.
              if (!esCliente) ...[
                const _Titulo('Historial'),
                if (datos.historial.isEmpty)
                  const ListaVacia(mensaje: 'Sin cambios de estado registrados.')
                else
                  ...datos.historial.map((cambio) => _CambioDeEstado(cambio: cambio)),
              ],
            ],
          );
        },
      ),
    );
  }
}

/// Avance de la orden como lo ve el cliente: de «Recibida» a «Entregada».
class _AvanceOrden extends StatelessWidget {
  const _AvanceOrden({required this.datos});

  final OrdenServicioDetalleApi datos;

  @override
  Widget build(BuildContext context) {
    final orden = datos.orden;
    if (orden.estadoId == EstadoOrden.cancelada) {
      return const Padding(
        padding: EdgeInsets.fromLTRB(16, 16, 16, 0),
        child: Text('Esta orden fue anulada.', style: TextStyle(color: Marca.acento, fontWeight: FontWeight.w600)),
      );
    }

    final pasos = pasosDeAvance(
      estadoId: orden.estadoId,
      cambios: [
        for (final cambio in datos.historial)
          if (cambio.cambiaEstado) (estado: cambio.estadoNuevoId, fecha: cambio.fechaCambio),
      ],
      fechaIngreso: orden.fechaIngreso,
      fechaEstimadaEntrega: orden.fechaEstimadaEntrega,
    );

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _Titulo('Avance'),
          for (final paso in pasos)
            Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    switch (paso.situacion) {
                      SituacionPaso.hecho => Icons.check_circle,
                      SituacionPaso.actual => Icons.radio_button_checked,
                      SituacionPaso.pendiente => Icons.radio_button_unchecked,
                    },
                    size: 20,
                    color: paso.situacion == SituacionPaso.pendiente ? Marca.borde : Marca.acento,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          paso.titulo,
                          style: TextStyle(
                            fontWeight: paso.situacion == SituacionPaso.actual ? FontWeight.w700 : FontWeight.w500,
                            color: paso.situacion == SituacionPaso.pendiente ? Marca.textoSecundario : Marca.texto,
                          ),
                        ),
                        if (paso.fecha != null)
                          Text(
                            paso.estimada ? 'Estimado ${fechaHora(paso.fecha)}' : fechaHora(paso.fecha),
                            style: const TextStyle(color: Marca.textoSecundario, fontSize: 12),
                          ),
                      ],
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

/// Lo que encontró el técnico, en lectura: el cliente no edita el diagnóstico.
class _DiagnosticoCliente extends StatelessWidget {
  const _DiagnosticoCliente({required this.orden});

  final OrdenServicioApi orden;

  @override
  Widget build(BuildContext context) {
    final diagnostico = orden.diagnostico?.trim() ?? '';
    final solucion = orden.solucion?.trim() ?? '';

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: diagnostico.isEmpty && solucion.isEmpty
          ? const Text(
              'El técnico todavía no registra el diagnóstico.',
              style: TextStyle(color: Marca.textoSecundario),
            )
          : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (diagnostico.isNotEmpty) Text(diagnostico),
                if (solucion.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text('Solución: $solucion', style: const TextStyle(color: Marca.textoSecundario)),
                ],
              ],
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
          Padding(
            padding: const EdgeInsets.only(top: 5),
            child: Icon(
              Icons.circle,
              size: 10,
              color: cambio.cambiaEstado ? Marca.acento : Marca.borde,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Las aprobaciones y la asignación de técnico se anotan sin
                // cambiar el estado: se muestra la anotación como título.
                Text(
                  cambio.cambiaEstado
                      ? nombreEstadoOrden(cambio.estadoNuevoId)
                      : (cambio.observaciones ?? 'Actualización'),
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                Text(
                  '${fechaHora(cambio.fechaCambio)} · ${cambio.usuarioNombre ?? 'Sistema'}',
                  style: const TextStyle(color: Marca.textoSecundario, fontSize: 12),
                ),
                if (cambio.cambiaEstado &&
                    cambio.observaciones != null &&
                    cambio.observaciones!.isNotEmpty)
                  Text(cambio.observaciones!),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Respuesta del cliente al presupuesto y decisión de Gerencia. El cliente
/// responde desde aquí; Gerencia decide desde la web.
class _Aprobaciones extends StatelessWidget {
  const _Aprobaciones({
    required this.orden,
    required this.ocupado,
    required this.puedeResponder,
    required this.esCliente,
    required this.alResponder,
  });

  final OrdenServicioApi orden;
  final bool ocupado;
  final bool puedeResponder;
  final bool esCliente;
  final void Function(int estado) alResponder;

  @override
  Widget build(BuildContext context) {
    final presupuesto = orden.estadoPresupuestoId;
    final gerencia = orden.estadoGerenciaId;
    final respuestas =
        puedeResponder ? respuestasPresupuesto(orden.estadoId, presupuesto, orden.total ?? 0) : const <int>[];

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Aprobaciones',
              style: TextStyle(fontFamily: Marca.fuenteTitulos, fontSize: 16, fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 8),
            _Dato(
              'Presupuesto',
              [
                nombresEstadoPresupuesto[presupuesto],
                if (orden.fechaRespuestaCliente != null) fechaHora(orden.fechaRespuestaCliente),
                orden.observacionesPresupuesto,
              ].whereType<String>().where((texto) => texto.isNotEmpty).join(' · '),
            ),
            _Dato(
              'Gerencia',
              [
                nombresEstadoGerencia[gerencia],
                if (gerencia != EstadoGerencia.noAplica) orden.usuarioAprobacionGerencia,
                orden.observacionesGerencia,
              ].whereType<String>().where((texto) => texto.isNotEmpty).join(' · '),
            ),
            if (respuestas.isNotEmpty) ...[
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  if (respuestas.contains(EstadoPresupuesto.aprobado))
                    FilledButton(
                      style: FilledButton.styleFrom(minimumSize: const Size(0, 44)),
                      onPressed: ocupado ? null : () => alResponder(EstadoPresupuesto.aprobado),
                      child: Text(esCliente ? 'Aprobar presupuesto' : 'El cliente aprobó'),
                    ),
                  if (respuestas.contains(EstadoPresupuesto.rechazado))
                    OutlinedButton(
                      onPressed: ocupado ? null : () => alResponder(EstadoPresupuesto.rechazado),
                      child: Text(esCliente ? 'Rechazar' : 'El cliente rechazó'),
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Pide una observación para la acción. Devuelve null si se cancela.
class _DialogoObservacion extends StatefulWidget {
  const _DialogoObservacion({required this.titulo, required this.obligatoria, this.aviso});

  final String titulo;
  final bool obligatoria;
  final String? aviso;

  @override
  State<_DialogoObservacion> createState() => _DialogoObservacionState();
}

class _DialogoObservacionState extends State<_DialogoObservacion> {
  final _observacion = TextEditingController();

  @override
  void dispose() {
    _observacion.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.titulo),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (widget.aviso != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(widget.aviso!),
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
