import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErrorApi } from './http'
import {
  cantidadesPorImportar,
  describirCantidades,
  enviarImportacion,
  hojasDelResultado,
  problemaDelArchivo,
  repuestosDelHistorial,
  textoImportados,
  textoPorImportar,
  totalOmitidos,
} from './importaciones'
import { MODO_DUPLICADOS, type HistorialImportacionItem, type ResultadoImportacionExcel } from './tipos'

const resumen = (valores: Partial<ResultadoImportacionExcel['clientes']> = {}) => ({
  totalFilasLeidas: 2,
  filasValidas: 2,
  filasConError: 0,
  registrosCreados: 0,
  registrosOmitidos: 0,
  ...valores,
})

const resultadoBase = (valores: Partial<ResultadoImportacionExcel> = {}): ResultadoImportacionExcel => ({
  exitoso: true,
  soloValidacion: true,
  nombreArchivo: 'migracion.xlsx',
  fechaProceso: '2026-10-18T15:00:00Z',
  categoriaId: null,
  clientes: resumen(),
  catalogo: resumen(),
  unidades: resumen(),
  repuestos: resumen(),
  errores: [],
  modoDuplicados: MODO_DUPLICADOS.error,
  ...valores,
})

const resultadoConErrores = resultadoBase({
  exitoso: false,
  soloValidacion: false,
  clientes: resumen({ filasValidas: 1, filasConError: 1 }),
  errores: [
    {
      hoja: 'Clientes',
      fila: 3,
      columna: 'NumeroDocumento',
      valorLeido: '4587123',
      mensaje: 'El DNI debe contener exactamente 8 dígitos numéricos.',
    },
  ],
})

const responder = (cuerpo: unknown, estado: number) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'content-type': 'application/json' } })

const excel = () => new File(['contenido'], 'migracion.xlsx')

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('importación de Excel', () => {
  it('antes de subir revisa la extensión y el tamaño', () => {
    expect(problemaDelArchivo({ name: 'clientes.XLSX', size: 2048 })).toBeNull()
    expect(problemaDelArchivo({ name: 'clientes.xls', size: 2048 })).toBe('Solo se aceptan archivos de Excel (.xlsx).')
    expect(problemaDelArchivo({ name: 'clientes.xlsx', size: 0 })).toBe('El archivo está vacío.')
    expect(problemaDelArchivo({ name: 'clientes.xlsx', size: 11 * 1024 * 1024 })).toBe('El archivo pesa más de 10 MB.')
  })

  it('cuenta clientes, unidades y repuestos en singular y plural', () => {
    expect(describirCantidades({ clientes: 120, unidades: 80, repuestos: 1 })).toBe('120 clientes, 80 unidades y 1 repuesto')
    expect(describirCantidades({ clientes: 1, unidades: 0, repuestos: 3 })).toBe('1 cliente y 3 repuestos')
    expect(describirCantidades({ clientes: 0, unidades: 1, repuestos: 0 })).toBe('1 unidad')
    expect(describirCantidades({ clientes: 0, unidades: 0, repuestos: 0 })).toBe('')
  })

  it('muestra una fila por hoja y toma los repuestos de catalogo si no viene la hoja Repuestos', () => {
    const legado = resultadoBase({ unidades: null, repuestos: undefined, catalogo: resumen({ filasValidas: 5 }) })

    expect(hojasDelResultado(legado).map((fila) => fila.hoja)).toEqual(['Clientes', 'Unidades', 'Repuestos'])
    expect(cantidadesPorImportar(legado)).toEqual({ clientes: 2, unidades: 0, repuestos: 5 })
  })

  it('al validar avisa lo que se creará y lo que se omite por estar registrado', () => {
    const conOmitidos = resultadoBase({
      modoDuplicados: MODO_DUPLICADOS.omitir,
      clientes: resumen({ filasValidas: 3, registrosOmitidos: 2 }),
      unidades: resumen({ filasValidas: 0, registrosOmitidos: 1 }),
      repuestos: resumen({ filasValidas: 0 }),
    })

    expect(totalOmitidos(conOmitidos)).toBe(3)
    expect(textoPorImportar(conOmitidos)).toBe('El archivo está listo: se crearán 3 clientes. 3 ya estaban registrados y se omiten.')

    const todoRegistrado = resultadoBase({
      clientes: resumen({ filasValidas: 0, registrosOmitidos: 1 }),
      unidades: resumen({ filasValidas: 0 }),
      repuestos: resumen({ filasValidas: 0 }),
    })
    expect(textoPorImportar(todoRegistrado)).toBe(
      'El archivo está listo, pero no hay registros nuevos. 1 ya estaba registrado y se omite.',
    )
  })

  it('al importar cuenta lo creado', () => {
    const importado = resultadoBase({
      soloValidacion: false,
      clientes: resumen({ registrosCreados: 1 }),
      unidades: resumen({ registrosCreados: 2 }),
      repuestos: resumen({ registrosCreados: 0 }),
    })
    expect(textoImportados(importado)).toBe('Se importaron 1 cliente y 2 unidades.')

    const vacio = resultadoBase({ soloValidacion: false, clientes: resumen(), unidades: resumen(), repuestos: resumen() })
    expect(textoImportados(vacio)).toBe('No había registros nuevos para importar.')
  })

  it('en el historial anterior al 15/10 los repuestos vienen en catalogoProcesados', () => {
    const base = { catalogoProcesados: 4, repuestosProcesados: 0 } as HistorialImportacionItem
    expect(repuestosDelHistorial(base)).toBe(4)
    expect(repuestosDelHistorial({ ...base, catalogoProcesados: 6, repuestosProcesados: 6 })).toBe(6)
  })

  it('manda el archivo, la categoría y la regla para lo ya registrado en el formulario', async () => {
    const llamar = vi.fn().mockResolvedValue(responder(resultadoBase(), 200))
    vi.stubGlobal('fetch', llamar)

    await enviarImportacion('/importaciones/validar', excel(), 'categoria-1', MODO_DUPLICADOS.omitir)

    expect(llamar.mock.calls[0][0]).toBe('/api/importaciones/validar')
    const formulario = (llamar.mock.calls[0][1] as { body: FormData }).body
    expect((formulario.get('archivo') as File).name).toBe('migracion.xlsx')
    expect(formulario.get('categoriaId')).toBe('categoria-1')
    expect(formulario.get('modoDuplicados')).toBe('OmitirExistentes')
  })

  it('sin regla no manda el campo y la API marca error lo ya registrado', async () => {
    const llamar = vi.fn().mockResolvedValue(responder(resultadoBase(), 200))
    vi.stubGlobal('fetch', llamar)

    await enviarImportacion('/importaciones/validar', excel())

    const formulario = (llamar.mock.calls[0][1] as { body: FormData }).body
    expect(formulario.has('modoDuplicados')).toBe(false)
    expect(formulario.has('categoriaId')).toBe(false)
  })

  it('un 400 con el resultado devuelve los errores por fila en vez de lanzarlos', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(responder(resultadoConErrores, 400)))

    await expect(enviarImportacion('/importaciones/ejecutar', excel())).resolves.toEqual(resultadoConErrores)
  })

  it('un error que no es del archivo se sigue lanzando', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(responder({ error: 'Sin permiso' }, 403)))
    await expect(enviarImportacion('/importaciones/ejecutar', excel())).rejects.toBeInstanceOf(ErrorApi)

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(responder({ error: 'Archivo inválido' }, 400)))
    await expect(enviarImportacion('/importaciones/validar', excel())).rejects.toMatchObject({ estado: 400 })
  })
})
