import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../api/estados.dart';
import '../api/modelos.dart';
import '../auth/sesion.dart';
import '../formato.dart';
import '../tema.dart';
import 'comunes.dart';

// La foto se reduce antes de subirla: alcanza para ver el detalle de la unidad
// y pesa una fracción de lo que saca la cámara.
const _ladoMaximo = 1920.0;
const _calidad = 80;

/// Fotos de la orden. El personal las toma con la cámara o las elige de la
/// galería; cada una lleva la etapa en que se tomó y una observación opcional.
class SeccionFotosOrden extends ConsumerStatefulWidget {
  const SeccionFotosOrden({
    required this.ordenId,
    required this.estadoOrden,
    required this.puedeSubir,
    super.key,
  });

  final String ordenId;
  final int estadoOrden;
  final bool puedeSubir;

  @override
  ConsumerState<SeccionFotosOrden> createState() => _SeccionFotosOrdenState();
}

class _SeccionFotosOrdenState extends ConsumerState<SeccionFotosOrden> {
  bool _subiendo = false;
  String? _error;

  Future<void> _agregar(ImageSource origen) async {
    setState(() => _error = null);

    final XFile? foto;
    try {
      foto = await ImagePicker().pickImage(
        source: origen,
        maxWidth: _ladoMaximo,
        maxHeight: _ladoMaximo,
        imageQuality: _calidad,
      );
    } catch (_) {
      // Sin permiso de cámara o de fotos, o en un simulador sin cámara.
      if (mounted) {
        setState(() => _error = origen == ImageSource.camera
            ? 'No se pudo abrir la cámara. Revisa el permiso en los ajustes del teléfono.'
            : 'No se pudo abrir la galería. Revisa el permiso en los ajustes del teléfono.');
      }
      return;
    }
    if (foto == null || !mounted) return;

    final datos = await showModalBottomSheet<_DatosFoto>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => _HojaDatosFoto(etapaInicial: etapaFotoSugerida(widget.estadoOrden)),
    );
    if (datos == null || !mounted) return;

    setState(() => _subiendo = true);
    try {
      await ref.read(apiProvider).subirFotoOrden(
            widget.ordenId,
            bytes: await foto.readAsBytes(),
            nombreArchivo: foto.name,
            etapa: datos.etapa,
            observacion: datos.observacion,
          );
      ref.invalidate(fotosOrdenProvider(widget.ordenId));
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Foto agregada a la orden')));
      }
    } catch (fallo) {
      if (mounted) {
        setState(() => _error = '$fallo');
      }
    } finally {
      if (mounted) {
        setState(() => _subiendo = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final fotos = ref.watch(fotosOrdenProvider(widget.ordenId));

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (widget.puedeSubir)
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    style: OutlinedButton.styleFrom(minimumSize: const Size(0, 44)),
                    icon: const Icon(Icons.photo_camera_outlined),
                    label: const Text('Tomar foto'),
                    onPressed: _subiendo ? null : () => _agregar(ImageSource.camera),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton.icon(
                    style: OutlinedButton.styleFrom(minimumSize: const Size(0, 44)),
                    icon: const Icon(Icons.photo_library_outlined),
                    label: const Text('De la galería'),
                    onPressed: _subiendo ? null : () => _agregar(ImageSource.gallery),
                  ),
                ),
              ],
            ),
          if (_subiendo)
            const Padding(
              padding: EdgeInsets.only(top: 12),
              child: LinearProgressIndicator(),
            ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(_error!, style: const TextStyle(color: Marca.acento)),
            ),
          const SizedBox(height: 12),
          fotos.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => AvisoError(
              error: error,
              alReintentar: () => ref.invalidate(fotosOrdenProvider(widget.ordenId)),
            ),
            data: (lista) => lista.isEmpty
                ? const ListaVacia(mensaje: 'Todavía no hay fotos de esta orden.')
                : GridView.count(
                    crossAxisCount: 3,
                    mainAxisSpacing: 8,
                    crossAxisSpacing: 8,
                    shrinkWrap: true,
                    physics: const NeverScrollableScrollPhysics(),
                    children: [
                      for (final foto in lista)
                        _Miniatura(
                          foto: foto,
                          alTocar: () => Navigator.of(context).push(
                            MaterialPageRoute<void>(
                              fullscreenDialog: true,
                              builder: (_) => _PantallaFoto(
                                ordenId: widget.ordenId,
                                foto: foto,
                                puedeEliminar: widget.puedeSubir,
                              ),
                            ),
                          ),
                        ),
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}

class _DatosFoto {
  const _DatosFoto(this.etapa, this.observacion);

  final int etapa;
  final String? observacion;
}

/// Etapa y observación de la foto antes de subirla.
class _HojaDatosFoto extends StatefulWidget {
  const _HojaDatosFoto({required this.etapaInicial});

  final int etapaInicial;

  @override
  State<_HojaDatosFoto> createState() => _HojaDatosFotoState();
}

class _HojaDatosFotoState extends State<_HojaDatosFoto> {
  late int _etapa = widget.etapaInicial;
  final _observacion = TextEditingController();

  @override
  void dispose() {
    _observacion.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.fromLTRB(20, 16, 20, 16 + MediaQuery.viewInsetsOf(context).bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Foto de la orden',
            style: TextStyle(fontFamily: Marca.fuenteTitulos, fontSize: 18, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final entrada in nombresEtapaFoto.entries)
                ChoiceChip(
                  label: Text(entrada.value),
                  selected: _etapa == entrada.key,
                  onSelected: (_) => setState(() => _etapa = entrada.key),
                ),
            ],
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _observacion,
            maxLines: 2,
            maxLength: 500,
            decoration: const InputDecoration(
              labelText: 'Observación (opcional)',
              hintText: 'Rayón en el tanque, fuga en el retén…',
            ),
          ),
          const SizedBox(height: 8),
          FilledButton(
            style: FilledButton.styleFrom(minimumSize: const Size(0, 48)),
            onPressed: () => Navigator.of(context).pop(_DatosFoto(_etapa, _observacion.text.trim())),
            child: const Text('Subir foto'),
          ),
        ],
      ),
    );
  }
}

class _Miniatura extends ConsumerWidget {
  const _Miniatura({required this.foto, required this.alTocar});

  final FotoOrdenApi foto;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final archivo = ref.watch(archivoFotoProvider(foto.urlRelativa));

    return InkWell(
      onTap: alTocar,
      child: Stack(
        fit: StackFit.expand,
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(6),
            child: ColoredBox(
              color: Marca.fondo,
              child: archivo.when(
                loading: () => const Center(child: CircularProgressIndicator(strokeWidth: 2)),
                error: (_, _) => const Icon(Icons.broken_image_outlined, color: Marca.textoSecundario),
                data: (bytes) => Image.memory(Uint8List.fromList(bytes), fit: BoxFit.cover, gaplessPlayback: true),
              ),
            ),
          ),
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
              decoration: BoxDecoration(
                color: Colors.black.withValues(alpha: 0.55),
                borderRadius: const BorderRadius.vertical(bottom: Radius.circular(6)),
              ),
              child: Text(
                foto.nombreEtapa,
                style: const TextStyle(color: Colors.white, fontSize: 12),
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// La foto en grande, con zoom, sus datos y la opción de quitarla.
class _PantallaFoto extends ConsumerStatefulWidget {
  const _PantallaFoto({required this.ordenId, required this.foto, required this.puedeEliminar});

  final String ordenId;
  final FotoOrdenApi foto;
  final bool puedeEliminar;

  @override
  ConsumerState<_PantallaFoto> createState() => _PantallaFotoState();
}

class _PantallaFotoState extends ConsumerState<_PantallaFoto> {
  bool _eliminando = false;

  Future<void> _eliminar() async {
    final confirmado = await showDialog<bool>(
      context: context,
      builder: (contexto) => AlertDialog(
        title: const Text('Quitar la foto'),
        content: const Text('La foto deja de verse en la orden.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(contexto).pop(false), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.of(contexto).pop(true), child: const Text('Quitar')),
        ],
      ),
    );
    if (confirmado != true || !mounted) return;

    setState(() => _eliminando = true);
    try {
      await ref.read(apiProvider).eliminarFotoOrden(widget.ordenId, widget.foto.id);
      ref.invalidate(fotosOrdenProvider(widget.ordenId));
      if (mounted) {
        Navigator.of(context).pop();
      }
    } catch (fallo) {
      if (mounted) {
        setState(() => _eliminando = false);
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$fallo')));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final archivo = ref.watch(archivoFotoProvider(widget.foto.urlRelativa));
    final foto = widget.foto;

    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: Text(foto.nombreEtapa),
        actions: [
          if (widget.puedeEliminar)
            IconButton(
              tooltip: 'Quitar la foto',
              icon: const Icon(Icons.delete_outline),
              onPressed: _eliminando ? null : _eliminar,
            ),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: archivo.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => AvisoError(error: error),
              data: (bytes) => InteractiveViewer(
                maxScale: 5,
                child: Center(child: Image.memory(Uint8List.fromList(bytes))),
              ),
            ),
          ),
          Container(
            width: double.infinity,
            color: Colors.black,
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  [fechaHora(foto.fechaCreacion), foto.usuarioNombre].whereType<String>().join(' · '),
                  style: const TextStyle(color: Colors.white70),
                ),
                if (foto.observacion != null && foto.observacion!.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 6),
                    child: Text(foto.observacion!, style: const TextStyle(color: Colors.white)),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
