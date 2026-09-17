import 'package:flutter/material.dart';

import '../tema.dart';

/// Error de una consulta, con la opción de reintentar.
class AvisoError extends StatelessWidget {
  const AvisoError({required this.error, this.alReintentar, super.key});

  final Object error;
  final VoidCallback? alReintentar;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, color: Marca.acento, size: 32),
            const SizedBox(height: 12),
            Text(
              '$error',
              textAlign: TextAlign.center,
              style: const TextStyle(color: Marca.textoSecundario),
            ),
            if (alReintentar != null) ...[
              const SizedBox(height: 16),
              OutlinedButton(
                onPressed: alReintentar,
                child: const Text('Reintentar'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Mensaje para una lista vacía.
class ListaVacia extends StatelessWidget {
  const ListaVacia({required this.mensaje, super.key});

  final String mensaje;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Text(
          mensaje,
          textAlign: TextAlign.center,
          style: const TextStyle(color: Marca.textoSecundario),
        ),
      ),
    );
  }
}
