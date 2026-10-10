import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/modelos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';

/// Abre la hoja para corregir o quitar un ítem de la orden. Devuelve true si cambió algo.
Future<bool?> abrirEditarItem(BuildContext context, String ordenId, DetalleServicioApi detalle) {
  return showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (_) => HojaEditarItem(ordenId: ordenId, detalle: detalle),
  );
}

/// Cantidad y precio de un ítem ya cargado, o quitarlo. El precio se compara con
/// el de lista del repuesto o servicio; en mano de obra y terceros, que no tienen
/// lista, con el precio que ya tenía. Si cambia, la orden espera a Gerencia.
class HojaEditarItem extends ConsumerStatefulWidget {
  const HojaEditarItem({required this.ordenId, required this.detalle, super.key});

  final String ordenId;
  final DetalleServicioApi detalle;

  @override
  ConsumerState<HojaEditarItem> createState() => _HojaEditarItemState();
}

class _HojaEditarItemState extends ConsumerState<HojaEditarItem> {
  late final _precio = TextEditingController(text: widget.detalle.precioUnitario.toStringAsFixed(2));
  late int _cantidad = widget.detalle.cantidad;
  bool _guardando = false;
  String? _error;

  @override
  void dispose() {
    _precio.dispose();
    super.dispose();
  }

  double? get _precioEscrito => double.tryParse(_precio.text.replaceAll(',', '.'));

  /// Precio contra el que se decide si hace falta aprobación.
  double _precioDeReferencia() {
    final detalle = widget.detalle;
    if (detalle.productoId != null) {
      final productos = ref.watch(productosProvider).value ?? const <ProductoApi>[];
      for (final producto in productos) {
        if (producto.id == detalle.productoId) return producto.precioVenta;
      }
    }
    if (detalle.servicioId != null) {
      final servicios = ref.watch(serviciosProvider).value ?? const <ServicioApi>[];
      for (final servicio in servicios) {
        if (servicio.id == detalle.servicioId) return servicio.precioSugerido;
      }
    }
    return detalle.precioUnitario;
  }

  bool _distinto(double? a, double b) => a != null && (a - b).abs() > 0.005;

  Future<void> _ejecutar(Future<void> Function() accion) async {
    setState(() {
      _guardando = true;
      _error = null;
    });
    try {
      await accion();
      if (mounted) Navigator.of(context).pop(true);
    } catch (fallo) {
      if (mounted) setState(() => _error = '$fallo');
    } finally {
      if (mounted) setState(() => _guardando = false);
    }
  }

  Future<void> _guardar() {
    final precio = _precioEscrito;
    final cambioPrecio = _distinto(precio, widget.detalle.precioUnitario);
    final cambioCantidad = _cantidad != widget.detalle.cantidad;
    if (!cambioPrecio && !cambioCantidad) {
      Navigator.of(context).pop(false);
      return Future.value();
    }
    // Solo lo que cambió: corregir la cantidad no abre una solicitud de precio.
    return _ejecutar(() => ref.read(apiProvider).actualizarItem(
          widget.ordenId,
          widget.detalle.id,
          cantidad: cambioCantidad ? _cantidad : null,
          precioUnitario: cambioPrecio ? precio : null,
        ));
  }

  Future<void> _quitar() async {
    final confirmado = await showDialog<bool>(
      context: context,
      builder: (contexto) => AlertDialog(
        title: const Text('Quitar el ítem'),
        content: Text(widget.detalle.esRepuesto ? 'El repuesto vuelve al stock.' : 'El ítem sale de la orden.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(contexto).pop(false), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.of(contexto).pop(true), child: const Text('Quitar')),
        ],
      ),
    );
    if (confirmado != true || !mounted) return;
    await _ejecutar(() => ref.read(apiProvider).quitarItem(widget.ordenId, widget.detalle.id));
  }

  @override
  Widget build(BuildContext context) {
    final referencia = _precioDeReferencia();
    final precio = _precioEscrito;
    // Gerencia y Recepción fijan el precio sin aprobación: a ellos no se les avisa.
    final pideAprobacion =
        _distinto(precio, referencia) && !ref.watch(sesionProvider).fijaPreciosDeOrdenSinAprobacion;
    final precioValido = precio != null && precio >= 0;

    return Padding(
      padding: EdgeInsets.fromLTRB(20, 16, 20, 16 + MediaQuery.viewInsetsOf(context).bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            widget.detalle.descripcion,
            style: const TextStyle(fontFamily: Marca.fuenteTitulos, fontSize: 18, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              const Text('Cantidad'),
              IconButton(
                tooltip: 'Menos',
                icon: const Icon(Icons.remove_circle_outline),
                onPressed: _cantidad > 1 && !_guardando ? () => setState(() => _cantidad--) : null,
              ),
              Text('$_cantidad', style: const TextStyle(fontWeight: FontWeight.w600)),
              IconButton(
                tooltip: 'Más',
                icon: const Icon(Icons.add_circle_outline),
                onPressed: _guardando ? null : () => setState(() => _cantidad++),
              ),
            ],
          ),
          TextField(
            controller: _precio,
            enabled: !_guardando,
            onChanged: (_) => setState(() {}),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: InputDecoration(
              labelText: 'Precio unitario (S/)',
              helperText: widget.detalle.productoId != null || widget.detalle.servicioId != null
                  ? 'De lista: ${soles(referencia)}'
                  : null,
            ),
          ),
          if (pideAprobacion)
            Container(
              margin: const EdgeInsets.only(top: 8),
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: Marca.acento.withValues(alpha: 0.08),
                border: const Border(left: BorderSide(color: Marca.acento, width: 3)),
              ),
              child: const Text(
                'Con otro precio, la orden queda pendiente hasta que Gerencia lo apruebe.',
                style: TextStyle(color: Marca.texto),
              ),
            ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(_error!, style: const TextStyle(color: Marca.acento)),
            ),
          const SizedBox(height: 16),
          Row(
            children: [
              TextButton.icon(
                icon: const Icon(Icons.delete_outline),
                label: const Text('Quitar'),
                onPressed: _guardando ? null : _quitar,
              ),
              const Spacer(),
              FilledButton(
                style: FilledButton.styleFrom(minimumSize: const Size(120, 48)),
                onPressed: precioValido && !_guardando ? _guardar : null,
                child: _guardando
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : const Text('Guardar'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
