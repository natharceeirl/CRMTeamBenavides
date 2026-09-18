String _dos(int valor) => valor.toString().padLeft(2, '0');

/// Montos en soles, como en la web.
String soles(double monto) => 'S/ ${monto.toStringAsFixed(2)}';

/// Fechas de la API (UTC) en hora local, como «18/09 08:45».
String fechaHora(DateTime? fecha) {
  if (fecha == null) {
    return '—';
  }

  final local = fecha.toLocal();
  return '${_dos(local.day)}/${_dos(local.month)} ${_dos(local.hour)}:${_dos(local.minute)}';
}
