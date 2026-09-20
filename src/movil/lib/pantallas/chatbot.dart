import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/modelos.dart';
import '../auth/sesion.dart';
import '../tema.dart';

/// Abre el asistente como hoja inferior, igual que el widget de la web.
void abrirChatbot(BuildContext context) {
  showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Marca.fondo,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
    ),
    builder: (_) => const _HojaChatbot(),
  );
}

class _Mensaje {
  const _Mensaje(this.de, this.texto);

  final String de; // 'bot' o 'usuario'
  final String texto;
}

class _HojaChatbot extends ConsumerStatefulWidget {
  const _HojaChatbot();

  @override
  ConsumerState<_HojaChatbot> createState() => _HojaChatbotState();
}

class _HojaChatbotState extends ConsumerState<_HojaChatbot> {
  final _entrada = TextEditingController();
  final _telefono = TextEditingController();
  final _motivo = TextEditingController();

  final List<_Mensaje> _mensajes = [
    const _Mensaje('bot', 'Hola, soy el asistente de Team Benavides. ¿En qué te ayudo?'),
  ];

  List<FaqApi> _sugerencias = const [];
  String? _consultaId;
  bool _consultando = false;
  bool _ofreceAgente = false;
  bool _formularioAgente = false;
  bool _enviandoAgente = false;

  @override
  void dispose() {
    _entrada.dispose();
    _telefono.dispose();
    _motivo.dispose();
    super.dispose();
  }

  Future<void> _preguntar(String mensaje) async {
    final limpio = mensaje.trim();
    if (limpio.isEmpty || _consultando) {
      return;
    }

    setState(() {
      _mensajes.add(_Mensaje('usuario', limpio));
      _entrada.clear();
      _sugerencias = const [];
      _consultando = true;
    });

    try {
      final respuesta = await ref.read(apiProvider).consultarChatbot(limpio);
      if (!mounted) return;
      setState(() {
        _mensajes.add(_Mensaje('bot', respuesta.mensajeRespuesta));
        _sugerencias = respuesta.sugerencias;
        _consultaId = respuesta.consultaId;
        // Siempre se ofrece el asesor: la búsqueda por palabras clave
        // a veces responde algo que no era lo que se preguntó.
        _ofreceAgente = true;
      });
    } catch (fallo) {
      if (!mounted) return;
      setState(() {
        _mensajes.add(_Mensaje('bot', '$fallo'));
        _ofreceAgente = true;
      });
    } finally {
      if (mounted) {
        setState(() => _consultando = false);
      }
    }
  }

  Future<void> _pedirAgente() async {
    if (_telefono.text.trim().isEmpty || _motivo.text.trim().isEmpty) {
      return;
    }

    setState(() => _enviandoAgente = true);

    try {
      final mensaje = await ref.read(apiProvider).solicitarAgente(
            telefono: _telefono.text.trim(),
            motivo: _motivo.text.trim(),
            consultaId: _consultaId,
            nombre: ref.read(sesionProvider).usuario?.nombre,
          );
      if (!mounted) return;
      setState(() {
        _mensajes.add(_Mensaje('bot', mensaje));
        _formularioAgente = false;
        _ofreceAgente = false;
      });
    } catch (fallo) {
      if (!mounted) return;
      setState(() => _mensajes.add(_Mensaje('bot', '$fallo')));
    } finally {
      if (mounted) {
        setState(() => _enviandoAgente = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final faqs = ref.watch(faqsProvider);
    final atajos = faqs.value ?? const <FaqApi>[];

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: SizedBox(
        height: MediaQuery.of(context).size.height * 0.75,
        child: Column(
          children: [
            ListTile(
              title: const Text(
                'Asistente Team Benavides',
                style: TextStyle(fontFamily: Marca.fuenteTitulos, fontWeight: FontWeight.w700),
              ),
              trailing: IconButton(
                icon: const Icon(Icons.close),
                onPressed: () => Navigator.of(context).pop(),
              ),
            ),
            const Divider(height: 1),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  for (final mensaje in _mensajes) _Burbuja(mensaje: mensaje),
                  if (_consultando) const _Burbuja(mensaje: _Mensaje('bot', 'Escribiendo…')),
                  if (_mensajes.length == 1)
                    for (final faq in atajos.take(4))
                      _BotonAtajo(texto: faq.pregunta, alTocar: () => _preguntar(faq.pregunta)),
                  for (final faq in _sugerencias)
                    _BotonAtajo(texto: faq.pregunta, alTocar: () => _preguntar(faq.pregunta)),
                  if (_ofreceAgente && !_formularioAgente)
                    _BotonAtajo(
                      texto: '¿No era eso? Hablar con un asesor',
                      alTocar: () => setState(() => _formularioAgente = true),
                    ),
                  if (_formularioAgente) ...[
                    const SizedBox(height: 8),
                    TextField(
                      controller: _telefono,
                      keyboardType: TextInputType.phone,
                      decoration: const InputDecoration(labelText: 'Teléfono'),
                    ),
                    const SizedBox(height: 8),
                    TextField(
                      controller: _motivo,
                      maxLines: 2,
                      decoration: const InputDecoration(labelText: 'Motivo'),
                    ),
                    const SizedBox(height: 12),
                    FilledButton(
                      onPressed: _enviandoAgente ? null : _pedirAgente,
                      child: Text(_enviandoAgente ? 'Enviando…' : 'Pedir que me contacten'),
                    ),
                  ],
                ],
              ),
            ),
            const Divider(height: 1),
            Padding(
              padding: const EdgeInsets.all(12),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _entrada,
                      decoration: const InputDecoration(hintText: 'Escribe tu mensaje'),
                      onSubmitted: _preguntar,
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton.filled(
                    icon: const Icon(Icons.send),
                    onPressed: _consultando ? null : () => _preguntar(_entrada.text),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Burbuja extends StatelessWidget {
  const _Burbuja({required this.mensaje});

  final _Mensaje mensaje;

  @override
  Widget build(BuildContext context) {
    final esBot = mensaje.de == 'bot';

    return Align(
      alignment: esBot ? Alignment.centerLeft : Alignment.centerRight,
      child: Container(
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.75),
        decoration: BoxDecoration(
          color: esBot ? Marca.superficie : Marca.texto,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: esBot ? Marca.borde : Marca.texto),
        ),
        child: Text(
          mensaje.texto,
          style: TextStyle(color: esBot ? Marca.texto : Marca.fondo),
        ),
      ),
    );
  }
}

class _BotonAtajo extends StatelessWidget {
  const _BotonAtajo({required this.texto, required this.alTocar});

  final String texto;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: OutlinedButton(
        onPressed: alTocar,
        style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(44)),
        child: Text(texto, textAlign: TextAlign.center),
      ),
    );
  }
}
