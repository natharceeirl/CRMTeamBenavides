import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { avisoSegun } from './avisos'
import { clavesClientes } from './clientes'
import { ErrorApi, solicitar, solicitarFormData } from './http'
import { clavesInventario } from './inventario'
import { clavesVehiculos } from './vehiculos'
import type {
  HistorialImportacionItem,
  ModoDuplicados,
  ResultadoImportacionExcel,
  ResumenHojaImportacion,
} from './tipos'

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
  modoDuplicados?: ModoDuplicados,
): Promise<ResultadoImportacionExcel> {
  const formData = new FormData()
  formData.append('archivo', archivo)
  if (categoriaId) formData.append('categoriaId', categoriaId)
  // Sin el campo, la API marca como error lo que ya está registrado.
  if (modoDuplicados) formData.append('modoDuplicados', modoDuplicados)

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

/** «a», «a y b», «a, b y c». */
const enumerar = (partes: string[]) =>
  partes.length <= 1 ? (partes[0] ?? '') : `${partes.slice(0, -1).join(', ')} y ${partes.at(-1)}`

export type CantidadesImportacion = { clientes: number; unidades: number; repuestos: number }

/** «120 clientes, 80 unidades y 1 repuesto», o vacío si no hay ninguno. */
export function describirCantidades({ clientes, unidades, repuestos }: CantidadesImportacion): string {
  return enumerar(
    [
      clientes > 0 && enPlural(clientes, 'cliente', 'clientes'),
      unidades > 0 && enPlural(unidades, 'unidad', 'unidades'),
      repuestos > 0 && enPlural(repuestos, 'repuesto', 'repuestos'),
    ].filter((parte): parte is string => Boolean(parte)),
  )
}

const RESUMEN_VACIO: ResumenHojaImportacion = {
  totalFilasLeidas: 0,
  filasValidas: 0,
  filasConError: 0,
  registrosCreados: 0,
  registrosOmitidos: 0,
}

/** Cada hoja del resultado; Repuestos incluye la hoja Catalogo de la plantilla anterior. */
export function hojasDelResultado(resultado: ResultadoImportacionExcel) {
  return [
    { hoja: 'Clientes', ...RESUMEN_VACIO, ...resultado.clientes },
    { hoja: 'Unidades', ...RESUMEN_VACIO, ...resultado.unidades },
    { hoja: 'Repuestos', ...RESUMEN_VACIO, ...(resultado.repuestos ?? resultado.catalogo) },
  ]
}

const sumarPorHoja = (
  resultado: ResultadoImportacionExcel,
  campo: 'filasValidas' | 'registrosCreados' | 'registrosOmitidos',
): CantidadesImportacion => {
  const [clientes, unidades, repuestos] = hojasDelResultado(resultado).map((hoja) => hoja[campo])
  return { clientes, unidades, repuestos }
}

/** Lo que se crearía al importar un archivo ya validado. */
export const cantidadesPorImportar = (resultado: ResultadoImportacionExcel) => sumarPorHoja(resultado, 'filasValidas')

/** Cuántas filas se saltan porque ya estaban registradas (solo con `OmitirExistentes`). */
export function totalOmitidos(resultado: ResultadoImportacionExcel): number {
  const { clientes, unidades, repuestos } = sumarPorHoja(resultado, 'registrosOmitidos')
  return clientes + unidades + repuestos
}

/** « 3 ya estaban registrados y se omiten.», o vacío. */
function textoOmitidos(resultado: ResultadoImportacionExcel, tiempo: 'presente' | 'pasado'): string {
  const omitidos = totalOmitidos(resultado)
  if (omitidos === 0) return ''
  if (omitidos === 1) return ` 1 ya estaba registrado y ${tiempo === 'presente' ? 'se omite' : 'se omitió'}.`
  return ` ${omitidos} ya estaban registrados y ${tiempo === 'presente' ? 'se omiten' : 'se omitieron'}.`
}

/** «El archivo está listo: se crearán 120 clientes. 3 ya estaban registrados y se omiten.» */
export function textoPorImportar(resultado: ResultadoImportacionExcel): string {
  const cantidades = describirCantidades(cantidadesPorImportar(resultado))
  const principal = cantidades ? `El archivo está listo: se crearán ${cantidades}.` : 'El archivo está listo, pero no hay registros nuevos.'
  return principal + textoOmitidos(resultado, 'presente')
}

/** «Se importaron 120 clientes y 1 repuesto.» */
export function textoImportados(resultado: ResultadoImportacionExcel): string {
  const cantidades = describirCantidades(sumarPorHoja(resultado, 'registrosCreados'))
  const principal = cantidades ? `Se importaron ${cantidades}.` : 'No había registros nuevos para importar.'
  return principal + textoOmitidos(resultado, 'pasado')
}

/** Los registros anteriores al 15/10 solo traen los repuestos en `catalogoProcesados`. */
export const repuestosDelHistorial = (item: HistorialImportacionItem) =>
  Math.max(item.repuestosProcesados ?? 0, item.catalogoProcesados)

export type ParametrosImportacion = {
  archivo: File
  categoriaId?: string | null
  modoDuplicados: ModoDuplicados
}

/** Revisa el archivo sin guardar nada. Los errores se muestran en la pantalla, no en un aviso. */
export function useValidarImportacion() {
  return useMutation({
    mutationFn: ({ archivo, categoriaId, modoDuplicados }: ParametrosImportacion) =>
      enviarImportacion('/importaciones/validar', archivo, categoriaId, modoDuplicados),
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
    mutationFn: ({ archivo, categoriaId, modoDuplicados }: ParametrosImportacion) =>
      enviarImportacion('/importaciones/ejecutar', archivo, categoriaId, modoDuplicados),
    onSuccess: async (resultado) => {
      // Un intento rechazado también queda en el historial.
      const pendientes = [consultas.invalidateQueries({ queryKey: clavesImportaciones.historial })]
      if (resultado.exitoso) {
        pendientes.push(
          consultas.invalidateQueries({ queryKey: clavesClientes.todos }),
          consultas.invalidateQueries({ queryKey: clavesVehiculos.todos }),
          consultas.invalidateQueries({ queryKey: clavesInventario.productos }),
          // La hoja Repuestos crea las categorías que todavía no existen.
          consultas.invalidateQueries({ queryKey: clavesInventario.categorias }),
        )
      }
      await Promise.all(pendientes)
    },
  })
}
