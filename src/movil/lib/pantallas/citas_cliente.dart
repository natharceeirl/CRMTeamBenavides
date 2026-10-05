import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/permisos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

/// Las citas en curso primero, de la más cercana a la más lejana; las cerradas
/// después, de la más reciente a la más antigua.
({List<CitaApi> proximas, List<CitaApi> anteriores}) separarCitas(List<CitaApi> citas) {
  final proximas = citas.where((cita) => !esCitaFinal(cita.estado)).toList()
    ..sort((una, otra) => una.fechaHoraProgramada.compareTo(otra.fechaHoraProgramada));
  final anteriores = citas.where((cita) => esCitaFinal(cita.estado)).toList()
    ..sort((una, otra) => otra.fechaHoraProgramada.compareTo(una.fechaHoraProgramada));
  return (proximas: proximas, anteriores: anteriores);
}

/// Lo que falta para poder agendar, o null si está todo. El backend no controla
/// la fecha pasada: lo hace la app.
String? faltaParaAgendar({
  required String? vehiculoId,
  required DateTime? fecha,
  required TimeOfDay? hora,
  required String motivo,
  required DateTime ahora,
}) {
  if (vehiculoId == null) return 'Elige la unidad.';
  if (fecha == null || hora == null) return 'Elige el día y la hora.';
  final programada = DateTime(fecha.year, fecha.month, fecha.day, hora.hour, hora.minute);
  if (programada.isBefore(ahora)) return 'La cita no puede quedar en el pasado.';
  if (motivo.trim().isEmpty) return 'Cuéntanos el motivo de la cita.';
  return null;
}

class PantallaCitasCliente extends ConsumerWidget {
  const PantallaCitasCliente({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final citas = ref.watch(citasProvider);
    final puedeAgendar = ref.watch(sesionProvider).tienePermiso(Permisos.citasCrear);

    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(citasProvider),
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
        children: [
          if (puedeAgendar)
            FilledButton.icon(
              style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
              onPressed: () => abrirAgendarCita(context),
              icon: const Icon(Icons.event_available),
              label: const Text('Agendar cita'),
            ),
          citas.when(
            loading: () => const Padding(
              padding: EdgeInsets.all(32),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => AvisoError(error: error, alReintentar: () => ref.invalidate(citasProvider)),
            data: (lista) {
              if (lista.isEmpty) {
                return const ListaVacia(mensaje: 'Todavía no tienes citas.');
              }
              final (:proximas, :anteriores) = separarCitas(lista);
              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (proximas.isNotEmpty) ...[
                    const _Titulo('Próximas'),
                    for (final cita in proximas) _TarjetaCita(cita: cita),
                  ],
                  if (anteriores.isNotEmpty) ...[
                    const _Titulo('Anteriores'),
                    for (final cita in anteriores) _TarjetaCita(cita: cita),
                  ],
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

class _TarjetaCita extends StatelessWidget {
  const _TarjetaCita({required this.cita});

  final CitaApi cita;

  @override
  Widget build(BuildContext context) {
    final cerrada = esCitaFinal(cita.estado);

    return Card(
      child: InkWell(
        onTap: () => _abrirDetalle(context, cita),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      fechaConDia(cita.fechaHoraProgramada),
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        color: cerrada ? Marca.textoSecundario : Marca.texto,
                      ),
                    ),
                  ),
                  EtiquetaEstadoCita(estado: cita.estado),
                ],
              ),
              const SizedBox(height: 6),
              Text(cita.unidad),
              Text(cita.motivo, style: const TextStyle(color: Marca.textoSecundario)),
            ],
          ),
        ),
      ),
    );
  }
}

class EtiquetaEstadoCita extends StatelessWidget {
  const EtiquetaEstadoCita({required this.estado, super.key});

  final String estado;

  @override
  Widget build(BuildContext context) {
    final cerrada = esCitaFinal(estado);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: cerrada ? Marca.borde : Marca.acento.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        nombresEstadoCita[estado] ?? 'Desconocido',
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: cerrada ? Marca.textoSecundario : Marca.acento,
        ),
      ),
    );
  }
}

void _abrirDetalle(BuildContext context, CitaApi cita) {
  showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (_) => _HojaCita(cita: cita),
  );
}

/// Detalle de la cita. Se cancela con motivo mientras la unidad no haya llegado al taller.
class _HojaCita extends ConsumerStatefulWidget {
  const _HojaCita({required this.cita});

  final CitaApi cita;

  @override
  ConsumerState<_HojaCita> createState() => _HojaCitaState();
}

class _HojaCitaState extends ConsumerState<_HojaCita> {
  final _motivo = TextEditingController();
  bool _cancelando = false;
  bool _guardando = false;
  String? _error;

  @override
  void dispose() {
    _motivo.dispose();
    super.dispose();
  }

  Future<void> _cancelar() async {
    setState(() {
      _guardando = true;
      _error = null;
    });
    try {
      await ref.read(apiProvider).cancelarCita(widget.cita.id, _motivo.text.trim());
      ref.invalidate(citasProvider);
      if (mounted) {
        Navigator.of(context).pop();
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Cita cancelada')));
      }
    } on Exception catch (error) {
      setState(() => _error = '$error');
    } finally {
      if (mounted) {
        setState(() => _guardando = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final cita = widget.cita;
    final puedeCancelar =
        ref.watch(sesionProvider).tienePermiso(Permisos.citasCancelar) && citaAntesDelTaller(cita.estado);

    return Padding(
      padding: EdgeInsets.fromLTRB(20, 16, 20, 24 + MediaQuery.viewInsetsOf(context).bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  fechaConDia(cita.fechaHoraProgramada),
                  style: const TextStyle(fontFamily: Marca.fuenteTitulos, fontSize: 18, fontWeight: FontWeight.w700),
                ),
              ),
              EtiquetaEstadoCita(estado: cita.estado),
            ],
          ),
          const SizedBox(height: 4),
          Text(cita.numeroCita, style: const TextStyle(color: Marca.textoSecundario)),
          const Divider(height: 24),
          Text(cita.unidad, style: const TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 4),
          Text(cita.motivo),
          if (puedeCancelar) ...[
            const SizedBox(height: 20),
            if (_cancelando) ...[
              TextField(
                controller: _motivo,
                autofocus: true,
                onChanged: (_) => setState(() {}),
                maxLength: 500,
                decoration: const InputDecoration(labelText: 'Motivo de la cancelación'),
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Text(_error!, style: const TextStyle(color: Marca.acento)),
                ),
              FilledButton(
                style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                onPressed: _motivo.text.trim().isEmpty || _guardando ? null : _cancelar,
                child: _guardando
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : const Text('Cancelar cita'),
              ),
            ] else
              OutlinedButton(
                style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                onPressed: () => setState(() => _cancelando = true),
                child: const Text('Cancelar cita'),
              ),
          ],
        ],
      ),
    );
  }
}

/// Abre la hoja para agendar una cita. Devuelve true si se agendó.
Future<bool?> abrirAgendarCita(BuildContext context) {
  return showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (_) => const HojaAgendarCita(),
  );
}

class HojaAgendarCita extends ConsumerStatefulWidget {
  const HojaAgendarCita({super.key});

  @override
  ConsumerState<HojaAgendarCita> createState() => _HojaAgendarCitaState();
}

class _HojaAgendarCitaState extends ConsumerState<HojaAgendarCita> {
  final _motivo = TextEditingController();
  final _observaciones = TextEditingController();
  String? _vehiculoId;
  DateTime? _fecha;
  TimeOfDay? _hora;
  bool _guardando = false;
  String? _error;

  @override
  void dispose() {
    _motivo.dispose();
    _observaciones.dispose();
    super.dispose();
  }

  Future<void> _elegirFecha() async {
    final hoy = DateUtils.dateOnly(DateTime.now());
    final elegida = await showDatePicker(
      context: context,
      initialDate: _fecha ?? hoy,
      firstDate: hoy,
      lastDate: hoy.add(const Duration(days: 90)),
      helpText: 'Día de la cita',
    );
    if (elegida != null) {
      setState(() => _fecha = elegida);
    }
  }

  Future<void> _elegirHora() async {
    final elegida = await showTimePicker(
      context: context,
      initialTime: _hora ?? const TimeOfDay(hour: 9, minute: 0),
      helpText: 'Hora de la cita',
    );
    if (elegida != null) {
      setState(() => _hora = elegida);
    }
  }

  Future<void> _agendar() async {
    final falta = faltaParaAgendar(
      vehiculoId: _vehiculoId,
      fecha: _fecha,
      hora: _hora,
      motivo: _motivo.text,
      ahora: DateTime.now(),
    );
    if (falta != null) {
      setState(() => _error = falta);
      return;
    }

    setState(() {
      _guardando = true;
      _error = null;
    });
    try {
      final fecha = _fecha!;
      await ref.read(apiProvider).agendarCita(
            vehiculoId: _vehiculoId!,
            fechaHora: DateTime(fecha.year, fecha.month, fecha.day, _hora!.hour, _hora!.minute),
            motivo: _motivo.text.trim(),
            observaciones: _observaciones.text.trim(),
          );
      ref.invalidate(citasProvider);
      if (mounted) {
        Navigator.of(context).pop(true);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Cita agendada. El taller te la confirmará.')),
        );
      }
    } on Exception catch (error) {
      setState(() => _error = '$error');
    } finally {
      if (mounted) {
        setState(() => _guardando = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final unidades = ref.watch(vehiculosProvider(null));

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: ListView(
        shrinkWrap: true,
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          Row(
            children: [
              const Expanded(
                child: Text(
                  'Agendar cita',
                  style: TextStyle(fontFamily: Marca.fuenteTitulos, fontSize: 18, fontWeight: FontWeight.w700),
                ),
              ),
              IconButton(
                tooltip: 'Cerrar',
                icon: const Icon(Icons.close),
                onPressed: () => Navigator.of(context).pop(),
              ),
            ],
          ),
          const SizedBox(height: 8),
          unidades.when(
            loading: () => const LinearProgressIndicator(),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () => ref.invalidate(vehiculosProvider(null)),
            ),
            data: (lista) {
              final activas = lista.where((unidad) => unidad.activo).toList();
              if (activas.isEmpty) {
                return const Text(
                  'No tienes unidades registradas. Pide al taller que las registre.',
                  style: TextStyle(color: Marca.textoSecundario),
                );
              }
              return DropdownButtonFormField<String>(
                initialValue: _vehiculoId,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Unidad'),
                items: [
                  for (final unidad in activas)
                    DropdownMenuItem(
                      value: unidad.id,
                      child: Text('${unidad.marca} ${unidad.modelo} · ${unidad.identificador}',
                          overflow: TextOverflow.ellipsis),
                    ),
                ],
                onChanged: (valor) => setState(() => _vehiculoId = valor),
              );
            },
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                  onPressed: _elegirFecha,
                  icon: const Icon(Icons.calendar_today_outlined),
                  label: Text(_fecha == null ? 'Día' : dia(_fecha!)),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                  onPressed: _elegirHora,
                  icon: const Icon(Icons.schedule),
                  label: Text(_hora == null ? 'Hora' : horaMinuto(_hora!.hour, _hora!.minute)),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _motivo,
            maxLength: 500,
            maxLines: 3,
            minLines: 2,
            decoration: const InputDecoration(
              labelText: 'Motivo',
              hintText: 'Mantenimiento, revisión, un ruido al frenar…',
            ),
          ),
          TextField(
            controller: _observaciones,
            maxLength: 500,
            decoration: const InputDecoration(labelText: 'Observaciones (opcional)'),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(_error!, style: const TextStyle(color: Marca.acento)),
            ),
          FilledButton(
            style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
            onPressed: _guardando ? null : _agendar,
            child: _guardando
                ? const SizedBox(
                    height: 20,
                    width: 20,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                  )
                : const Text('Agendar'),
          ),
        ],
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
