import dayjs from 'dayjs'
import { describe, expect, it } from 'vitest'
import {
  citasPorDia,
  claveDia,
  diasDeLaSemana,
  lunesDe,
  motivoFechaCitaInvalida,
  nombreDia,
  rangoSemana,
  textoDuracion,
} from './agenda'
import { citaAntesDelTaller, esCitaFinal, rutaCitas, siguientesEstadosCita } from '../api/citas'
import { ESTADO_CITA } from '../api/tipos'

describe('semana de la agenda', () => {
  it('empieza el lunes, también si la fecha cae en domingo', () => {
    expect(claveDia(lunesDe(dayjs('2026-10-01T15:00')))).toBe('2026-09-28') // jueves
    expect(claveDia(lunesDe(dayjs('2026-10-04T10:00')))).toBe('2026-09-28') // domingo
    expect(claveDia(lunesDe(dayjs('2026-09-28T00:00')))).toBe('2026-09-28') // el mismo lunes
    expect(lunesDe(dayjs('2026-10-01T15:00')).hour()).toBe(0)
  })

  it('tiene siete días de lunes a domingo', () => {
    const dias = diasDeLaSemana(lunesDe(dayjs('2026-10-01')))
    expect(dias).toHaveLength(7)
    expect(nombreDia(dias[0])).toBe('Lunes')
    expect(nombreDia(dias[6])).toBe('Domingo')
  })

  it('muestra el rango, con los dos años si cruza el año', () => {
    expect(rangoSemana(dayjs('2026-09-28'))).toBe('28/09 – 04/10/2026')
    expect(rangoSemana(dayjs('2026-12-28'))).toBe('28/12/2026 – 03/01/2027')
  })
})

describe('citasPorDia', () => {
  it('agrupa por día local y ordena por hora', () => {
    const tarde = { id: 'b', fechaHoraProgramada: dayjs('2026-10-01T16:00').toISOString() }
    const manana = { id: 'a', fechaHoraProgramada: dayjs('2026-10-01T09:00').toISOString() }
    const otroDia = { id: 'c', fechaHoraProgramada: dayjs('2026-10-02T09:00').toISOString() }
    const dias = citasPorDia([tarde, otroDia, manana])
    expect(dias.get('2026-10-01')?.map((cita) => cita.id)).toEqual(['a', 'b'])
    expect(dias.get('2026-10-02')?.map((cita) => cita.id)).toEqual(['c'])
  })
})

describe('textoDuracion', () => {
  it('lee los minutos como horas', () => {
    expect(textoDuracion(30)).toBe('30 min')
    expect(textoDuracion(60)).toBe('1 h')
    expect(textoDuracion(90)).toBe('1 h 30 min')
  })
})

describe('motivoFechaCitaInvalida', () => {
  const ahora = dayjs('2026-10-02T10:00')

  it('pide la fecha y no deja agendar en el pasado', () => {
    expect(motivoFechaCitaInvalida(null, ahora)).toBe('Elige la fecha y la hora')
    expect(motivoFechaCitaInvalida(dayjs('2026-10-02T09:00'), ahora)).toBe('La cita no puede quedar en el pasado')
    expect(motivoFechaCitaInvalida(dayjs('2026-10-02T11:00'), ahora)).toBeNull()
  })
})

describe('estados de la cita', () => {
  it('sigue las transiciones del backend', () => {
    expect(siguientesEstadosCita(ESTADO_CITA.pendiente)).toEqual([
      ESTADO_CITA.confirmada,
      ESTADO_CITA.enTaller,
      ESTADO_CITA.noAsistio,
    ])
    // Una pendiente no se completa sin pasar por el taller, y desde el taller no se retrocede.
    expect(siguientesEstadosCita(ESTADO_CITA.pendiente)).not.toContain(ESTADO_CITA.completada)
    expect(siguientesEstadosCita(ESTADO_CITA.enTaller)).toEqual([ESTADO_CITA.completada])
    expect(siguientesEstadosCita(ESTADO_CITA.completada)).toEqual([])
  })

  it('se cancela o reprograma solo antes de llegar al taller', () => {
    expect(citaAntesDelTaller(ESTADO_CITA.confirmada)).toBe(true)
    expect(citaAntesDelTaller(ESTADO_CITA.enTaller)).toBe(false)
    expect(esCitaFinal(ESTADO_CITA.noAsistio)).toBe(true)
  })

  it('arma la ruta con los filtros', () => {
    expect(rutaCitas({})).toBe('/citas')
    expect(rutaCitas({ estado: ESTADO_CITA.pendiente, fechaInicio: 'a' }, '/citas/exportar-excel')).toBe(
      '/citas/exportar-excel?fechaInicio=a&estado=Pendiente',
    )
    expect(rutaCitas({ clienteId: 'k1' })).toBe('/citas?clienteId=k1')
  })
})
