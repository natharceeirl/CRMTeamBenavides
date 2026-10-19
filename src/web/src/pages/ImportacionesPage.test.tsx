// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { ResultadoImportacionExcel, ResumenHojaImportacion } from '../api/tipos'
import { apiFalsa, montar } from '../test/pantalla'
import { ImportacionesPage } from './ImportacionesPage'

const TIPO_XLSX = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

const hoja = (leidas: number, validas: number, { omitidos = 0, conError = 0 } = {}): ResumenHojaImportacion => ({
  totalFilasLeidas: leidas,
  filasValidas: validas,
  filasConError: conError,
  registrosCreados: 0,
  registrosOmitidos: omitidos,
})

const validacion = (cambios: Partial<ResultadoImportacionExcel> = {}): ResultadoImportacionExcel => ({
  exitoso: true,
  soloValidacion: true,
  nombreArchivo: 'clientes.xlsx',
  fechaProceso: '2026-10-19T15:00:00Z',
  categoriaId: null,
  clientes: hoja(3, 2, { omitidos: 1 }),
  catalogo: hoja(0, 0),
  unidades: hoja(1, 1),
  repuestos: hoja(0, 0),
  errores: [],
  modoDuplicados: 'OmitirExistentes',
  ...cambios,
})

const pantallaVacia = { 'GET /categorias-producto': [], 'GET /importaciones/historial': [] }

async function elegirArchivo(usuario: ReturnType<typeof userEvent.setup>) {
  const entrada = document.querySelector('input[type="file"]') as HTMLInputElement
  await usuario.upload(entrada, new File(['PK'], 'clientes.xlsx', { type: TIPO_XLSX }))
  await screen.findByText('clientes.xlsx')
}

const formularioEnviado = (llamadas: { metodo: string; ruta: string; cuerpo: unknown }[]) => {
  const pedido = llamadas.find((llamada) => llamada.metodo === 'POST' && llamada.ruta === '/importaciones/validar')
  return pedido?.cuerpo as FormData
}

describe('importar datos', () => {
  it('con «saltar lo ya registrado», dice qué se va a crear y deja importar', async () => {
    const usuario = userEvent.setup()
    const { llamadas } = apiFalsa({ ...pantallaVacia, 'POST /importaciones/validar': validacion() })
    montar(<ImportacionesPage />)

    await elegirArchivo(usuario)
    await usuario.click(screen.getByRole('radio', { name: 'Saltarlo e importar solo lo nuevo' }))
    await usuario.click(screen.getByRole('button', { name: 'Validar' }))

    expect(
      await screen.findByText('El archivo está listo: se crearán 2 clientes y 1 unidad. 1 ya estaba registrado y se omite.'),
    ).toBeTruthy()
    expect(screen.getByRole('columnheader', { name: 'Ya registradas' })).toBeTruthy()
    const importar = screen.getByRole('button', { name: 'Importar 2 clientes y 1 unidad' }) as HTMLButtonElement
    expect(importar.disabled).toBe(false)

    const formulario = formularioEnviado(llamadas)
    expect(formulario.get('modoDuplicados')).toBe('OmitirExistentes')
    expect((formulario.get('archivo') as File).name).toBe('clientes.xlsx')
  })

  it('un archivo con errores muestra cada fila con su problema y no deja importar', async () => {
    const usuario = userEvent.setup()
    const conErrores = validacion({
      exitoso: false,
      clientes: hoja(3, 2, { conError: 1 }),
      errores: [
        { hoja: 'Clientes', fila: 4, columna: 'NumeroDocumento', valorLeido: '1234', mensaje: 'El DNI debe tener 8 dígitos.' },
      ],
      modoDuplicados: 'ErrorSiExiste',
    })
    const { llamadas } = apiFalsa({
      ...pantallaVacia,
      // La API responde 400 con el mismo resultado: los errores por fila no son un fallo de la pantalla.
      'POST /importaciones/validar': () =>
        new Response(JSON.stringify(conErrores), { status: 400, headers: { 'content-type': 'application/json' } }),
    })
    montar(<ImportacionesPage />)

    await elegirArchivo(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Validar' }))

    expect(
      await screen.findByText('El archivo tiene 1 error. No se importa nada hasta corregirlos en el Excel y volver a validarlo.'),
    ).toBeTruthy()
    expect(screen.getByText('El DNI debe tener 8 dígitos.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Importar' }) as HTMLButtonElement).disabled).toBe(true)
    // Sin elegir nada, lo ya registrado es un error.
    expect(formularioEnviado(llamadas).get('modoDuplicados')).toBe('ErrorSiExiste')
  })

  it('cambiar qué hacer con lo ya registrado obliga a validar de nuevo', async () => {
    const usuario = userEvent.setup()
    apiFalsa({ ...pantallaVacia, 'POST /importaciones/validar': validacion() })
    montar(<ImportacionesPage />)

    await elegirArchivo(usuario)
    await usuario.click(screen.getByRole('radio', { name: 'Saltarlo e importar solo lo nuevo' }))
    await usuario.click(screen.getByRole('button', { name: 'Validar' }))
    await screen.findByText('Resultado de la validación')

    await usuario.click(screen.getByRole('radio', { name: 'Marcarlo como error y no importar nada' }))

    expect(screen.queryByText('Resultado de la validación')).toBeNull()
    expect((screen.getByRole('button', { name: 'Importar' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('no acepta un archivo que no es de Excel', async () => {
    const usuario = userEvent.setup({ applyAccept: false })
    apiFalsa(pantallaVacia)
    montar(<ImportacionesPage />)

    const entrada = document.querySelector('input[type="file"]') as HTMLInputElement
    await usuario.upload(entrada, new File(['a,b'], 'clientes.csv', { type: 'text/csv' }))

    expect(await screen.findByText('Solo se aceptan archivos de Excel (.xlsx).')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Validar' }) as HTMLButtonElement).disabled).toBe(true)
  })
})
