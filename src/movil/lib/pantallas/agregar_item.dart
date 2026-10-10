import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/permisos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

/// Abre la hoja para agregar un ítem a la orden. Devuelve true si se agregó.
Future<bool?> abrirAgregarItem(BuildContext context, String ordenId) {
  return showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (_) => HojaAgregarItem(ordenId: ordenId),
  );
}

/// Repuestos y servicios salen del catálogo con su precio, que se puede
/// cambiar: otro precio, suba o baje, deja la orden pendiente hasta que
/// Gerencia lo apruebe. La mano de obra y los terceros llevan precio libre y
/// solo aparecen con `precios.modificar`.
class HojaAgregarItem extends ConsumerStatefulWidget {
  const HojaAgregarItem({required this.ordenId, super.key});

  final String ordenId;

  @override
  ConsumerState<HojaAgregarItem> createState() => _HojaAgregarItemState();
}

class _HojaAgregarItemState extends ConsumerState<HojaAgregarItem> {
  final _descripcion = TextEditingController();
  final _precio = TextEditingController();

  int _tipo = TipoItem.repuesto;
  String _busqueda = '';
  ProductoApi? _producto;
  ServicioApi? _servicio;
  int _cantidad = 1;
  int _afectacion = 0;
  bool _guardando = false;
  String? _error;

  @override
  void dispose() {
    _descripcion.dispose();
    _precio.dispose();
    super.dispose();
  }

  bool get _esManual => _tipo == TipoItem.manoDeObra || _tipo == TipoItem.terceros;

  double? get _precioManual => double.tryParse(_precio.text.replaceAll(',', '.'));

  /// Precio de lista del repuesto o servicio elegido.
  double? get _precioCatalogo => switch (_tipo) {
        TipoItem.repuesto => _producto?.precioVenta,
        TipoItem.servicio => _servicio?.precioSugerido,
        _ => null,
      };

  /// Diferencias menores a medio céntimo son el mismo precio.
  bool get _fueraDeLista {
    final catalogo = _precioCatalogo;
    final elegido = _precioManual;
    return catalogo != null && elegido != null && (elegido - catalogo).abs() > 0.005;
  }

  void _ponerPrecioDeLista(double precio) => _precio.text = precio.toStringAsFixed(2);

  bool get _listo => switch (_tipo) {
        TipoItem.repuesto => _producto != null && (_precioManual ?? -1) >= 0,
        TipoItem.servicio => _servicio != null && (_precioManual ?? -1) >= 0,
        _ => _descripcion.text.trim().isNotEmpty && (_precioManual ?? -1) >= 0,
      };

  void _elegirTipo(int tipo) {
    setState(() {
      _tipo = tipo;
      _busqueda = '';
      _producto = null;
      _servicio = null;
      _cantidad = 1;
      _error = null;
      _precio.clear();
    });
  }

  Future<void> _agregar() async {
    setState(() {
      _guardando = true;
      _error = null;
    });

    try {
      await ref.read(apiProvider).agregarItem(
            widget.ordenId,
            tipoItem: _tipo,
            cantidad: _cantidad,
            productoId: _producto?.id,
            servicioId: _servicio?.id,
            descripcion: _esManual ? _descripcion.text.trim() : null,
            // Del catálogo solo se envía el precio si cambió: así la API usa el
            // de lista y no abre una solicitud a Gerencia.
            precioUnitario: _esManual || _fueraDeLista ? _precioManual : null,
            tipoAfectacionIgv: _esManual ? _afectacion : null,
          );
      if (mounted) {
        Navigator.of(context).pop(true);
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
    final sesion = ref.watch(sesionProvider);
    final tipos = tiposDeItemPermitidos(
      puedeFijarPrecios: sesion.tienePermiso(Permisos.preciosModificar),
    );
    // Un repuesto no se puede pedir en más cantidad de la que hay en stock.
    final maximo = _tipo == TipoItem.repuesto ? _producto?.stockActual : null;

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: SizedBox(
        height: MediaQuery.sizeOf(context).height * 0.85,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 8, 0),
              child: Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Agregar a la orden',
                      style: TextStyle(
                        fontFamily: Marca.fuenteTitulos,
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  IconButton(
                    tooltip: 'Cerrar',
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.of(context).pop(),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Wrap(
                spacing: 8,
                children: [
                  for (final tipo in tipos)
                    ChoiceChip(
                      label: Text(nombresTipoItem[tipo]!),
                      selected: _tipo == tipo,
                      onSelected: (_) => _elegirTipo(tipo),
                    ),
                ],
              ),
            ),
            Expanded(child: _cuerpo()),
            if (!_esManual && _precioCatalogo != null) _precioDelCatalogo(),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                child: Text(_error!, style: const TextStyle(color: Marca.acento)),
              ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
              child: Row(
                children: [
                  const Text('Cantidad'),
                  IconButton(
                    tooltip: 'Menos',
                    icon: const Icon(Icons.remove_circle_outline),
                    onPressed: _cantidad > 1 ? () => setState(() => _cantidad--) : null,
                  ),
                  Text('$_cantidad', style: const TextStyle(fontWeight: FontWeight.w600)),
                  IconButton(
                    tooltip: 'Más',
                    icon: const Icon(Icons.add_circle_outline),
                    onPressed: maximo == null || _cantidad < maximo
                        ? () => setState(() => _cantidad++)
                        : null,
                  ),
                  const Spacer(),
                  FilledButton(
                    style: FilledButton.styleFrom(minimumSize: const Size(120, 48)),
                    onPressed: _listo && !_guardando ? _agregar : null,
                    child: _guardando
                        ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                          )
                        : const Text('Agregar'),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// Precio del repuesto o servicio elegido, con el aviso cuando sale de la lista.
  Widget _precioDelCatalogo() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _precio,
            onChanged: (_) => setState(() {}),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: InputDecoration(
              labelText: 'Precio unitario (S/)',
              helperText: 'De lista: ${soles(_precioCatalogo!)}',
            ),
          ),
          if (_fueraDeLista && !ref.watch(sesionProvider).fijaPreciosDeOrdenSinAprobacion)
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
        ],
      ),
    );
  }

  Widget _cuerpo() {
    switch (_tipo) {
      case TipoItem.repuesto:
        return _ListaCatalogo<ProductoApi>(
          datos: ref.watch(productosProvider),
          busqueda: _busqueda,
          alBuscar: (texto) => setState(() => _busqueda = texto),
          pista: 'Buscar repuesto por código o nombre',
          vacio: 'Todavía no hay repuestos en el inventario.',
          coincide: (producto, texto) =>
              producto.activo &&
              '${producto.codigo} ${producto.nombre}'.toLowerCase().contains(texto),
          elegido: (producto) => producto.id == _producto?.id,
          alElegir: (producto) => setState(() {
            _producto = producto;
            _cantidad = 1;
            _ponerPrecioDeLista(producto.precioVenta);
          }),
          disponible: (producto) => !producto.agotado,
          titulo: (producto) => producto.nombre,
          detalle: (producto) => producto.agotado
              ? '${producto.codigo} · sin stock'
              : '${producto.codigo} · stock ${producto.stockActual} ${producto.unidad}',
          precio: (producto) => producto.precioVenta,
        );
      case TipoItem.servicio:
        return _ListaCatalogo<ServicioApi>(
          datos: ref.watch(serviciosProvider),
          busqueda: _busqueda,
          alBuscar: (texto) => setState(() => _busqueda = texto),
          pista: 'Buscar servicio',
          vacio: 'Todavía no hay servicios en el catálogo.',
          coincide: (servicio, texto) => servicio.nombre.toLowerCase().contains(texto),
          elegido: (servicio) => servicio.id == _servicio?.id,
          alElegir: (servicio) => setState(() {
            _servicio = servicio;
            _ponerPrecioDeLista(servicio.precioSugerido);
          }),
          disponible: (_) => true,
          titulo: (servicio) => servicio.nombre,
          detalle: (servicio) => nombresAfectacionIgv[servicio.tipoAfectacionIgv] ?? 'Gravado',
          precio: (servicio) => servicio.precioSugerido,
        );
      default:
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            TextField(
              controller: _descripcion,
              onChanged: (_) => setState(() {}),
              decoration: InputDecoration(
                labelText: _tipo == TipoItem.terceros ? 'Servicio de terceros' : 'Trabajo realizado',
                hintText: _tipo == TipoItem.terceros ? 'Tornería del eje, pintura…' : 'Regulación de válvulas…',
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _precio,
              onChanged: (_) => setState(() {}),
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: const InputDecoration(labelText: 'Precio unitario (S/)'),
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<int>(
              initialValue: _afectacion,
              decoration: const InputDecoration(labelText: 'Afectación de IGV'),
              items: [
                for (final entrada in nombresAfectacionIgv.entries)
                  DropdownMenuItem(value: entrada.key, child: Text(entrada.value)),
              ],
              onChanged: (valor) => setState(() => _afectacion = valor ?? 0),
            ),
          ],
        );
    }
  }
}

/// Lista buscable de un catálogo (repuestos o servicios) con su precio.
class _ListaCatalogo<T> extends StatelessWidget {
  const _ListaCatalogo({
    required this.datos,
    required this.busqueda,
    required this.alBuscar,
    required this.pista,
    required this.vacio,
    required this.coincide,
    required this.elegido,
    required this.alElegir,
    required this.disponible,
    required this.titulo,
    required this.detalle,
    required this.precio,
  });

  final AsyncValue<List<T>> datos;
  final String busqueda;
  final ValueChanged<String> alBuscar;
  final String pista;

  /// Mensaje cuando el catálogo no tiene nada, a diferencia de una búsqueda
  /// sin resultados.
  final String vacio;
  final bool Function(T elemento, String texto) coincide;
  final bool Function(T elemento) elegido;
  final ValueChanged<T> alElegir;
  final bool Function(T elemento) disponible;
  final String Function(T elemento) titulo;
  final String Function(T elemento) detalle;
  final double Function(T elemento) precio;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: TextField(
            onChanged: alBuscar,
            decoration: InputDecoration(prefixIcon: const Icon(Icons.search), hintText: pista),
          ),
        ),
        Expanded(
          child: datos.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(error: error),
            data: (lista) {
              final texto = busqueda.toLowerCase();
              final visibles = lista.where((elemento) => coincide(elemento, texto)).toList();
              if (lista.isEmpty) {
                return ListaVacia(mensaje: vacio);
              }
              if (visibles.isEmpty) {
                return const ListaVacia(mensaje: 'No hay coincidencias en el catálogo.');
              }
              return ListView.builder(
                itemCount: visibles.length,
                itemBuilder: (context, indice) {
                  final elemento = visibles[indice];
                  final activo = disponible(elemento);
                  return ListTile(
                    enabled: activo,
                    selected: elegido(elemento),
                    selectedTileColor: Marca.acento.withValues(alpha: 0.08),
                    title: Text(titulo(elemento)),
                    subtitle: Text(detalle(elemento)),
                    trailing: Text(
                      soles(precio(elemento)),
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    onTap: activo ? () => alElegir(elemento) : null,
                  );
                },
              );
            },
          ),
        ),
      ],
    );
  }
}
