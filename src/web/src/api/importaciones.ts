import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { avisoSegun } from './avisos'
import { clavesClientes } from './clientes'
import { ErrorApi, solicitar, solicitarFormData } from './http'
import { clavesInventario } from './inventario'
import type { HistorialImportacionItem, ResultadoImportacionExcel } from './tipos'

export const RUTA_PLANTILLA_IMPORTACION = '/importaciones/plantilla'
export const NOMBRE_PLANTILLA_IMPORTACION = 'plantilla-importacion-team-benavides.xlsx'

/** El mismo límite que pone la API. */
export const TAMANO_MAXIMO_IMPORTACION = 10 * 1024 * 1024

export const clavesImportaciones = {
  historial: ['importaciones', 'historial'] as const,
}

/** Las 50 importaciones más recientes. Solo con `configuracion.editar`, como toda la pantalla. */
export function useHistorialImportaciones() {
  return useQuery({
    queryKey: clavesImportaciones.historial,
    queryFn: () => solicitar<HistorialImportacionItem[]>('/importaciones/historial?limite=50'),
  })
}

/** Lo que se revisa antes de subir el archivo; la API lo vuelve a revisar. */
export function problemaDelArchivo(archivo: Pick<File, 'name' | 'size'>): string | null {
  if (!archivo.name.toLowerCase().endsWith('.xlsx')) return 'Solo se aceptan archivos de Excel (.xlsx).'
  if (archivo.size === 0) return 'El archivo está vacío.'
  if (archivo.size > TAMANO_MAXIMO_IMPORTACION) return 'El archivo pesa más de 10 MB.'
  return null
}

function esResultado(datos: unknown): datos is ResultadoImportacionExcel {
  if (!datos || typeof datos !== 'object') return false
  const resultado = datos as Partial<ResultadoImportacionExcel>
  return Array.isArray(resultado.errores) && typeof resultado.clientes === 'object' && typeof resultado.catalogo === 'object'
}

/**
 * Un archivo con errores responde 400 con el mismo resultado que uno bueno, y
 * ese resultado trae los errores por fila: se devuelve en vez de lanzarlo. Solo
 * se lanza lo que no es del archivo, como la sesión, los permisos o la API caída.
 */
export async function enviarImportacion(
  ruta: '/importaciones/validar' | '/importaciones/ejecutar',
  archivo: File,
  categoriaId?: string | null,
): Promise<ResultadoImportacionExcel> {
  const formData = new FormData()
  formData.append('archivo', archivo)
  if (categoriaId) formData.append('categoriaId', categoriaId)

  try {
    return await solicitarFormData<ResultadoImportacionExcel>(ruta, formData)
  } catch (error) {
    if (error instanceof ErrorApi && error.estado === 400 && esResultado(error.cuerpo)) {
      return error.cuerpo
    }
    throw error
  }
}

const enPlural = (cantidad: number, singular: string, plural: string) =>
  `${cantidad} ${cantidad === 1 ? singular : plural}`

/** «120 clientes y 1 producto», o vacío si no hay ninguno. */
export function describirCantidades(clientes: number, productos: number): string {
  return [
    clientes > 0 && enPlural(clientes, 'cliente', 'clientes'),
    productos > 0 && enPlural(productos, 'producto', 'productos'),
  ]
    .filter(Boolean)
    .join(' y ')
}

/** «Se importaron 120 clientes y 1 producto.» */
export function textoImportados(resultado: Pick<ResultadoImportacionExcel, 'clientes' | 'catalogo'>): string {
  const cantidades = describirCantidades(resultado.clientes.registrosCreados, resultado.catalogo.registrosCreados)
  return cantidades ? `Se importaron ${cantidades}.` : 'No había registros para importar.'
}

export type ParametrosImportacion = {
  archivo: File
  categoriaId?: string | null
}

/** Revisa el archivo sin guardar nada. Los errores se muestran en la pantalla, no en un aviso. */
export function useValidarImportacion() {
  return useMutation({
    mutationFn: ({ archivo, categoriaId }: ParametrosImportacion) =>
      enviarImportacion('/importaciones/validar', archivo, categoriaId),
  })
}

/** Importa todo o nada: con un error en cualquier fila no se crea ningún registro. */
export function useEjecutarImportacion() {
  const consultas = useQueryClient()

  return useMutation({
    meta: {
      exito: avisoSegun<ParametrosImportacion, ResultadoImportacionExcel>((_, resultado) =>
        resultado.exitoso ? textoImportados(resultado) : null,
      ),
    },
    mutationFn: ({ archivo, categoriaId }: ParametrosImportacion) =>
      enviarImportacion('/importaciones/ejecutar', archivo, categoriaId),
    onSuccess: async (resultado) => {
      // Un intento rechazado también queda en el historial.
      const pendientes = [consultas.invalidateQueries({ queryKey: clavesImportaciones.historial })]
      if (resultado.exitoso) {
        pendientes.push(
          consultas.invalidateQueries({ queryKey: clavesClientes.todos }),
          consultas.invalidateQueries({ queryKey: clavesInventario.productos }),
          consultas.invalidateQueries({ queryKey: clavesInventario.categorias }),
        )
      }
      await Promise.all(pendientes)
    },
  })
}
