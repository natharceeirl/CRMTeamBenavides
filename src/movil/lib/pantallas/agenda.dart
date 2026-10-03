import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/permisos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'citas_cliente.dart' show EtiquetaEstadoCita;
import 'comunes.dart';

/// Cómo se lee cada paso de la cita en el botón.
const _accionEstado = <String, String>{
  EstadoCita.confirmada: 'Confirmar',
  EstadoCita.enTaller: 'Llegó al taller',
  EstadoCita.completada: 'Completar',
  EstadoCita.noAsistio: 'No asistió',
};

/// Las citas agrupadas por día local, cada día en orden de hora.
List<({DateTime dia, List<CitaApi> citas})> citasPorDia(List<CitaApi> citas) {
  final dias = <DateTime, List<CitaApi>>{};
  final enOrden = [...citas]..sort((una, otra) => una.fechaHoraProgramada.compareTo(otra.fechaHoraProgramada));
  for (final cita in enOrden) {
    final local = cita.fechaHoraProgramada.toLocal();
    dias.putIfAbsent(DateTime(local.year, local.month, local.day), () => []).add(cita);
  }
  return [for (final entrada in dias.entries) (dia: entrada.key, citas: entrada.value)];
}

/// «Hoy», «Mañana» o «Lun 05/10».
String tituloDia(DateTime dia, DateTime hoy) {
  final base = DateTime(hoy.year, hoy.month, hoy.day);
  final diferencia = DateTime(dia.year, dia.month, dia.day).difference(base).inDays;
  if (diferencia == 0) return 'Hoy';
  if (diferencia == 1) return 'Mañana';
  return diaConNombre(dia);
}

/// Agenda del personal: las citas de esta semana. Quien edita citas las confirma
/// o marca la llegada desde aquí; la agenda completa está en la web.
class PantallaAgenda extends ConsumerWidget {
  const PantallaAgenda({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final agenda = ref.watch(agendaProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Agenda de la semana')),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(agendaProvider),
        child: agenda.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ListView(
            children: [AvisoError(error: error, alReintentar: () => ref.invalidate(agendaProvider))],
          ),
          data: (citas) {
            if (citas.isEmpty) {
              return ListView(children: const [ListaVacia(mensaje: 'No hay citas esta semana.')]);
            }
            final hoy = DateTime.now();
            return ListView(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
              children: [
                for (final grupo in citasPorDia(citas)) ...[
                  Padding(
                    padding: const EdgeInsets.only(top: 16, bottom: 8),
                    child: Text(
                      tituloDia(grupo.dia, hoy),
                      style: const TextStyle(fontFamily: Marca.fuenteTitulos, fontSize: 18, fontWeight: FontWeight.w700),
                    ),
                  ),
                  for (final cita in grupo.citas) _TarjetaAgenda(cita: cita),
                ],
              ],
            );
          },
        ),
      ),
    );
  }
}

class _TarjetaAgenda extends StatelessWidget {
  const _TarjetaAgenda({required this.cita});

  final CitaApi cita;

  @override
  Widget build(BuildContext context) {
    final local = cita.fechaHoraProgramada.toLocal();
    final cerrada = esCitaFinal(cita.estado);

    return Card(
      child: InkWell(
        onTap: () => showModalBottomSheet<void>(
          context: context,
          isScrollControlled: true,
          useSafeArea: true,
          builder: (_) => _HojaAgenda(cita: cita),
        ),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      '${horaMinuto(local.hour, local.minute)} · ${cita.duracionMinutos} min',
                      style: TextStyle(fontWeight: FontWeight.w700, color: cerrada ? Marca.textoSecundario : Marca.texto),
                    ),
                  ),
                  EtiquetaEstadoCita(estado: cita.estado),
                ],
              ),
              const SizedBox(height: 6),
              Text(cita.clienteNombre),
              Text(cita.unidad, style: const TextStyle(color: Marca.textoSecundario)),
              Text(cita.motivo, style: const TextStyle(color: Marca.textoSecundario)),
            ],
          ),
        ),
      ),
    );
  }
}

class _HojaAgenda extends ConsumerStatefulWidget {
  const _HojaAgenda({required this.cita});

  final CitaApi cita;

  @override
  ConsumerState<_HojaAgenda> createState() => _HojaAgendaState();
}

class _HojaAgendaState extends ConsumerState<_HojaAgenda> {
  String? _guardando;
  String? _error;

  Future<void> _cambiar(String destino) async {
    setState(() {
      _guardando = destino;
      _error = null;
    });
    try {
      await ref.read(apiProvider).cambiarEstadoCita(widget.cita.id, destino);
      ref.invalidate(agendaProvider);
      if (mounted) {
        Navigator.of(context).pop();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Cita: ${nombresEstadoCita[destino]!.toLowerCase()}')),
        );
      }
    } on Exception catch (error) {
      setState(() => _error = '$error');
    } finally {
      if (mounted) {
        setState(() => _guardando = null);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final cita = widget.cita;
    final destinos = ref.watch(sesionProvider).tienePermiso(Permisos.citasEditar)
        ? siguientesEstadosCita(cita.estado)
        : const <String>[];

    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
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
          Text(cita.numeroCita, style: const TextStyle(color: Marca.textoSecundario)),
          const Divider(height: 24),
          Text(cita.clienteNombre, style: const TextStyle(fontWeight: FontWeight.w600)),
          Text(cita.unidad),
          const SizedBox(height: 4),
          Text(cita.motivo),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(_error!, style: const TextStyle(color: Marca.acento)),
            ),
          if (destinos.isNotEmpty) ...[
            const SizedBox(height: 16),
            for (final (indice, destino) in destinos.indexed)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: indice == 0
                    ? FilledButton(
                        style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                        onPressed: _guardando == null ? () => _cambiar(destino) : null,
                        child: Text(_accionEstado[destino] ?? nombresEstadoCita[destino]!),
                      )
                    : OutlinedButton(
                        style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                        onPressed: _guardando == null ? () => _cambiar(destino) : null,
                        child: Text(_accionEstado[destino] ?? nombresEstadoCita[destino]!),
                      ),
              ),
          ],
        ],
      ),
    );
  }
}
