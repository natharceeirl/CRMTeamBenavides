import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/estados.dart';
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

  bool _cargadoDelServidor = false;
  bool _guardando = false;
  String? _error;

  @override
  void dispose() {
    _diagnostico.dispose();
    super.dispose();
  }

  Future<void> _guardarDiagnostico() async {
    setState(() {
      _error = null;
      _guardando = true;
    });

    try {
      await ref
          .read(apiProvider)
          .registrarDiagnostico(widget.ordenId, _diagnostico.text.trim());
      ref.invalidate(ordenProvider(widget.ordenId));
      ref.invalidate(ordenesProvider);

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

  @override
  Widget build(BuildContext context) {
    final orden = ref.watch(ordenProvider(widget.ordenId));

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
          // El texto del servidor se carga una sola vez para no pisar lo que
          // el técnico esté escribiendo cuando la consulta se refresca.
          if (!_cargadoDelServidor) {
            _diagnostico.text = datos.orden.diagnostico ?? '';
            _cargadoDelServidor = true;
          }

          final puedeDiagnosticar = permiteDiagnostico(datos.orden.estadoId);

          return ListView(
            padding: const EdgeInsets.only(bottom: 32),
            children: [
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${datos.orden.unidad} · ${datos.orden.vehiculoPlaca}',
                        style: const TextStyle(
                          fontFamily: Marca.fuenteTitulos,
                          fontSize: 18,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 8),
                      _Dato('Estado', nombreEstadoOrden(datos.orden.estadoId)),
                      _Dato('Cliente', datos.orden.clienteNombre),
                      _Dato('Teléfono', datos.clienteTelefono),
                      _Dato('Técnico', datos.orden.tecnicoNombre ?? 'Sin asignar'),
                      _Dato('Ingreso', fechaHora(datos.orden.fechaApertura)),
                      _Dato(
                        'Kilometraje',
                        datos.vehiculoKilometraje == null
                            ? null
                            : '${datos.vehiculoKilometraje} km',
                      ),
                      _Dato('Observaciones', datos.orden.observaciones),
                    ],
                  ),
                ),
              ),
              const Padding(
                padding: EdgeInsets.fromLTRB(20, 16, 20, 4),
                child: Text(
                  'Diagnóstico',
                  style: TextStyle(
                    fontFamily: Marca.fuenteTitulos,
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (_error != null) ...[
                      Text(_error!, style: const TextStyle(color: Marca.acento)),
                      const SizedBox(height: 8),
                    ],
                    TextField(
                      controller: _diagnostico,
                      maxLines: 5,
                      enabled: puedeDiagnosticar && !_guardando,
                      decoration: const InputDecoration(
                        hintText: 'Qué encontraste en la unidad',
                      ),
                    ),
                    const SizedBox(height: 12),
                    FilledButton(
                      onPressed: puedeDiagnosticar && !_guardando ? _guardarDiagnostico : null,
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
                    if (!puedeDiagnosticar)
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
              const Padding(
                padding: EdgeInsets.fromLTRB(20, 24, 20, 4),
                child: Text(
                  'Trabajos y repuestos',
                  style: TextStyle(
                    fontFamily: Marca.fuenteTitulos,
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
              if (datos.detalles.isEmpty)
                const ListaVacia(mensaje: 'Todavía no hay trabajos ni repuestos.')
              else
                ...datos.detalles.map(
                  (detalle) => Card(
                    child: ListTile(
                      title: Text(detalle.descripcion),
                      subtitle: Text(
                        '${detalle.esRepuesto ? 'Repuesto' : 'Mano de obra'} · '
                        '${detalle.cantidad} x ${soles(detalle.precioUnitario)}',
                        style: const TextStyle(color: Marca.textoSecundario),
                      ),
                      trailing: Text(
                        soles(detalle.subtotal),
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                    ),
                  ),
                ),
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 0),
                child: Row(
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
              ),
              const Padding(
                padding: EdgeInsets.fromLTRB(20, 24, 20, 0),
                child: Text(
                  'Agregar repuestos y cambiar el estado se hace desde la web; en la app '
                  'todavía no está.',
                  style: TextStyle(color: Marca.textoSecundario),
                ),
              ),
            ],
          );
        },
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
          Expanded(child: Text(valor ?? '—')),
        ],
      ),
    );
  }
}
