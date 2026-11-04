// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { apiFalsa, montar } from '../test/pantalla'
import { ModalAltaRapidaRepuesto } from './ModalesAltaRapida'

const registrarCon = (llamadas: { ruta: string; cuerpo: unknown }[]) =>
  JSON.parse(String(llamadas.find((llamada) => llamada.ruta === '/productos/alta-rapida')?.cuerpo))

describe('registrar un repuesto rápido', () => {
  it('por defecto el precio va en soles y con IGV', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({
      'GET /productos/marcas': () => [],
      'POST /productos/alta-rapida': () => ({ id: 'p1' }),
    })
    montar(<ModalAltaRapidaRepuesto abierto onCerrar={vi.fn()} onRegistrado={vi.fn()} />)

    await usuario.type(screen.getByLabelText('Nombre'), 'Filtro de aceite')
    await usuario.type(screen.getByLabelText('Precio de venta'), '35')
    expect((screen.getByRole('checkbox', { name: 'Incluye IGV' }) as HTMLInputElement).checked).toBe(true)
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }))

    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.ruta === '/productos/alta-rapida')).toBe(true))
    expect(registrarCon(llamadas)).toMatchObject({ precioVenta: 35, monedaPrecio: 0, precioIncluyeIgv: true })
  })

  it('en dólares y sin IGV manda la moneda y la casilla desmarcada', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({
      'GET /productos/marcas': () => [],
      'POST /productos/alta-rapida': () => ({ id: 'p1' }),
    })
    montar(<ModalAltaRapidaRepuesto abierto onCerrar={vi.fn()} onRegistrado={vi.fn()} />)

    await usuario.type(screen.getByLabelText('Nombre'), 'Casco')
    await usuario.click(screen.getByRole('combobox', { name: 'Moneda' }))
    await usuario.click(await screen.findByTitle('Dólares'))
    expect(await screen.findByText('Se cobra en soles al tipo de cambio del día.')).toBeTruthy()
    await usuario.type(screen.getByLabelText('Precio de venta'), '25')
    await usuario.click(screen.getByRole('checkbox', { name: 'Incluye IGV' }))
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }))

    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.ruta === '/productos/alta-rapida')).toBe(true))
    expect(registrarCon(llamadas)).toMatchObject({ precioVenta: 25, monedaPrecio: 1, precioIncluyeIgv: false })
  })
})
