import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

/// Consulta de repuestos e inventario. Es de solo lectura: registrar entradas,
/// salidas y ajustes se hace desde la web, donde vive la regla de negocio.
class PantallaInventario extends ConsumerStatefulWidget {
  const PantallaInventario({super.key});

  @override
  ConsumerState<PantallaInventario> createState() => _PantallaInventarioState();
}

class _PantallaInventarioState extends ConsumerState<PantallaInventario> {
  String _busqueda = '';
  bool _soloBajoStock = false;

  /// `null` es «todas las categorías».
  String? _categoriaId;

  @override
  Widget build(BuildContext context) {
    final productos = ref.watch(productosProvider);
    final categorias = ref.watch(categoriasProductoProvider);

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
          child: TextField(
            decoration: const InputDecoration(
              prefixIcon: Icon(Icons.search),
              hintText: 'Buscar por código, nombre o categoría',
            ),
            onChanged: (valor) => setState(() => _busqueda = valor),
          ),
        ),
        _FiltroCategorias(
          // Si las categorías fallan, la lista sigue: el filtro es opcional.
          categorias: categorias.value ?? const [],
          seleccionada: _categoriaId,
          soloBajoStock: _soloBajoStock,
          alElegirCategoria: (id) => setState(() => _categoriaId = id),
          alCambiarBajoStock: (valor) => setState(() => _soloBajoStock = valor),
        ),
        Expanded(
          child: productos.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () => ref.invalidate(productosProvider),
            ),
            data: (lista) {
              final visibles = _filtrar(lista);

              if (visibles.isEmpty) {
                return const ListaVacia(
                  mensaje: 'No hay repuestos que coincidan.',
                );
              }

              return RefreshIndicator(
                onRefresh: () async {
                  ref.invalidate(productosProvider);
                  ref.invalidate(categoriasProductoProvider);
                },
                child: ListView.builder(
                  itemCount: visibles.length,
                  itemBuilder: (context, indice) {
                    final producto = visibles[indice];
                    return _FilaProducto(
                      producto: producto,
                      alTocar: () => abrirProducto(context, producto),
                    );
                  },
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  List<ProductoApi> _filtrar(List<ProductoApi> lista) {
    final texto = _busqueda.trim().toLowerCase();

    return lista.where((producto) {
      if (_soloBajoStock && !producto.esBajoStock) {
        return false;
      }
      if (_categoriaId != null && producto.categoriaId != _categoriaId) {
        return false;
      }
      final campos = [
        producto.codigo,
        producto.nombre,
        producto.categoriaNombre,
      ].join(' ').toLowerCase();
      return campos.contains(texto);
    }).toList();
  }
}

class _FiltroCategorias extends StatelessWidget {
  const _FiltroCategorias({
    required this.categorias,
    required this.seleccionada,
    required this.soloBajoStock,
    required this.alElegirCategoria,
    required this.alCambiarBajoStock,
  });

  final List<CategoriaProductoApi> categorias;
  final String? seleccionada;
  final bool soloBajoStock;
  final ValueChanged<String?> alElegirCategoria;
  final ValueChanged<bool> alCambiarBajoStock;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 56,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        children: [
          FilterChip(
            label: const Text('Solo bajo stock'),
            selected: soloBajoStock,
            onSelected: alCambiarBajoStock,
          ),
          const SizedBox(width: 8),
          ChoiceChip(
            label: const Text('Todas'),
            selected: seleccionada == null,
            onSelected: (_) => alElegirCategoria(null),
          ),
          for (final categoria in categorias) ...[
            const SizedBox(width: 8),
            ChoiceChip(
              label: Text(categoria.nombre),
              selected: seleccionada == categoria.id,
              onSelected: (_) => alElegirCategoria(categoria.id),
            ),
          ],
        ],
      ),
    );
  }
}

class _FilaProducto extends StatelessWidget {
  const _FilaProducto({required this.producto, required this.alTocar});

  final ProductoApi producto;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: ListTile(
        title: Text(
          producto.nombre,
          style: const TextStyle(fontWeight: FontWeight.w600),
        ),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${producto.codigo} · ${producto.categoriaNombre}',
              style: const TextStyle(color: Marca.textoSecundario),
            ),
            const SizedBox(height: 4),
            Row(
              children: [
                EtiquetaStock(producto: producto),
                const SizedBox(width: 8),
                Text(
                  soles(producto.precioVenta),
                  style: const TextStyle(
                    color: Marca.textoSecundario,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ],
        ),
        isThreeLine: true,
        trailing: Text(
          '${producto.stockActual} ${producto.unidad}'.trim(),
          style: const TextStyle(fontWeight: FontWeight.w600),
        ),
        onTap: alTocar,
      ),
    );
  }
}

/// Mismo criterio que la web (`EstadoStock.tsx`): el backend ya calcula
/// `esBajoStock`, acá solo se pinta.
class EtiquetaStock extends StatelessWidget {
  const EtiquetaStock({required this.producto, super.key});

  final ProductoApi producto;

  @override
  Widget build(BuildContext context) {
    final String texto;
    final Color fondo;
    final Color color;

    if (producto.agotado) {
      texto = 'Agotado';
      fondo = Marca.texto;
      color = Marca.fondo;
    } else if (producto.esBajoStock) {
      texto = 'Stock bajo';
      fondo = Marca.acento.withValues(alpha: 0.12);
      color = Marca.acento;
    } else {
      texto = 'Disponible';
      fondo = Marca.borde;
      color = Marca.textoSecundario;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: fondo,
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        texto,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: color,
        ),
      ),
    );
  }
}

void abrirProducto(BuildContext context, ProductoApi producto) {
  showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Marca.fondo,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
    ),
    builder: (_) => _HojaProducto(producto: producto),
  );
}

class _HojaProducto extends ConsumerWidget {
  const _HojaProducto({required this.producto});

  final ProductoApi producto;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final movimientos = ref.watch(movimientosProductoProvider(producto.id));

    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.7,
      maxChildSize: 0.95,
      builder: (context, control) => ListView(
        controller: control,
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          Text(producto.nombre, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 4),
          Text(
            '${producto.codigo} · ${producto.categoriaNombre}',
            style: const TextStyle(color: Marca.textoSecundario),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              EtiquetaStock(producto: producto),
              const Spacer(),
              Text(
                soles(producto.precioVenta),
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
            ],
          ),
          const Divider(height: 32),
          _Dato(
            etiqueta: 'Stock actual',
            valor: '${producto.stockActual} ${producto.unidad}'.trim(),
          ),
          _Dato(etiqueta: 'Stock mínimo', valor: '${producto.stockMinimo}'),
          if (producto.descripcion != null && producto.descripcion!.isNotEmpty)
            _Dato(etiqueta: 'Descripción', valor: producto.descripcion!),
          const Divider(height: 32),
          Text(
            'Últimos movimientos',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 8),
          movimientos.when(
            loading: () => const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () =>
                  ref.invalidate(movimientosProductoProvider(producto.id)),
            ),
            data: (lista) => lista.isEmpty
                ? const ListaVacia(mensaje: 'Sin movimientos registrados.')
                : Column(
                    children: [
                      for (final movimiento in lista.take(10))
                        _FilaMovimiento(movimiento: movimiento),
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}

class _Dato extends StatelessWidget {
  const _Dato({required this.etiqueta, required this.valor});

  final String etiqueta;
  final String valor;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
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
          Expanded(child: Text(valor)),
        ],
      ),
    );
  }
}

class _FilaMovimiento extends StatelessWidget {
  const _FilaMovimiento({required this.movimiento});

  final MovimientoInventarioApi movimiento;

  @override
  Widget build(BuildContext context) {
    // La entrada suma y la salida resta; el ajuste fija el stock, así que no
    // lleva signo.
    final signo = switch (movimiento.tipoId) {
      TipoMovimiento.entrada => '+',
      TipoMovimiento.salida => '-',
      _ => '',
    };

    return ListTile(
      contentPadding: EdgeInsets.zero,
      dense: true,
      title: Text(nombreTipoMovimiento(movimiento.tipoId)),
      subtitle: Text(
        [
          fechaHora(movimiento.fechaCreacion),
          if (movimiento.motivo != null && movimiento.motivo!.isNotEmpty)
            movimiento.motivo!,
        ].join(' · '),
        style: const TextStyle(color: Marca.textoSecundario, fontSize: 12),
      ),
      trailing: Text(
        '$signo${movimiento.cantidad}',
        style: const TextStyle(fontWeight: FontWeight.w600),
      ),
    );
  }
}
