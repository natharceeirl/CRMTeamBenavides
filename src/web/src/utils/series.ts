/**
 * Cálculos de los gráficos: agrupar por día y elegir los cortes del eje.
 *
 * Viven acá y no dentro de los componentes para poder probarlos sin montar
 * nada, que es como está probado el resto de la web.
 */

export type PuntoSerie = {
  /** Día en hora local, como `2026-09-21`. Sirve de clave estable. */
  dia: string
  /** Lo que se escribe bajo el eje, como «21/09». */
  etiqueta: string
  valor: number
}

const dosDigitos = (valor: number) => String(valor).padStart(2, '0')

/** `2026-09-21` a partir de una fecha, siempre en hora local. */
export function claveDelDia(fecha: Date): string {
  return `${fecha.getFullYear()}-${dosDigitos(fecha.getMonth() + 1)}-${dosDigitos(fecha.getDate())}`
}

/** «21/09», que es como el resto de la web escribe los días. */
export function etiquetaDelDia(clave: string): string {
  const [, mes, dia] = clave.split('-')
  return `${dia}/${mes}`
}

/**
 * Suma los items por día dentro del rango, dejando en cero los días sin datos.
 *
 * Los días vacíos importan: si se saltan, una semana con ventas solo el lunes
 * y el viernes se dibuja como una línea que sube sin parar.
 */
export function serieDiaria<T>(
  items: readonly T[],
  leerFecha: (item: T) => string,
  leerValor: (item: T) => number,
  rango: { fechaDesde: string; fechaHasta: string },
): PuntoSerie[] {
  const desde = new Date(rango.fechaDesde)
  const hasta = new Date(rango.fechaHasta)
  if (Number.isNaN(desde.getTime()) || Number.isNaN(hasta.getTime()) || desde > hasta) {
    return []
  }

  const acumulado = new Map<string, number>()
  for (const cursor = new Date(desde); cursor <= hasta; cursor.setDate(cursor.getDate() + 1)) {
    acumulado.set(claveDelDia(cursor), 0)
  }

  for (const item of items) {
    const fecha = new Date(leerFecha(item))
    if (Number.isNaN(fecha.getTime())) {
      continue
    }
    const clave = claveDelDia(fecha)
    // Lo que cae fuera del rango se descarta: el eje no lo puede mostrar.
    if (!acumulado.has(clave)) {
      continue
    }
    acumulado.set(clave, (acumulado.get(clave) ?? 0) + leerValor(item))
  }

  return [...acumulado.entries()].map(([dia, valor]) => ({
    dia,
    etiqueta: etiquetaDelDia(dia),
    valor,
  }))
}

/**
 * Cortes redondos del eje vertical: siempre arrancan en cero y terminan en un
 * número que se lee de un vistazo (10, 25, 500…), nunca en el máximo crudo.
 */
export function ticksDeEje(
  maximo: number,
  { cantidad = 4, entero = false }: { cantidad?: number; entero?: boolean } = {},
): number[] {
  const tope = Number.isFinite(maximo) ? Math.max(maximo, 0) : 0
  // Sin datos el eje igual necesita altura, si no la línea se pega al borde.
  if (tope <= 0) {
    return [0, 1]
  }

  const pasoCrudo = tope / cantidad
  const magnitud = 10 ** Math.floor(Math.log10(pasoCrudo))
  const escalones = entero ? [1, 2, 5, 10] : [1, 2, 2.5, 5, 10]

  let paso =
    escalones.map((escalon) => escalon * magnitud).find((valor) => valor >= pasoCrudo) ??
    10 * magnitud
  if (entero) {
    paso = Math.max(1, Math.ceil(paso))
  }

  const cortes = Math.ceil(tope / paso)
  return Array.from({ length: cortes + 1 }, (_, indice) => Number((indice * paso).toFixed(6)))
}

/** El tope del eje: el último corte, que es el que fija la altura del gráfico. */
export function topeDeEje(cortes: readonly number[]): number {
  return cortes.length === 0 ? 1 : cortes[cortes.length - 1]
}

/**
 * Qué posiciones del eje horizontal llevan etiqueta.
 *
 * Un mes son treinta días y treinta fechas debajo del eje se pisan entre sí:
 * se reparten unas pocas, siempre con la primera y la última.
 */
export function indicesVisibles(cantidad: number, maximo = 6): number[] {
  if (cantidad <= 0) {
    return []
  }
  if (maximo <= 1 || cantidad === 1) {
    return [0]
  }
  if (cantidad <= maximo) {
    return Array.from({ length: cantidad }, (_, indice) => indice)
  }

  const paso = (cantidad - 1) / (maximo - 1)
  const indices = Array.from({ length: maximo }, (_, indice) => Math.round(indice * paso))
  return [...new Set(indices)]
}
