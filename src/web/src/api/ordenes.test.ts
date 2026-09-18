import { describe, expect, it } from 'vitest'
import {
  ESTADO,
  esEstadoTerminal,
  nombresEstado,
  permiteEditarDetalles,
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
