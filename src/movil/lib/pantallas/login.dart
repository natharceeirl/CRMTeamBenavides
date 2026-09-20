import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/sesion.dart';
import '../tema.dart';

class PantallaLogin extends ConsumerStatefulWidget {
  const PantallaLogin({super.key});

  @override
  ConsumerState<PantallaLogin> createState() => _PantallaLoginState();
}

class _PantallaLoginState extends ConsumerState<PantallaLogin> {
  final _formulario = GlobalKey<FormState>();
  final _correo = TextEditingController();
  final _contrasena = TextEditingController();

  String? _error;
  bool _enviando = false;

  @override
  void dispose() {
    _correo.dispose();
    _contrasena.dispose();
    super.dispose();
  }

  Future<void> _ingresar() async {
    if (!_formulario.currentState!.validate()) {
      return;
    }

    setState(() {
      _error = null;
      _enviando = true;
    });

    try {
      await ref
          .read(sesionProvider.notifier)
          .entrar(_correo.text.trim(), _contrasena.text);
      // La redirección la hace el guardia de rutas al cambiar la sesión.
    } catch (fallo) {
      if (mounted) {
        setState(() => _error = '$fallo');
      }
    } finally {
      if (mounted) {
        setState(() => _enviando = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _formulario,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text(
                      'TEAM BENAVIDES',
                      style: TextStyle(
                        fontSize: 13,
                        letterSpacing: 2,
                        fontWeight: FontWeight.w600,
                        color: Marca.acento,
                      ),
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Iniciar sesión',
                      style: TextStyle(
                        fontFamily: Marca.fuenteTitulos,
                        fontSize: 30,
                        fontWeight: FontWeight.w700,
                        color: Marca.texto,
                      ),
                    ),
                    const SizedBox(height: 4),
                    const Text(
                      'Gestión del taller y la tienda',
                      style: TextStyle(color: Marca.textoSecundario),
                    ),
                    const SizedBox(height: 24),
                    if (_error != null) ...[
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: Marca.acento.withValues(alpha: 0.08),
                          borderRadius: BorderRadius.circular(6),
                          border: Border.all(
                            color: Marca.acento.withValues(alpha: 0.4),
                          ),
                        ),
                        child: Text(
                          _error!,
                          style: const TextStyle(color: Marca.texto),
                        ),
                      ),
                      const SizedBox(height: 16),
                    ],
                    TextFormField(
                      controller: _correo,
                      decoration: const InputDecoration(
                        labelText: 'Correo',
                        hintText: 'nombre@empresa.pe',
                      ),
                      keyboardType: TextInputType.emailAddress,
                      autocorrect: false,
                      validator: (valor) =>
                          (valor == null || valor.trim().isEmpty)
                              ? 'Ingresa tu correo'
                              : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _contrasena,
                      decoration: const InputDecoration(
                        labelText: 'Contraseña',
                      ),
                      obscureText: true,
                      onFieldSubmitted: (_) => _ingresar(),
                      validator: (valor) => (valor == null || valor.isEmpty)
                          ? 'Ingresa tu contraseña'
                          : null,
                    ),
                    const SizedBox(height: 24),
                    FilledButton(
                      onPressed: _enviando ? null : _ingresar,
                      child: _enviando
                          ? const SizedBox(
                              height: 20,
                              width: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : const Text('Ingresar'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
