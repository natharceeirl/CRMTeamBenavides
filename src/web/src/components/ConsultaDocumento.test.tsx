// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { apiFalsa, montar, usuarioConSesion } from '../test/pantalla'
import { documentoConsultable } from '../api/consultaDocumento'
import { ModalCliente } from './ModalCliente'
import { ModalProveedor } from './ModalProveedor'

const recepcion = () => usuarioConSesion({ roles: ['Recepcion'], permisos: ['clientes.ver', 'clientes.crear'] })

const dni = {
  tipoDocumento: 'DNI',
  tipoDocumentoId: 0,
  numeroDocumento: '46027897',
  nombreCompleto: 'Roxana Karina Delgado Huamani',
  clienteExistenteId: null,
  clienteExistenteNombre: null,
}

describe('completar por DNI o RUC', () => {
  it('solo se consulta un DNI de 8 dígitos o un RUC de 11', () => {
    expect(documentoConsultable('46027897')).toBe(true)
    expect(documentoConsultable(' 20601030013 ')).toBe(true)
    expect(documentoConsultable('4602789')).toBe(false)
    expect(documentoConsultable('CE123456')).toBe(false)
  })

  it('al escribir el DNI del cliente se completa su nombre y el tipo de documento', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({
      ...recepcion(),
      'GET /consulta-documento/:numero': () => dni,
      'POST /clientes': () => ({ id: 'k1', nombreCompleto: 'Roxana Karina Delgado Huamani' }),
    })
    montar(<ModalCliente abierto onCerrar={vi.fn()} />)

    await usuario.type(await screen.findByLabelText('Número de documento'), '46027897')

    await vi.waitFor(() =>
      expect((screen.getByLabelText('Nombre completo') as HTMLInputElement).value).toBe('Roxana Karina Delgado Huamani'),
    )
    expect(screen.getByText('Datos de RENIEC. Revísalos antes de guardar.')).toBeTruthy()
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))
    await vi.waitFor(() => expect(llamadas.some((llamada) => llamada.metodo === 'POST' && llamada.ruta === '/clientes')).toBe(true))
    const cliente = JSON.parse(String(llamadas.find((llamada) => llamada.ruta === '/clientes' && llamada.metodo === 'POST')?.cuerpo))
    expect(cliente).toMatchObject({ nombreCompleto: 'Roxana Karina Delgado Huamani', tipoDocumento: 0, numeroDocumento: '46027897' })
    // Un solo pedido por número, aunque se escriba de a un dígito.
    expect(llamadas.filter((llamada) => llamada.ruta.startsWith('/consulta-documento')).length).toBe(1)
  })

  it('avisa si el documento ya es de un cliente y no pisa el nombre escrito a mano', async () => {
    const usuario = userEvent.setup()
    apiFalsa({
      ...recepcion(),
      'GET /consulta-documento/:numero': () => ({ ...dni, clienteExistenteId: 'k9', clienteExistenteNombre: 'Roxana Delgado' }),
    })
    montar(<ModalCliente abierto onCerrar={vi.fn()} />)

    await usuario.type(await screen.findByLabelText('Nombre completo'), 'Roxi Delgado')
    await usuario.type(screen.getByLabelText('Número de documento'), '46027897')

    expect(await screen.findByText('Ese documento ya es del cliente Roxana Delgado.')).toBeTruthy()
    expect((screen.getByLabelText('Nombre completo') as HTMLInputElement).value).toBe('Roxi Delgado')
  })

  it('si la consulta no está disponible, lo dice y el nombre se escribe a mano', async () => {
    const usuario = userEvent.setup()
    apiFalsa({
      ...recepcion(),
      'GET /consulta-documento/:numero': () =>
        new Response(JSON.stringify({ mensaje: 'Se acabaron las consultas de DNI y RUC del mes: escribe el nombre a mano.' }), {
          status: 503,
          headers: { 'content-type': 'application/json' },
        }),
    })
    montar(<ModalCliente abierto onCerrar={vi.fn()} />)

    await usuario.type(await screen.findByLabelText('Número de documento'), '46027897')

    expect(await screen.findByText('Se acabaron las consultas de DNI y RUC del mes: escribe el nombre a mano.')).toBeTruthy()
    expect((screen.getByLabelText('Nombre completo') as HTMLInputElement).value).toBe('')
  })

  it('el proveedor se completa por RUC con la razón social y la dirección de SUNAT', async () => {
    const usuario = userEvent.setup()
    apiFalsa({
      ...usuarioConSesion({ roles: ['Gerencia/Admin'] }),
      'GET /consulta-documento/:numero': () => ({
        tipoDocumento: 'RUC',
        tipoDocumentoId: 1,
        numeroDocumento: '20383929050',
        nombreCompleto: 'YAMAHA MOTOR DEL PERU S.A.',
        razonSocial: 'YAMAHA MOTOR DEL PERU S.A.',
        direccion: 'AV. REPUBLICA DE PANAMA NRO 3545, SAN ISIDRO, LIMA, LIMA',
        estado: 'ACTIVO',
        condicion: 'HABIDO',
      }),
    })
    montar(<ModalProveedor abierto onCerrar={vi.fn()} />)

    await usuario.type(await screen.findByLabelText('Número de documento'), '20383929050')

    await vi.waitFor(() =>
      expect((screen.getByLabelText('Razón social o nombre') as HTMLInputElement).value).toBe('YAMAHA MOTOR DEL PERU S.A.'),
    )
    expect((screen.getByLabelText('Dirección') as HTMLInputElement).value).toBe(
      'AV. REPUBLICA DE PANAMA NRO 3545, SAN ISIDRO, LIMA, LIMA',
    )
    expect(screen.getByText('Datos de SUNAT (ACTIVO · HABIDO). Revísalos antes de guardar.')).toBeTruthy()
  })
})
