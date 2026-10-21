// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFalsa, montar } from '../test/pantalla'
import { achicarFoto } from '../utils/imagen'
import { ModalFotosOrden } from './ModalFotosOrden'

// jsdom no dibuja en un canvas: el achicado se prueba aparte y aquí se simula su resultado.
vi.mock('../utils/imagen', async (original) => ({
  ...(await original<typeof import('../utils/imagen')>()),
  achicarFoto: vi.fn(),
}))

const MB = 1024 * 1024

/** Un archivo con el peso indicado, sin reservar esa memoria en la prueba. */
function foto(nombre: string, bytes: number, tipo = 'image/jpeg') {
  const archivo = new File(['x'], nombre, { type: tipo })
  Object.defineProperty(archivo, 'size', { value: bytes })
  return archivo
}

async function abrirFormulario() {
  const usuario = userEvent.setup({ applyAccept: false })
  montar(<ModalFotosOrden abierto ordenServicioId="os1" numeroOrden="OS-000091" onCerrar={vi.fn()} />)
  await usuario.click(await screen.findByRole('button', { name: 'Agregar foto' }))
  const entrada = document.querySelector('input[type="file"]') as HTMLInputElement
  return { usuario, entrada }
}

describe('subir una foto desde la web', () => {
  beforeEach(() => {
    vi.mocked(achicarFoto).mockReset()
  })

  it('se achica antes de subirla y se sube la achicada', async () => {
    vi.mocked(achicarFoto).mockResolvedValue(foto('moto.jpg', 412 * 1024))
    const { llamadas } = apiFalsa({ 'GET /ordenes-servicio/:id/fotos': [], 'POST /ordenes-servicio/:id/fotos': {} })
    const { usuario, entrada } = await abrirFormulario()

    await usuario.upload(entrada, foto('IMG_2041.jpeg', 4.8 * MB))

    expect(await screen.findByText('moto.jpg · 412 KB')).toBeTruthy()
    await usuario.click(screen.getByRole('button', { name: 'Guardar foto' }))
    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.metodo === 'POST')).toBe(true))
    const enviada = llamadas.find((llamada) => llamada.metodo === 'POST')?.cuerpo as FormData
    expect((enviada.get('archivo') as File).name).toBe('moto.jpg')
  })

  it('una foto de más de 25 MB ni se intenta achicar', async () => {
    apiFalsa({ 'GET /ordenes-servicio/:id/fotos': [] })
    const { usuario, entrada } = await abrirFormulario()

    await usuario.upload(entrada, foto('panoramica.jpg', 30 * MB))

    expect(await screen.findByText('La foto pesa más de 25 MB.')).toBeTruthy()
    expect(achicarFoto).not.toHaveBeenCalled()
  })

  it('si aun achicada pasa el límite de la API, lo dice y no deja guardarla', async () => {
    vi.mocked(achicarFoto).mockResolvedValue(foto('detalle.jpg', 3.4 * MB))
    apiFalsa({ 'GET /ordenes-servicio/:id/fotos': [] })
    const { usuario, entrada } = await abrirFormulario()

    await usuario.upload(entrada, foto('detalle.png', 9 * MB, 'image/png'))

    expect(await screen.findByText('Aun achicada, la foto pesa 3.4 MB; el máximo es 3 MB.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Guardar foto' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('un archivo que no es foto se rechaza', async () => {
    apiFalsa({ 'GET /ordenes-servicio/:id/fotos': [] })
    const { usuario, entrada } = await abrirFormulario()

    await usuario.upload(entrada, foto('presupuesto.pdf', 200 * 1024, 'application/pdf'))

    expect(await screen.findByText('Solo se aceptan fotos JPG, PNG o WEBP.')).toBeTruthy()
    expect(achicarFoto).not.toHaveBeenCalled()
  })
})
