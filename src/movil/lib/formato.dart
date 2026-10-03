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

const _dias = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

/// El día con su nombre: «Lun 05/10».
String diaConNombre(DateTime fecha) {
  final local = fecha.toLocal();
  return '${_dias[local.weekday - 1]} ${_dos(local.day)}/${_dos(local.month)}';
}

/// Fecha de una cita, con el día de la semana: «Lun 05/10 · 09:00».
String fechaConDia(DateTime fecha) {
  final local = fecha.toLocal();
  return '${diaConNombre(local)} · ${_dos(local.hour)}:${_dos(local.minute)}';
}

/// Solo el día: «05/10/2026».
String dia(DateTime fecha) => '${_dos(fecha.day)}/${_dos(fecha.month)}/${fecha.year}';

/// Hora de 24 horas: «09:00».
String horaMinuto(int hora, int minuto) => '${_dos(hora)}:${_dos(minuto)}';
