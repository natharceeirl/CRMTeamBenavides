import { describe, expect, it } from 'vitest'
import { pasosDeAvance } from './avance'
import { ESTADO } from '../api/ordenes'

const ingreso = '2026-09-24T14:15:00Z'
const diagnostico = '2026-09-24T16:40:00Z'
const aprobada = '2026-09-24T20:02:00Z'
const estimada = '2026-09-26T17:00:00Z'

describe('pasosDeAvance', () => {
  it('marca lo hecho con su fecha, el paso actual y la entrega estimada', () => {
    const pasos = pasosDeAvance({
      estadoId: ESTADO.aprobada,
      fechaApertura: ingreso,
      fechaIngreso: ingreso,
      fechaEstimadaEntrega: estimada,
      historial: [
        { estadoNuevoId: ESTADO.diagnostico, fechaCambio: diagnostico },
        { estadoNuevoId: ESTADO.aprobada, fechaCambio: aprobada },
      ],
    })

    expect(pasos.map((paso) => paso.titulo)).toEqual([
      'Recibida',
      'Diagnóstico',
      'Presupuesto aprobado',
      'En reparación',
      'Lista para recoger',
      'Entregada',
    ])
    expect(pasos[0]).toMatchObject({ situacion: 'hecho', fecha: ingreso })
    expect(pasos[1]).toMatchObject({ situacion: 'hecho', fecha: diagnostico })
    expect(pasos[2]).toMatchObject({ situacion: 'actual', fecha: aprobada })
    expect(pasos[3]).toMatchObject({ situacion: 'pendiente', fecha: null })
    expect(pasos[4]).toMatchObject({ situacion: 'pendiente', fecha: estimada, estimada: true })
  })

  it('en un reingreso «Lista» vuelve a quedar pendiente', () => {
    const pasos = pasosDeAvance({
      estadoId: ESTADO.enProceso,
      fechaApertura: ingreso,
      historial: [
        { estadoNuevoId: ESTADO.enProceso, fechaCambio: '2026-09-25T13:00:00Z' },
        { estadoNuevoId: ESTADO.lista, fechaCambio: '2026-09-25T18:00:00Z' },
        { estadoNuevoId: ESTADO.enProceso, fechaCambio: '2026-09-26T09:00:00Z' },
      ],
    })

    expect(pasos[3]).toMatchObject({ situacion: 'actual', fecha: '2026-09-26T09:00:00Z' })
    expect(pasos[4].situacion).toBe('pendiente')
  })
})
