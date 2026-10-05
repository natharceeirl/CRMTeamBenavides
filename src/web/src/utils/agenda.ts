import dayjs, { type Dayjs } from 'dayjs'

const NOMBRES_DIA = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado']

/** El lunes de la semana de la fecha, a las 00:00: la agenda va de lunes a domingo. */
export const lunesDe = (fecha: Dayjs) => fecha.startOf('day').subtract((fecha.day() + 6) % 7, 'day')

export const diasDeLaSemana = (lunes: Dayjs) => Array.from({ length: 7 }, (_, indice) => lunes.add(indice, 'day'))

export const nombreDia = (dia: Dayjs) => NOMBRES_DIA[dia.day()]

export const claveDia = (fecha: Dayjs | string) => dayjs(fecha).format('YYYY-MM-DD')

/** «28/09 – 04/10/2026», o con los dos años si la semana cruza el año. */
export function rangoSemana(lunes: Dayjs): string {
  const domingo = lunes.add(6, 'day')
  const desde = lunes.year() === domingo.year() ? lunes.format('DD/MM') : lunes.format('DD/MM/YYYY')
  return `${desde} – ${domingo.format('DD/MM/YYYY')}`
}

/** Agrupa las citas por día local, y cada día en orden de hora. */
export function citasPorDia<T extends { fechaHoraProgramada: string }>(citas: readonly T[]): Map<string, T[]> {
  const dias = new Map<string, T[]>()
  const enOrden = [...citas].sort(
    (una, otra) => new Date(una.fechaHoraProgramada).getTime() - new Date(otra.fechaHoraProgramada).getTime(),
  )
  for (const cita of enOrden) {
    const clave = claveDia(cita.fechaHoraProgramada)
    dias.set(clave, [...(dias.get(clave) ?? []), cita])
  }
  return dias
}

/** «30 min», «1 h», «1 h 30 min». */
export function textoDuracion(minutos: number): string {
  const horas = Math.floor(minutos / 60)
  const resto = minutos % 60
  if (horas === 0) return `${resto} min`
  return resto === 0 ? `${horas} h` : `${horas} h ${resto} min`
}

export const DURACIONES_CITA = [30, 60, 90, 120, 180, 240].map((minutos) => ({
  value: minutos,
  label: textoDuracion(minutos),
}))

/** La cita no se agenda en el pasado: el backend no lo controla. */
export const motivoFechaCitaInvalida = (fecha: Dayjs | null | undefined, ahora: Dayjs = dayjs()) => {
  if (!fecha) return 'Elige la fecha y la hora'
  return fecha.isBefore(ahora) ? 'La cita no puede quedar en el pasado' : null
}

/** La regla del campo de fecha y hora en los formularios de citas. */
export const reglaFechaCita = {
  validator: (_: unknown, fecha: Dayjs | null | undefined) => {
    const motivo = motivoFechaCitaInvalida(fecha)
    return motivo ? Promise.reject(new Error(motivo)) : Promise.resolve()
  },
}
