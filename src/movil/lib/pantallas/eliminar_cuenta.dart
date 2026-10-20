import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/sesion.dart';
import '../tema.dart';

Future<void> abrirEliminarCuenta(BuildContext context) {
  return showDialog<void>(
    context: context,
    builder: (_) => const DialogoEliminarCuenta(),
  );
}

/// El cliente elimina su cuenta de la app y del portal. Lo exigen Google Play y
/// Apple; el personal no lo ve, porque a sus cuentas las da de baja Gerencia.
class DialogoEliminarCuenta extends ConsumerStatefulWidget {
  const DialogoEliminarCuenta({super.key});

  @override
  ConsumerState<DialogoEliminarCuenta> createState() => _DialogoEliminarCuentaState();
}

class _DialogoEliminarCuentaState extends ConsumerState<DialogoEliminarCuenta> {
  final _formulario = GlobalKey<FormState>();
  final _password = TextEditingController();

  bool _eliminando = false;
  String? _error;

  @override
  void dispose() {
    _password.dispose();
    super.dispose();
  }

  Future<void> _eliminar() async {
    if (!(_formulario.currentState?.validate() ?? false)) {
      return;
    }

    setState(() {
      _eliminando = true;
      _error = null;
    });

    try {
      await ref.read(apiProvider).eliminarCuenta(_password.text);
      if (!mounted) return;
      // El aviso queda en el ScaffoldMessenger de la app, que sigue al pasar al login.
      final avisos = ScaffoldMessenger.of(context);
      Navigator.of(context).pop();
      avisos.showSnackBar(const SnackBar(content: Text('Tu cuenta se eliminó')));
      await ref.read(sesionProvider.notifier).salir();
    } catch (fallo) {
      if (mounted) {
        setState(() {
          _error = '$fallo';
          _eliminando = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Eliminar mi cuenta'),
      content: SingleChildScrollView(
        child: Form(
          key: _formulario,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Se borran el correo, el teléfono y la contraseña de tu cuenta, y se '
                'cierran tus sesiones en la app y en la web. No se puede deshacer.',
              ),
              const SizedBox(height: 8),
              const Text(
                'El taller conserva tu ficha de cliente, tus unidades y el historial de '
                'servicios y comprobantes, que la ley le pide guardar. Si más adelante '
                'quieres volver a usar la app, pide un código nuevo en el taller.',
              ),
              const SizedBox(height: 12),
              if (_error != null) ...[
                Text(_error!, style: const TextStyle(color: Marca.acento)),
                const SizedBox(height: 8),
              ],
              TextFormField(
                controller: _password,
                obscureText: true,
                decoration: const InputDecoration(labelText: 'Tu contraseña, para confirmar'),
                validator: (valor) => valor == null || valor.isEmpty ? 'Ingresa tu contraseña' : null,
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _eliminando ? null : () => Navigator.of(context).pop(),
          child: const Text('Cancelar'),
        ),
        // El tema da a los FilledButton ancho completo; en la fila de acciones no cabe.
        FilledButton(
          style: FilledButton.styleFrom(minimumSize: const Size(96, 44), backgroundColor: Marca.acentoBoton),
          onPressed: _eliminando ? null : _eliminar,
          child: const Text('Eliminar mi cuenta'),
        ),
      ],
    );
  }
}
