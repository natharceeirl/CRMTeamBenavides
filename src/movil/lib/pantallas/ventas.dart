import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

/// Ventas y cotizaciones. Es de solo lectura: crear, confirmar, anular, cobrar
/// y registrar el comprobante se hacen desde la web, que es donde vive la
/// transacción contra el stock y la caja. Aquí se consulta cuánto se debe.
class PantallaVentas extends ConsumerStatefulWidget {
  const PantallaVentas({super.key});

  @override
  ConsumerState<PantallaVentas> createState() => _PantallaVentasState();
}

class _PantallaVentasState extends ConsumerState<PantallaVentas> {
  String _busqueda = '';

  /// `null` es «todos los estados».
  int? _estadoId;

  @override
  Widget build(BuildContext context) {
    final ventas = ref.watch(ventasProvider);

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
          child: TextField(
            decoration: const InputDecoration(
              prefixIcon: Icon(Icons.search),
              hintText: 'Buscar por cliente o referencia',
            ),
            onChanged: (valor) => setState(() => _busqueda = valor),
          ),
        ),
        _FiltroEstados(
          seleccionado: _estadoId,
          alElegir: (estadoId) => setState(() => _estadoId = estadoId),
        ),
        Expanded(
          child: ventas.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () => ref.invalidate(ventasProvider),
            ),
            data: (lista) {
              final visibles = _filtrar(lista);

              if (visibles.isEmpty) {
                return const ListaVacia(mensaje: 'No hay ventas que coincidan.');
              }

              return Column(
                children: [
                  _Resumen(ventas: visibles),
                  Expanded(
                    child: RefreshIndicator(
                      onRefresh: () async => ref.invalidate(ventasProvider),
                      child: ListView.builder(
                        itemCount: visibles.length,
                        itemBuilder: (context, indice) {
                          final venta = visibles[indice];
                          return _FilaVenta(
                            venta: venta,
                            alTocar: () => abrirVenta(context, venta),
                          );
                        },
                      ),
                    ),
                  ),
                ],
              );
            },
          ),
        ),
      ],
    );
  }

  List<VentaApi> _filtrar(List<VentaApi> lista) {
    final texto = _busqueda.trim().toLowerCase();

    return lista.where((venta) {
      if (_estadoId != null && venta.estadoId != _estadoId) {
        return false;
      }
      final campos = [
        venta.clienteNombre,
        venta.referencia,
      ].join(' ').toLowerCase();
      return campos.contains(texto);
    }).toList();
  }
}

class _FiltroEstados extends StatelessWidget {
  const _FiltroEstados({required this.seleccionado, required this.alElegir});

  final int? seleccionado;
  final ValueChanged<int?> alElegir;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 56,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        children: [
          ChoiceChip(
            label: const Text('Todas'),
            selected: seleccionado == null,
            onSelected: (_) => alElegir(null),
          ),
          for (final entrada in nombresEstadoVenta.entries) ...[
            const SizedBox(width: 8),
            ChoiceChip(
              label: Text(entrada.value),
              selected: seleccionado == entrada.key,
              onSelected: (_) => alElegir(entrada.key),
            ),
          ],
        ],
      ),
    );
  }
}

/// Cuántas ventas se están viendo y cuánto suman. Las anuladas no suman: ni
/// entran a caja ni descuentan stock.
class _Resumen extends StatelessWidget {
  const _Resumen({required this.ventas});

  final List<VentaApi> ventas;

  @override
  Widget build(BuildContext context) {
    final total = ventas
        .where((venta) => esVentaVigente(venta.estadoId))
        .fold<double>(0, (suma, venta) => suma + venta.total);

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Row(
        children: [
          Text(
            '${ventas.length} ${ventas.length == 1 ? 'venta' : 'ventas'}',
            style: const TextStyle(color: Marca.textoSecundario),
          ),
          const Spacer(),
          Text(
            '${soles(total)} vigentes',
            style: const TextStyle(fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

class _FilaVenta extends StatelessWidget {
  const _FilaVenta({required this.venta, required this.alTocar});

  final VentaApi venta;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: ListTile(
        title: Text(
          venta.clienteNombre,
          style: const TextStyle(fontWeight: FontWeight.w600),
        ),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${venta.referencia} · ${venta.cantidadItems} '
              '${venta.cantidadItems == 1 ? 'ítem' : 'ítems'}',
              style: const TextStyle(color: Marca.textoSecundario),
            ),
            const SizedBox(height: 4),
            Wrap(
              spacing: 8,
              runSpacing: 4,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                EtiquetaEstadoVenta(estadoId: venta.estadoId),
                // Solo una venta confirmada se cobra.
                if (venta.estadoId == EstadoVenta.confirmada)
                  EtiquetaEstadoPago(estado: venta.estadoPago),
                Text(
                  fechaHora(venta.fecha),
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
        trailing: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Text(
              soles(venta.total),
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            if (venta.estadoId == EstadoVenta.confirmada && venta.saldo > 0)
              Text(
                'Debe ${soles(venta.saldo)}',
                style: const TextStyle(color: Marca.acento, fontSize: 12),
              ),
          ],
        ),
        onTap: alTocar,
      ),
    );
  }
}

/// Lo que se debe llama la atención; lo pagado queda sobrio.
class EtiquetaEstadoPago extends StatelessWidget {
  const EtiquetaEstadoPago({required this.estado, super.key});

  final String estado;

  @override
  Widget build(BuildContext context) {
    final pagado = estado == EstadoPago.pagado;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: pagado ? Marca.texto : Colors.transparent,
        border: Border.all(color: pagado ? Marca.texto : Marca.acento),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        estado,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: pagado ? Colors.white : Marca.acento,
        ),
      ),
    );
  }
}

/// Confirmada es la que ya entró a caja, así que lleva el acento. La anulada
/// se apaga y la cotización queda neutra, que todavía no es venta.
class EtiquetaEstadoVenta extends StatelessWidget {
  const EtiquetaEstadoVenta({required this.estadoId, super.key});

  final int estadoId;

  @override
  Widget build(BuildContext context) {
    final destacada = estadoId == EstadoVenta.confirmada;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: destacada ? Marca.acento.withValues(alpha: 0.12) : Marca.borde,
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        nombreEstadoVenta(estadoId),
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: destacada ? Marca.acento : Marca.textoSecundario,
        ),
      ),
    );
  }
}

void abrirVenta(BuildContext context, VentaApi venta) {
  showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Marca.fondo,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
    ),
    builder: (_) => _HojaVenta(venta: venta),
  );
}

class _HojaVenta extends ConsumerWidget {
  const _HojaVenta({required this.venta});

  final VentaApi venta;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detalle = ref.watch(ventaProvider(venta.id));

    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.75,
      maxChildSize: 0.95,
      builder: (context, control) => ListView(
        controller: control,
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          Text(
            venta.clienteNombre,
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 4),
          Text(
            '${venta.referencia} · ${fechaHora(venta.fecha)}',
            style: const TextStyle(color: Marca.textoSecundario),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              EtiquetaEstadoVenta(estadoId: venta.estadoId),
              const Spacer(),
              Text(
                soles(venta.total),
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 18,
                ),
              ),
            ],
          ),
          const Divider(height: 32),
          detalle.when(
            loading: () => const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () => ref.invalidate(ventaProvider(venta.id)),
            ),
            data: (datos) => _CuerpoVenta(datos: datos),
          ),
        ],
      ),
    );
  }
}

class _CuerpoVenta extends StatelessWidget {
  const _CuerpoVenta({required this.datos});

  final VentaDetalleApi datos;

  @override
  Widget build(BuildContext context) {
    final comprobante = datos.comprobante;
    final venta = datos.venta;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (datos.clienteDocumento?.isNotEmpty ?? false)
          _Dato(etiqueta: 'Documento', valor: datos.clienteDocumento!),
        if (datos.clienteTelefono?.isNotEmpty ?? false)
          _Dato(etiqueta: 'Teléfono', valor: datos.clienteTelefono!),
        if (datos.venta.vieneDeOrden)
          const _Dato(etiqueta: 'Origen', valor: 'Orden de servicio'),
        _Dato(
          etiqueta: 'Comprobante',
          valor: comprobante == null
              ? 'Sin comprobante'
              : '${comprobante.referencia} · ${comprobante.estado}',
        ),
        const Divider(height: 32),
        Text('Detalle', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 8),
        if (datos.detalles.isEmpty)
          const ListaVacia(mensaje: 'Esta venta no tiene líneas.')
        else
          for (final linea in datos.detalles) _FilaLinea(linea: linea),
        const Divider(height: 24),
        if (venta.montoIgv > 0) ...[
          _Monto(etiqueta: 'Op. gravadas', monto: venta.subtotalGravado),
          _Monto(etiqueta: 'IGV', monto: venta.montoIgv),
        ],
        _Monto(etiqueta: 'Total', monto: venta.total, destacado: true),
        if (venta.estadoId == EstadoVenta.confirmada) ...[
          const Divider(height: 32),
          Row(
            children: [
              Text('Cobro', style: Theme.of(context).textTheme.titleMedium),
              const Spacer(),
              EtiquetaEstadoPago(estado: venta.estadoPago),
            ],
          ),
          const SizedBox(height: 8),
          _Monto(etiqueta: 'Pagado', monto: venta.totalPagado),
          _Monto(
            etiqueta: 'Saldo',
            monto: venta.saldo,
            destacado: true,
            color: venta.saldo > 0 ? Marca.acento : null,
          ),
          for (final pago in datos.pagos) _FilaPago(pago: pago),
        ],
      ],
    );
  }
}

class _Monto extends StatelessWidget {
  const _Monto({
    required this.etiqueta,
    required this.monto,
    this.destacado = false,
    this.color,
  });

  final String etiqueta;
  final double monto;
  final bool destacado;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final estilo = TextStyle(
      fontWeight: destacado ? FontWeight.w700 : FontWeight.w400,
      fontSize: destacado ? 16 : 14,
      color: color,
    );
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Row(
        children: [
          Text(etiqueta, style: destacado ? estilo : const TextStyle(color: Marca.textoSecundario)),
          const Spacer(),
          Text(soles(monto), style: estilo),
        ],
      ),
    );
  }
}

class _FilaPago extends StatelessWidget {
  const _FilaPago({required this.pago});

  final PagoApi pago;

  @override
  Widget build(BuildContext context) {
    final detalle = [
      fechaHora(pago.fecha),
      if (pago.referencia?.isNotEmpty ?? false) pago.referencia!,
      if (pago.esAnticipo) 'Adelanto',
    ].join(' · ');

    return ListTile(
      contentPadding: EdgeInsets.zero,
      dense: true,
      title: Text(pago.metodoPagoNombre),
      subtitle: Text(detalle, style: const TextStyle(color: Marca.textoSecundario, fontSize: 12)),
      trailing: Text(soles(pago.monto), style: const TextStyle(fontWeight: FontWeight.w600)),
    );
  }
}

class _FilaLinea extends StatelessWidget {
  const _FilaLinea({required this.linea});

  final DetalleVentaApi linea;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      dense: true,
      title: Text(linea.productoNombre),
      subtitle: Text(
        [
          if (linea.productoCodigo.isNotEmpty) linea.productoCodigo,
          '${linea.cantidad} × ${soles(linea.precioUnitario)}',
        ].join(' · '),
        style: const TextStyle(color: Marca.textoSecundario, fontSize: 12),
      ),
      trailing: Text(
        soles(linea.importe),
        style: const TextStyle(fontWeight: FontWeight.w600),
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
