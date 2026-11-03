// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { apiFalsa, montar, usuarioConSesion } from '../test/pantalla'
import { soles } from '../utils/formato'
import { TableroPage } from './TableroPage'

const cajaAbierta = (saldo: number) => ({
  tieneCajaAbierta: true,
  caja: { id: 'c1', montoApertura: 200, saldoCalculado: saldo, movimientos: [] },
})

describe('caja en el tablero', () => {
  it('recién abierta con S/ 200 muestra los 200', async () => {
    apiFalsa({
      ...usuarioConSesion({ roles: ['Recepcion'], permisos: ['reportes.ver_operativos', 'caja.consultar'] }),
      'GET /caja-chica/actual': () => cajaAbierta(200),
    })
    montar(<TableroPage />)

    expect(await screen.findByText('Caja chica')).toBeTruthy()
    expect(await screen.findByText(soles(200))).toBeTruthy()
  })

  it('sin caja abierta dice que está cerrada', async () => {
    apiFalsa({
      ...usuarioConSesion({ roles: ['Recepcion'], permisos: ['reportes.ver_operativos', 'caja.consultar'] }),
      'GET /caja-chica/actual': () => ({ tieneCajaAbierta: false, caja: null }),
    })
    montar(<TableroPage />)

    expect(await screen.findByText('Cerrada')).toBeTruthy()
  })

  it('sin caja.consultar no aparece la caja', async () => {
    const { llamadas } = apiFalsa({
      ...usuarioConSesion({ roles: ['Vendedor'], permisos: ['reportes.ver_operativos'] }),
    })
    montar(<TableroPage />)

    expect(await screen.findByText('Productos con stock bajo')).toBeTruthy()
    expect(screen.queryByText('Caja chica')).toBeNull()
    expect(llamadas.some((llamada) => llamada.ruta === '/caja-chica/actual')).toBe(false)
  })
})
