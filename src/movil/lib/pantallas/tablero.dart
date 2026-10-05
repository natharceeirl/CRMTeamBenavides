import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/permisos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

class PantallaTablero extends ConsumerWidget {
  const PantallaTablero({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final resumen = ref.watch(resumenProvider);
    final sesion = ref.watch(sesionProvider);
    final veCaja = sesion.tienePermiso(Permisos.cajaConsultar);
    // Los montos son de Gerencia; el resto ve cuántas ventas hubo.
    final veMontos = sesion.tienePermiso(Permisos.reportesVerFinancieros);

    return resumen.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => AvisoError(
        error: error,
        alReintentar: () => ref.invalidate(resumenProvider),
      ),
      data: (datos) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(resumenProvider);
          if (veCaja) ref.invalidate(cajaActualProvider);
        },
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Row(
              children: [
                _Indicador(etiqueta: 'En taller', valor: '${datos.enTaller}'),
                const SizedBox(width: 12),
                _Indicador(etiqueta: 'Listas para entrega', valor: '${datos.listas}'),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                veMontos
                    ? _Indicador(etiqueta: 'Vendido', valor: soles(datos.montoVentas), compacto: true)
                    : _Indicador(etiqueta: 'Ventas', valor: '${datos.ventasConfirmadas}'),
                const SizedBox(width: 12),
                _Indicador(
                  etiqueta: 'Stock bajo',
                  valor: '${datos.productosStockBajo}',
                  alerta: datos.productosStockBajo > 0,
                ),
              ],
            ),
            if (veCaja) ...[
              const SizedBox(height: 12),
              const Row(children: [_IndicadorCaja()]),
            ],
            const SizedBox(height: 24),
            const Text(
              'Resumen general',
              style: TextStyle(
                fontFamily: Marca.fuenteTitulos,
                fontSize: 18,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 8),
            Card(
              margin: EdgeInsets.zero,
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  children: [
                    _Fila('Órdenes en total', '${datos.totalOrdenes}'),
                    _Fila('Órdenes entregadas', '${datos.entregadas}'),
                    _Fila('Ventas confirmadas', '${datos.ventasConfirmadas}'),
                    _Fila('Clientes activos', '${datos.clientesActivos}'),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Saldo de la caja chica abierta. Aparte del resumen: si la caja falla, el
/// tablero se sigue viendo.
class _IndicadorCaja extends ConsumerWidget {
  const _IndicadorCaja();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final caja = ref.watch(cajaActualProvider);

    final valor = caja.when(
      loading: () => '…',
      error: (_, _) => 'Sin datos',
      data: (datos) => datos.abierta ? soles(datos.saldo ?? 0) : 'Cerrada',
    );
    return _Indicador(etiqueta: 'Caja chica', valor: valor, compacto: true);
  }
}

class _Indicador extends StatelessWidget {
  const _Indicador({
    required this.etiqueta,
    required this.valor,
    this.alerta = false,
    this.compacto = false,
  });

  final String etiqueta;
  final String valor;
  final bool alerta;
  final bool compacto;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Card(
        margin: EdgeInsets.zero,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                etiqueta.toUpperCase(),
                style: const TextStyle(
                  fontSize: 11,
                  letterSpacing: 1,
                  fontWeight: FontWeight.w600,
                  color: Marca.textoSecundario,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                valor,
                style: TextStyle(
                  fontFamily: Marca.fuenteTitulos,
                  fontSize: compacto ? 20 : 28,
                  fontWeight: FontWeight.w700,
                  color: alerta ? Marca.acento : Marca.texto,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Fila extends StatelessWidget {
  const _Fila(this.etiqueta, this.valor);

  final String etiqueta;
  final String valor;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(etiqueta, style: const TextStyle(color: Marca.textoSecundario)),
          Text(valor, style: const TextStyle(fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}
