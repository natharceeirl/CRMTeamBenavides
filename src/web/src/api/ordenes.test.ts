import dayjs from 'dayjs'
import { describe, expect, it } from 'vitest'
import {
  ESTADO,
  GERENCIA,
  PRESUPUESTO,
  esEstadoTerminal,
  fechaIngresoOrden,
  motivoBloqueoAprobacion,
  motivoEntregaInvalida,
  nombresEstado,
  permiteEditarDetalles,
  esperaRespuestaDelCliente,
  etiquetaPresupuesto,
  respuestasPresupuesto,
  transicionesValidas,
} from './ordenes'

/**
 * La web solo puede ofrecer las transiciones que el backend acepta
 * (OrdenServicioService.CambiarEstadoAsync). Si alguien cambia una, esta prueba
 * avisa antes de que el usuario se coma un 400.
 */
describe('transiciones de estado', () => {
  it('desde Abierta solo se puede diagnosticar o anular', () => {
    expect(transicionesValidas[ESTADO.abierta]).toEqual([ESTADO.diagnostico, ESTADO.cancelada])
  })

  it('desde Diagnóstico solo se puede aprobar o anular', () => {
    expect(transicionesValidas[ESTADO.diagnostico]).toEqual([ESTADO.aprobada, ESTADO.cancelada])
  })

  it('desde Lista se puede entregar, devolver a proceso o anular', () => {
    expect(transicionesValidas[ESTADO.lista]).toEqual([
      ESTADO.entregada,
      ESTADO.enProceso,
      ESTADO.cancelada,
    ])
  })

  it('Entregada y Cancelada son terminales', () => {
    expect(transicionesValidas[ESTADO.entregada]).toEqual([])
    expect(transicionesValidas[ESTADO.cancelada]).toEqual([])
    expect(esEstadoTerminal(ESTADO.entregada)).toBe(true)
    expect(esEstadoTerminal(ESTADO.cancelada)).toBe(true)
    expect(esEstadoTerminal(ESTADO.enProceso)).toBe(false)
  })

  it('nunca ofrece volver al estado en el que ya está', () => {
    for (const [origen, destinos] of Object.entries(transicionesValidas)) {
      expect(destinos).not.toContain(Number(origen))
    }
  })

  it('todos los destinos son estados conocidos', () => {
    for (const destinos of Object.values(transicionesValidas)) {
      for (const destino of destinos) {
        expect(nombresEstado[destino]).toBeDefined()
      }
    }
  })
})

describe('edición de detalles', () => {
  it('deja editar mientras la orden está en taller', () => {
    expect(permiteEditarDetalles(ESTADO.abierta)).toBe(true)
    expect(permiteEditarDetalles(ESTADO.diagnostico)).toBe(true)
    expect(permiteEditarDetalles(ESTADO.aprobada)).toBe(true)
    expect(permiteEditarDetalles(ESTADO.enProceso)).toBe(true)
  })

  it('bloquea cuando ya está lista, entregada o anulada', () => {
    // El backend rechaza estos tres casos; la web no debe ni ofrecerlo.
    expect(permiteEditarDetalles(ESTADO.lista)).toBe(false)
    expect(permiteEditarDetalles(ESTADO.entregada)).toBe(false)
    expect(permiteEditarDetalles(ESTADO.cancelada)).toBe(false)
  })
})

/** Mismas reglas que CambiarEstadoAsync aplica antes de pasar a «Aprobada». */
describe('motivoBloqueoAprobacion', () => {
  it('deja aprobar solo con el presupuesto aprobado y sin Gerencia de por medio', () => {
    expect(
      motivoBloqueoAprobacion({ estadoPresupuestoClienteId: PRESUPUESTO.pendiente, estadoAprobacionGerenciaId: GERENCIA.noAplica }),
    ).toMatch(/Falta que el cliente apruebe/)
    expect(
      motivoBloqueoAprobacion({ estadoPresupuestoClienteId: PRESUPUESTO.aprobado, estadoAprobacionGerenciaId: GERENCIA.aprobado }),
    ).toBeNull()
  })

  it('bloquea si el cliente rechazó', () => {
    expect(
      motivoBloqueoAprobacion({ estadoPresupuestoClienteId: PRESUPUESTO.rechazado, estadoAprobacionGerenciaId: GERENCIA.noAplica }),
    ).toMatch(/rechazó el presupuesto/)
  })

  it('bloquea si Gerencia está pendiente o rechazó', () => {
    expect(
      motivoBloqueoAprobacion({ estadoPresupuestoClienteId: PRESUPUESTO.aprobado, estadoAprobacionGerenciaId: GERENCIA.pendiente }),
    ).toMatch(/Falta la aprobación de Gerencia/)
    expect(
      motivoBloqueoAprobacion({ estadoPresupuestoClienteId: PRESUPUESTO.aprobado, estadoAprobacionGerenciaId: GERENCIA.rechazado }),
    ).toMatch(/Gerencia rechazó/)
  })

  it('sin respuesta del cliente registrada, tampoco se aprueba', () => {
    expect(motivoBloqueoAprobacion({})).toMatch(/Falta que el cliente apruebe/)
  })
})

describe('fechas de la orden', () => {
  it('las órdenes antiguas traen el ingreso en 0001-01-01: vale la apertura', () => {
    const apertura = '2026-09-12T14:30:00Z'
    expect(fechaIngresoOrden({ fechaApertura: apertura, fechaIngreso: '0001-01-01T00:00:00' })).toBe(apertura)
    expect(fechaIngresoOrden({ fechaApertura: apertura })).toBe(apertura)
    expect(fechaIngresoOrden({ fechaApertura: apertura, fechaIngreso: '2026-09-29T13:56:53Z' })).toBe('2026-09-29T13:56:53Z')
  })

  it('la entrega estimada no puede quedar antes del ingreso', () => {
    const ingreso = dayjs('2026-09-29T09:00')
    expect(motivoEntregaInvalida(dayjs('2026-09-01T10:00'), ingreso)).toContain('no puede ser anterior al ingreso (29/09/2026 09:00)')
    expect(motivoEntregaInvalida(dayjs('2026-09-29T09:00'), ingreso)).toBeNull()
    expect(motivoEntregaInvalida(dayjs('2026-10-02T18:00'), ingreso)).toBeNull()
    expect(motivoEntregaInvalida(null, ingreso)).toBeNull()
  })
})

describe('respuestasPresupuesto', () => {
  it('ofrece aprobar y rechazar con la orden en diagnóstico y el presupuesto armado', () => {
    expect(respuestasPresupuesto(ESTADO.diagnostico, PRESUPUESTO.pendiente, 120)).toEqual([
      PRESUPUESTO.aprobado,
      PRESUPUESTO.rechazado,
    ])
  })

  it('no ofrece nada antes del diagnóstico ni sin ítems', () => {
    expect(respuestasPresupuesto(ESTADO.abierta, PRESUPUESTO.pendiente, 120)).toEqual([])
    expect(respuestasPresupuesto(ESTADO.diagnostico, PRESUPUESTO.pendiente, 0)).toEqual([])
  })

  it('después de aprobado no ofrece nada, ni para cambiar a rechazado', () => {
    expect(respuestasPresupuesto(ESTADO.diagnostico, PRESUPUESTO.aprobado, 120)).toEqual([])
  })

  it('rechazado y aún en diagnóstico, solo se puede aprobar', () => {
    expect(respuestasPresupuesto(ESTADO.diagnostico, PRESUPUESTO.rechazado, 120)).toEqual([PRESUPUESTO.aprobado])
  })

  it('con la orden ya aprobada, en proceso, lista o cerrada no se responde', () => {
    for (const estado of [ESTADO.aprobada, ESTADO.enProceso, ESTADO.lista, ESTADO.entregada, ESTADO.cancelada]) {
      expect(respuestasPresupuesto(estado, PRESUPUESTO.pendiente, 120)).toEqual([])
    }
  })
})

describe('etiquetaPresupuesto', () => {
  it('dice «Pendiente» solo mientras se espera la respuesta', () => {
    expect(etiquetaPresupuesto(ESTADO.diagnostico, PRESUPUESTO.pendiente, 120)).toEqual({ texto: 'Pendiente', tono: 'alerta' })
    expect(etiquetaPresupuesto(ESTADO.abierta, PRESUPUESTO.pendiente, 0).texto).toBe('En preparación')
    expect(etiquetaPresupuesto(ESTADO.lista, PRESUPUESTO.pendiente, 120)).toEqual({ texto: 'Sin respuesta', tono: 'apagado' })
    expect(etiquetaPresupuesto(ESTADO.lista, PRESUPUESTO.aprobado, 120).texto).toBe('Aprobado')
  })

  it('pide respuesta al cliente con el diagnóstico hecho y el presupuesto armado', () => {
    expect(esperaRespuestaDelCliente({ estadoId: ESTADO.diagnostico, estadoPresupuestoClienteId: PRESUPUESTO.pendiente, total: 120 })).toBe(true)
    expect(esperaRespuestaDelCliente({ estadoId: ESTADO.diagnostico, estadoPresupuestoClienteId: PRESUPUESTO.pendiente, total: 0 })).toBe(false)
    expect(esperaRespuestaDelCliente({ estadoId: ESTADO.abierta, estadoPresupuestoClienteId: PRESUPUESTO.pendiente, total: 120 })).toBe(false)
  })
})
