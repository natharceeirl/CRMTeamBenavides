import 'package:flutter/material.dart';

import 'inventario.dart';
import 'ventas.dart';

enum SeccionTienda { repuestos, ventas }

/// Agrupa repuestos y ventas en una sola pestaña. Son las dos caras del mismo
/// mostrador, y la barra inferior no aguanta seis destinos sin recortar las
/// etiquetas en pantallas chicas.
class PantallaTienda extends StatefulWidget {
  const PantallaTienda({super.key});

  @override
  State<PantallaTienda> createState() => _PantallaTiendaState();
}

class _PantallaTiendaState extends State<PantallaTienda> {
  SeccionTienda _seccion = SeccionTienda.repuestos;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
          child: SegmentedButton<SeccionTienda>(
            segments: const [
              ButtonSegment(
                value: SeccionTienda.repuestos,
                label: Text('Repuestos'),
                icon: Icon(Icons.inventory_2_outlined),
              ),
              ButtonSegment(
                value: SeccionTienda.ventas,
                label: Text('Ventas'),
                icon: Icon(Icons.receipt_long_outlined),
              ),
            ],
            selected: {_seccion},
            onSelectionChanged: (seleccion) =>
                setState(() => _seccion = seleccion.first),
          ),
        ),
        Expanded(
          child: IndexedStack(
            index: _seccion.index,
            children: const [PantallaInventario(), PantallaVentas()],
          ),
        ),
      ],
    );
  }
}
