import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { Dayjs } from 'dayjs'
import { solicitar } from './http'
import { avisoSegun, type AvisoExito } from './avisos'
import { clavesPortal } from './portal'
import type {
  ActualizarOrdenRequest,
  ActualizarDetalleRequest,
  AgregarDetalleRequest,
  AperturaOrdenRequest,
  AprobacionGerenciaRequest,
  AsignarTecnicoRequest,
  CambiarEstadoRequest,
  DetalleServicioResponse,
  DiagnosticoRequest,
  OrdenServicioDetalleResponse,
  OrdenServicioResponse,
  RespuestaPresupuestoRequest,
  FormatoAtencionResponse,
} from './tipos'

/**
 * Estados del backend (enum EstadoOrdenServicio). Se mandan como número porque
 * la API no tiene conversor de enums a texto; en las respuestas vienen los dos:
 * `estado` con el nombre y `estadoId` con el número.
 *
 * El cliente decidió mantener estos siete estados (resumen de cambios del 25/09).
 */
export const ESTADO = {
  abierta: 0,
  diagnostico: 1,
  aprobada: 2,
  enProceso: 3,
  lista: 4,
  entregada: 5,
  cancelada: 6,
} as const

export const nombresEstado: Record<number, string> = {
  0: 'Abierta',
  1: 'Diagnóstico',
  2: 'Aprobada',
  3: 'En proceso',
  4: 'Lista',
  5: 'Entregada',
  6: 'Cancelada',
}

/** Misma matriz que valida el backend en OrdenServicioService.CambiarEstadoAsync. */
export const transicionesValidas: Record<number, number[]> = {
  0: [1, 6],
  1: [2, 6],
  2: [3, 6],
  3: [4, 6],
  4: [5, 3, 6],
  5: [],
  6: [],
}

/**
 * El técnico no hace la aprobación final, la entrega ni la anulación: el backend
 * lo rechaza en OrdenServicioService.CambiarEstadoAsync.
 */
export const estadosVedadosAlTecnico: readonly number[] = [ESTADO.aprobada, ESTADO.entregada, ESTADO.cancelada]

export const esEstadoTerminal = (estadoId: number) =>
  estadoId === ESTADO.entregada || estadoId === ESTADO.cancelada

/** La orden se puede editar (diagnóstico y detalles) salvo en Lista o terminal. */
export const permiteEditarDetalles = (estadoId: number) =>
  !esEstadoTerminal(estadoId) && estadoId !== ESTADO.lista

/**
 * Por qué la orden no admite cambios en sus ítems, o null si los admite. Con la
 * orden liquidada tampoco: la venta tiene que coincidir con la orden.
 */
export function motivoItemsBloqueados(orden: { estadoId: number; ventaId?: string | null }): string | null {
  if (!permiteEditarDetalles(orden.estadoId)) {
    return `La orden está en «${nombresEstado[orden.estadoId]}» y ya no admite cambios en los ítems.`
  }
  if (orden.ventaId) {
    return 'La orden ya está liquidada: anula su venta para cambiar los trabajos o repuestos.'
  }
  return null
}

/** Enums TipoAtencion, ModalidadAtencion y TipoFalla del backend, en el mismo orden. */
export const TIPOS_ATENCION = [
  { value: 0, label: 'Mantenimiento preventivo' },
  { value: 1, label: 'Mantenimiento correctivo' },
  { value: 2, label: 'Reclamo de garantía' },
  { value: 3, label: 'Gratuito' },
]

export const MODALIDADES_ATENCION = [
  { value: 0, label: 'En taller' },
  { value: 1, label: 'En sitio' },
]

export const TIPOS_FALLA = [
  { value: 0, label: 'Menor' },
  { value: 1, label: 'Mayor' },
]

export const etiquetaDe = (opciones: { value: number; label: string }[], valor: number | null | undefined) =>
  opciones.find((opcion) => opcion.value === valor)?.label ?? '—'

/** Enum EstadoPresupuestoCliente del backend. */
export const PRESUPUESTO = { pendiente: 0, aprobado: 1, rechazado: 2 } as const

export const nombresPresupuesto: Record<number, string> = {
  [PRESUPUESTO.pendiente]: 'Pendiente',
  [PRESUPUESTO.aprobado]: 'Aprobado',
  [PRESUPUESTO.rechazado]: 'Rechazado',
}

/**
 * Qué respuestas al presupuesto se pueden dar todavía, con la regla del backend:
 * con la orden en Diagnóstico y algo que aprobar (total mayor a cero), y nunca
 * después de aprobado. Si se rechazó, el cliente aún puede aprobarlo.
 */
export function respuestasPresupuesto(estadoOrden: number, presupuesto: number, total: number): number[] {
  const enEtapa = estadoOrden === ESTADO.diagnostico && total > 0
  if (!enEtapa || presupuesto === PRESUPUESTO.aprobado) return []
  return presupuesto === PRESUPUESTO.rechazado
    ? [PRESUPUESTO.aprobado]
    : [PRESUPUESTO.aprobado, PRESUPUESTO.rechazado]
}

/**
 * Cómo se muestra la respuesta al presupuesto. «Pendiente» solo mientras de verdad
 * se espera; una orden que pasó de etapa sin respuesta queda «Sin respuesta».
 */
export function etiquetaPresupuesto(
  estadoOrden: number,
  presupuesto: number,
  total: number,
): { texto: string; tono: 'alerta' | 'hecho' | 'suave' | 'neutro' | 'apagado' } {
  if (presupuesto === PRESUPUESTO.aprobado) return { texto: 'Aprobado', tono: 'hecho' }
  if (presupuesto === PRESUPUESTO.rechazado) return { texto: 'Rechazado', tono: 'suave' }
  if (respuestasPresupuesto(estadoOrden, presupuesto, total).length > 0) return { texto: 'Pendiente', tono: 'alerta' }
  // Antes del diagnóstico, o sin ítems todavía, el presupuesto se está armando.
  if (estadoOrden === ESTADO.abierta || estadoOrden === ESTADO.diagnostico) {
    return { texto: 'En preparación', tono: 'neutro' }
  }
  return { texto: 'Sin respuesta', tono: 'apagado' }
}

/** El presupuesto ya está armado y espera al cliente: lo que se le pide responder. */
export const esperaRespuestaDelCliente = (orden: {
  estadoId: number
  estadoPresupuestoClienteId?: number
  total?: number
}) =>
  (orden.estadoPresupuestoClienteId ?? PRESUPUESTO.pendiente) === PRESUPUESTO.pendiente &&
  respuestasPresupuesto(orden.estadoId, PRESUPUESTO.pendiente, orden.total ?? 0).length > 0

/** Los estados con los nombres que entiende el cliente, como en la app. */
export const nombresEstadoCliente: Record<number, string> = {
  [ESTADO.abierta]: 'Recibida',
  [ESTADO.diagnostico]: 'En diagnóstico',
  [ESTADO.aprobada]: 'Presupuesto aprobado',
  [ESTADO.enProceso]: 'En reparación',
  [ESTADO.lista]: 'Lista para recoger',
  [ESTADO.entregada]: 'Entregada',
  [ESTADO.cancelada]: 'Cancelada',
}

/** Enum EstadoAprobacionGerencia del backend. */
export const GERENCIA = { noAplica: 0, pendiente: 1, aprobado: 2, rechazado: 3 } as const

export const nombresGerencia: Record<number, string> = {
  [GERENCIA.noAplica]: 'No aplica',
  [GERENCIA.pendiente]: 'Pendiente',
  [GERENCIA.aprobado]: 'Aprobada',
  [GERENCIA.rechazado]: 'Rechazada',
}

/**
 * Por qué la orden no puede pasar a «Aprobada», o null si puede. Son las mismas
 * reglas de OrdenServicioService.CambiarEstadoAsync: la interfaz solo las
 * anticipa para no mostrar un botón que la API rechazaría.
 */
export function motivoBloqueoAprobacion(
  orden: Pick<OrdenServicioResponse, 'estadoPresupuestoClienteId' | 'estadoAprobacionGerenciaId'>,
): string | null {
  if (orden.estadoPresupuestoClienteId === PRESUPUESTO.rechazado) {
    return 'El cliente rechazó el presupuesto.'
  }
  // «Aprobada» es «presupuesto aprobado»: sin la respuesta del cliente no se aprueba.
  if (orden.estadoPresupuestoClienteId !== PRESUPUESTO.aprobado) {
    return 'Falta que el cliente apruebe el presupuesto.'
  }
  if (orden.estadoAprobacionGerenciaId === GERENCIA.pendiente) {
    return 'Falta la aprobación de Gerencia.'
  }
  if (orden.estadoAprobacionGerenciaId === GERENCIA.rechazado) {
    return 'Gerencia rechazó la orden.'
  }
  return null
}

/**
 * Fecha en que la unidad entró al taller. Las órdenes anteriores al formato de
 * recepción traen `fechaIngreso` en 0001-01-01; para esas vale la apertura.
 */
export function fechaIngresoOrden(orden: { fechaApertura: string; fechaIngreso?: string | null }): string {
  if (orden.fechaIngreso && new Date(orden.fechaIngreso).getUTCFullYear() > 1900) {
    return orden.fechaIngreso
  }
  return orden.fechaApertura
}

/**
 * La entrega estimada no puede quedar antes del ingreso de la unidad. Se compara
 * por minuto porque el selector no maneja segundos.
 */
export function motivoEntregaInvalida(entrega: Dayjs | null | undefined, ingreso: Dayjs): string | null {
  if (!entrega || !entrega.isBefore(ingreso, 'minute')) return null
  return `La entrega estimada no puede ser anterior al ingreso (${ingreso.format('DD/MM/YYYY HH:mm')}).`
}

/** Filtros que acepta GET /api/ordenes-servicio. La búsqueda y las fechas las resuelve la API. */
export type FiltrosOrdenes = {
  estado?: number
  vehiculoId?: string
  clienteId?: string
  tecnicoId?: string
  busqueda?: string
  fechaDesde?: string
  fechaHasta?: string
}

export const clavesOrdenes = {
  todas: ['ordenes'] as const,
  lista: (filtros: FiltrosOrdenes) => ['ordenes', 'lista', filtros] as const,
  una: (id: string) => ['ordenes', id] as const,
}

/** `habilitado` en falso evita pedir órdenes a quien no puede verlas y recibiría 403. */
export function useOrdenes(filtros: FiltrosOrdenes = {}, habilitado = true) {
  return useQuery({
    queryKey: clavesOrdenes.lista(filtros),
    queryFn: () => {
      const parametros = new URLSearchParams()
      if (filtros.estado !== undefined) parametros.set('estado', String(filtros.estado))
      if (filtros.vehiculoId) parametros.set('vehiculoId', filtros.vehiculoId)
      if (filtros.clienteId) parametros.set('clienteId', filtros.clienteId)
      if (filtros.tecnicoId) parametros.set('tecnicoId', filtros.tecnicoId)
      if (filtros.busqueda?.trim()) parametros.set('busqueda', filtros.busqueda.trim())
      if (filtros.fechaDesde) parametros.set('fechaDesde', filtros.fechaDesde)
      if (filtros.fechaHasta) parametros.set('fechaHasta', filtros.fechaHasta)
      const consulta = parametros.toString()
      return solicitar<OrdenServicioResponse[]>(
        `/ordenes-servicio${consulta ? `?${consulta}` : ''}`,
      )
    },
    enabled: habilitado,
    placeholderData: (anterior) => anterior,
  })
}

export function useOrden(id: string | undefined) {
  return useQuery({
    queryKey: clavesOrdenes.una(id ?? ''),
    queryFn: () => solicitar<OrdenServicioDetalleResponse>(`/ordenes-servicio/${id}`),
    enabled: Boolean(id),
  })
}

export function useAbrirOrden() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: avisoSegun<AperturaOrdenRequest, OrdenServicioResponse>((_, orden) =>
        orden.numeroOrden ? `Orden ${orden.numeroOrden} abierta` : 'Orden abierta',
      ) },
    mutationFn: (datos: AperturaOrdenRequest) =>
      solicitar<OrdenServicioResponse>('/ordenes-servicio', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}

/** Mutación sobre una orden que, al terminar, refresca la lista y el detalle. */
function useAccionOrden<T>(ruta: string, exito: AvisoExito) {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito },
    mutationFn: ({ id, datos }: { id: string; datos: T }) =>
      solicitar<OrdenServicioResponse>(`/ordenes-servicio/${id}/${ruta}`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    // El resumen del portal (por responder, saldo) depende de la orden.
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesOrdenes.todas }),
        consultas.invalidateQueries({ queryKey: clavesPortal.todas }),
      ])
    },
  })
}

/** El personal registra la respuesta del cliente al presupuesto; queda en el historial. */
export const useResponderPresupuesto = () =>
  useAccionOrden<RespuestaPresupuestoRequest>(
    'aprobacion-cliente',
    avisoSegun<{ datos: RespuestaPresupuestoRequest }>(({ datos }) =>
      datos.estado === PRESUPUESTO.rechazado ? 'Rechazo del presupuesto registrado' : 'Aprobación del presupuesto registrada',
    ),
  )

/** Solo con `ordenes.aprobar_gerencia`. */
export const useAprobacionGerencia = () =>
  useAccionOrden<AprobacionGerenciaRequest>(
    'aprobacion-gerencia',
    avisoSegun<{ datos: AprobacionGerenciaRequest }>(({ datos }) => {
      if (datos.estado === GERENCIA.aprobado) return 'Aprobado por Gerencia'
      if (datos.estado === GERENCIA.rechazado) return 'Rechazado por Gerencia'
      return 'Aprobación de Gerencia requerida'
    }),
  )

export const useAsignarTecnico = () => useAccionOrden<AsignarTecnicoRequest>('asignar-tecnico', 'Técnico asignado')

/** Quien edita órdenes sin poder aprobarlas (Recepción) pide la aprobación de Gerencia, con su motivo. */
export const useSolicitarAprobacionGerencia = () =>
  useAccionOrden<{ observaciones: string }>('solicitar-aprobacion-gerencia', 'Aprobación solicitada a Gerencia')

/** PUT /api/ordenes-servicio/{id}: datos de recepción y seguimiento de la orden. */
export function useActualizarOrden() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Datos de la orden guardados' },
    mutationFn: ({ id, datos }: { id: string; datos: ActualizarOrdenRequest }) =>
      solicitar<OrdenServicioResponse>(`/ordenes-servicio/${id}`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}

export function useRegistrarDiagnostico() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Diagnóstico guardado' },
    mutationFn: ({ id, datos }: { id: string; datos: DiagnosticoRequest }) =>
      solicitar<OrdenServicioResponse>(`/ordenes-servicio/${id}/diagnostico`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}

/**
 * Un precio fuera de lista abre una solicitud a Gerencia y volver al de lista la
 * cierra: los ítems refrescan la orden y la bandeja. La clave se escribe aquí
 * porque api/aprobaciones importa este archivo.
 */
function refrescarOrdenYAprobaciones(consultas: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    consultas.invalidateQueries({ queryKey: clavesOrdenes.todas }),
    consultas.invalidateQueries({ queryKey: ['aprobaciones'] }),
  ])
}

export function useAgregarDetalle() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Ítem agregado a la orden' },
    mutationFn: ({ id, datos }: { id: string; datos: AgregarDetalleRequest }) =>
      solicitar<DetalleServicioResponse>(`/ordenes-servicio/${id}/detalles`, {
        metodo: 'POST',
        cuerpo: datos,
      }),
    onSuccess: () => refrescarOrdenYAprobaciones(consultas),
  })
}

export function useActualizarDetalle() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Ítem actualizado' },
    mutationFn: ({ id, detalleId, datos }: { id: string; detalleId: string; datos: ActualizarDetalleRequest }) =>
      solicitar<DetalleServicioResponse>(`/ordenes-servicio/${id}/detalles/${detalleId}`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: () => refrescarOrdenYAprobaciones(consultas),
  })
}

export function useEliminarDetalle() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Ítem quitado de la orden' },
    mutationFn: ({ id, detalleId }: { id: string; detalleId: string }) =>
      solicitar<{ message: string }>(`/ordenes-servicio/${id}/detalles/${detalleId}`, {
        metodo: 'DELETE',
      }),
    onSuccess: () => refrescarOrdenYAprobaciones(consultas),
  })
}

export function useCambiarEstado() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: avisoSegun<{ datos: CambiarEstadoRequest }>(({ datos }) =>
        datos.nuevoEstado === ESTADO.cancelada ? 'Orden anulada' : `Orden en «${nombresEstado[datos.nuevoEstado]}»`,
      ) },
    mutationFn: ({ id, datos }: { id: string; datos: CambiarEstadoRequest }) =>
      solicitar<OrdenServicioResponse>(`/ordenes-servicio/${id}/estado`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}

export function useFormatoAtencionOrden(id?: string | null) {
  return useQuery({
    queryKey: ['formato-atencion', id],
    queryFn: () => solicitar<FormatoAtencionResponse>(`/ordenes-servicio/${id}/formato-atencion`),
    enabled: Boolean(id),
  })
}

