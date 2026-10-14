import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErrorApi } from './http'
import { describirCantidades, enviarImportacion, problemaDelArchivo, textoImportados } from './importaciones'
import type { ResultadoImportacionExcel } from './tipos'

const resumen = (creados = 0) => ({ totalFilasLeidas: 2, filasValidas: 2, filasConError: 0, registrosCreados: creados })

const resultadoConErrores: ResultadoImportacionExcel = {
  exitoso: false,
  soloValidacion: false,
  nombreArchivo: 'clientes.xlsx',
  fechaProceso: '2026-10-14T15:00:00Z',
  categoriaId: null,
  clientes: { totalFilasLeidas: 2, filasValidas: 1, filasConError: 1, registrosCreados: 0 },
  catalogo: resumen(),
  errores: [
    {
      hoja: 'Clientes',
      fila: 3,
      columna: 'NumeroDocumento',
      valorLeido: '4587123',
      mensaje: 'El DNI debe contener exactamente 8 dígitos numéricos.',
    },
  ],
}

const responder = (cuerpo: unknown, estado: number) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'content-type': 'application/json' } })

const excel = () => new File(['contenido'], 'clientes.xlsx')

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

  it('cuenta lo importado en singular y plural', () => {
    expect(describirCantidades(120, 1)).toBe('120 clientes y 1 producto')
    expect(describirCantidades(0, 3)).toBe('3 productos')
    expect(textoImportados({ clientes: resumen(1), catalogo: resumen(0) })).toBe('Se importaron 1 cliente.')
    expect(textoImportados({ clientes: resumen(0), catalogo: resumen(0) })).toBe('No había registros para importar.')
  })

  it('manda el archivo y la categoría en el formulario', async () => {
    const llamar = vi.fn().mockResolvedValue(responder({ ...resultadoConErrores, exitoso: true, errores: [] }, 200))
    vi.stubGlobal('fetch', llamar)

    await enviarImportacion('/importaciones/validar', excel(), 'categoria-1')

    expect(llamar.mock.calls[0][0]).toBe('/api/importaciones/validar')
    const formulario = (llamar.mock.calls[0][1] as { body: FormData }).body
    expect((formulario.get('archivo') as File).name).toBe('clientes.xlsx')
    expect(formulario.get('categoriaId')).toBe('categoria-1')
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
