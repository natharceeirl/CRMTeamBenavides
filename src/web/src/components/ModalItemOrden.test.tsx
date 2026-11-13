// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { apiFalsa, montar } from '../test/pantalla'
import { ModalItemOrden } from './ModalItemOrden'

const rutaAgregar = '/ordenes-servicio/o1/detalles'
const agregadoCon = (llamadas: { ruta: string; cuerpo: unknown }[]) =>
  JSON.parse(String(llamadas.find((llamada) => llamada.ruta === rutaAgregar)?.cuerpo))

const api = () =>
  apiFalsa({
    'GET /productos': () => [],
    'GET /servicios': () => [],
    [`POST ${rutaAgregar}`]: () => ({ id: 'd1' }),
  })

async function manoDeObra(usuario: ReturnType<typeof userEvent.setup>, precio: string) {
  montar(<ModalItemOrden abierto ordenId="o1" onCerrar={vi.fn()} />)
  await usuario.click(screen.getByText('Mano de obra'))
  await usuario.type(screen.getByLabelText('Descripción del trabajo'), 'Limpiar y lubricar cadena')
  await usuario.type(screen.getByLabelText('Precio unitario (S/, con IGV)'), precio)
}

describe('agregar mano de obra a la orden', () => {
  it('por defecto el precio va con IGV', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = api()

    await manoDeObra(usuario, '80')
    expect((screen.getByRole('checkbox', { name: 'Incluye IGV' }) as HTMLInputElement).checked).toBe(true)
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }))

    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.ruta === rutaAgregar)).toBe(true))
    expect(agregadoCon(llamadas)).toMatchObject({ tipoItem: 2, precioUnitario: 80, precioIncluyeIgv: true })
  })

  it('al desmarcar «Incluye IGV» el precio va sin IGV', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = api()

    await manoDeObra(usuario, '67.8')
    await usuario.click(screen.getByRole('checkbox', { name: 'Incluye IGV' }))
    expect(screen.getByLabelText('Precio unitario (S/, sin IGV)')).toBeTruthy()
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }))

    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.ruta === rutaAgregar)).toBe(true))
    expect(agregadoCon(llamadas)).toMatchObject({ precioUnitario: 67.8, precioIncluyeIgv: false })
  })
})
