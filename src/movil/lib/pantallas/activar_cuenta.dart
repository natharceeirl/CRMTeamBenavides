import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/sesion.dart';
import '../tema.dart';

/// El cliente activa su cuenta con su DNI y el código de 6 dígitos que le dan en
/// el taller. Después entra con su DNI y la contraseña que eligió aquí.
class PantallaActivarCuenta extends ConsumerStatefulWidget {
  const PantallaActivarCuenta({super.key});

  @override
  ConsumerState<PantallaActivarCuenta> createState() => _PantallaActivarCuentaState();
}

class _PantallaActivarCuentaState extends ConsumerState<PantallaActivarCuenta> {
  final _formulario = GlobalKey<FormState>();
  final _documento = TextEditingController();
  final _codigo = TextEditingController();
  final _email = TextEditingController();
  final _contrasena = TextEditingController();
  final _repetida = TextEditingController();

  String? _error;
  bool _enviando = false;

  @override
  void dispose() {
    _documento.dispose();
    _codigo.dispose();
    _email.dispose();
    _contrasena.dispose();
    _repetida.dispose();
    super.dispose();
  }

  Future<void> _activar() async {
    if (!_formulario.currentState!.validate()) {
      return;
    }

    setState(() {
      _error = null;
      _enviando = true;
    });

    try {
      final email = _email.text.trim();
      await ref.read(sesionProvider.notifier).activar(
            documento: _documento.text.trim(),
            codigo: _codigo.text.trim(),
            password: _contrasena.text,
            email: email.isEmpty ? null : email,
          );
      // Con la sesión abierta, el guardia de rutas lleva al inicio.
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
      appBar: AppBar(),
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
                      'Activar mi cuenta',
                      style: TextStyle(
                        fontFamily: Marca.fuenteTitulos,
                        fontSize: 30,
                        fontWeight: FontWeight.w700,
                        color: Marca.texto,
                      ),
                    ),
                    const SizedBox(height: 4),
                    const Text(
                      'Escribe tu DNI y el código que te dieron en el taller. '
                      'Después entras con tu DNI y la contraseña que elijas.',
                      style: TextStyle(color: Marca.textoSecundario),
                    ),
                    const SizedBox(height: 24),
                    if (_error != null) ...[
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: Marca.acento.withValues(alpha: 0.08),
                          borderRadius: BorderRadius.circular(6),
                          border: Border.all(color: Marca.acento.withValues(alpha: 0.4)),
                        ),
                        child: Text(_error!, style: const TextStyle(color: Marca.texto)),
                      ),
                      const SizedBox(height: 16),
                    ],
                    TextFormField(
                      controller: _documento,
                      decoration: const InputDecoration(labelText: 'DNI'),
                      keyboardType: TextInputType.number,
                      autocorrect: false,
                      validator: (valor) =>
                          (valor == null || valor.trim().isEmpty) ? 'Ingresa tu DNI' : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _codigo,
                      decoration: const InputDecoration(
                        labelText: 'Código de activación',
                        hintText: '6 dígitos',
                      ),
                      keyboardType: TextInputType.number,
                      inputFormatters: [
                        FilteringTextInputFormatter.digitsOnly,
                        LengthLimitingTextInputFormatter(6),
                      ],
                      validator: (valor) =>
                          (valor == null || valor.trim().length != 6) ? 'El código tiene 6 dígitos' : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _email,
                      decoration: const InputDecoration(
                        labelText: 'Correo (opcional)',
                        hintText: 'También podrás entrar con él',
                      ),
                      keyboardType: TextInputType.emailAddress,
                      autocorrect: false,
                      validator: (valor) {
                        final texto = valor?.trim() ?? '';
                        return texto.isEmpty || texto.contains('@') ? null : 'Revisa el correo';
                      },
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _contrasena,
                      decoration: const InputDecoration(labelText: 'Contraseña nueva'),
                      obscureText: true,
                      validator: (valor) =>
                          (valor == null || valor.length < 6) ? 'Al menos 6 caracteres' : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _repetida,
                      decoration: const InputDecoration(labelText: 'Repite la contraseña'),
                      obscureText: true,
                      onFieldSubmitted: (_) => _activar(),
                      validator: (valor) =>
                          valor != _contrasena.text ? 'Las contraseñas no coinciden' : null,
                    ),
                    const SizedBox(height: 24),
                    FilledButton(
                      onPressed: _enviando ? null : _activar,
                      child: _enviando
                          ? const SizedBox(
                              height: 20,
                              width: 20,
                              child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                            )
                          : const Text('Activar y entrar'),
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
