String _dos(int valor) => valor.toString().padLeft(2, '0');

/// Enteros con separador de miles, como la web (es-PE): «12,450».
String entero(num valor) {
  final texto = valor.round().abs().toString();
  final conMiles = texto.replaceAllMapped(RegExp(r'\B(?=(\d{3})+(?!\d))'), (_) => ',');
  return valor < 0 ? '-$conMiles' : conMiles;
}

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
