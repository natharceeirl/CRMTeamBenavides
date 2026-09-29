import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/sesion.dart';
import '../tema.dart';

Future<void> abrirCambioDePassword(BuildContext context) {
  return showDialog<void>(
    context: context,
    builder: (_) => const DialogoCambiarPassword(),
  );
}

/// El usuario de la sesión cambia su propia contraseña.
class DialogoCambiarPassword extends ConsumerStatefulWidget {
  const DialogoCambiarPassword({super.key});

  @override
  ConsumerState<DialogoCambiarPassword> createState() => _DialogoCambiarPasswordState();
}

class _DialogoCambiarPasswordState extends ConsumerState<DialogoCambiarPassword> {
  final _formulario = GlobalKey<FormState>();
  final _actual = TextEditingController();
  final _nueva = TextEditingController();
  final _repetida = TextEditingController();

  bool _guardando = false;
  String? _error;

  @override
  void dispose() {
    _actual.dispose();
    _nueva.dispose();
    _repetida.dispose();
    super.dispose();
  }

  Future<void> _guardar() async {
    if (!(_formulario.currentState?.validate() ?? false)) {
      return;
    }

    setState(() {
      _guardando = true;
      _error = null;
    });

    try {
      await ref.read(apiProvider).cambiarPassword(_actual.text, _nueva.text);
      if (!mounted) return;
      Navigator.of(context).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Contraseña actualizada')),
      );
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
    return AlertDialog(
      title: const Text('Cambiar contraseña'),
      content: Form(
        key: _formulario,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_error != null) ...[
              Text(_error!, style: const TextStyle(color: Marca.acento)),
              const SizedBox(height: 8),
            ],
            TextFormField(
              controller: _actual,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Contraseña actual'),
              validator: (valor) =>
                  valor == null || valor.isEmpty ? 'Ingresa tu contraseña actual' : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _nueva,
              obscureText: true,
              decoration: const InputDecoration(
                labelText: 'Nueva contraseña',
                helperText: 'Mayúscula, minúscula, número y un símbolo.',
              ),
              validator: (valor) =>
                  valor == null || valor.length < 6 ? 'Al menos 6 caracteres' : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _repetida,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Repite la nueva contraseña'),
              validator: (valor) =>
                  valor != _nueva.text ? 'No coincide con la nueva contraseña' : null,
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: _guardando ? null : () => Navigator.of(context).pop(),
          child: const Text('Cancelar'),
        ),
        // El tema da a los FilledButton ancho completo; en la fila de acciones no cabe.
        FilledButton(
          style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
          onPressed: _guardando ? null : _guardar,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}
