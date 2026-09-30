import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { avisoSegun } from './avisos'
import {
  TIPO_MOVIMIENTO_CAJA,
  type AperturaCajaRequest,
  type CajaChicaDetalleResponse,
  type CajaChicaResponse,
  type CierreCajaRequest,
  type EstadoCajaActualResponse,
  type MovimientoCajaResponse,
  type RegistrarMovimientoCajaRequest,
} from './tipos'

export const clavesCaja = {
  todas: ['caja-chica'] as const,
  actual: ['caja-chica', 'actual'] as const,
  historial: ['caja-chica', 'historial'] as const,
  una: (id: string) => ['caja-chica', id] as const,
}

/** La caja abierta con sus movimientos, o que no hay ninguna abierta. */
export function useCajaActual(habilitado = true) {
  return useQuery({
    queryKey: clavesCaja.actual,
    queryFn: () => solicitar<EstadoCajaActualResponse>('/caja-chica/actual'),
    enabled: habilitado,
  })
}

export function useHistorialCajas(habilitado = true) {
  return useQuery({
    queryKey: clavesCaja.historial,
    queryFn: () => solicitar<CajaChicaResponse[]>('/caja-chica/historial'),
    enabled: habilitado,
  })
}

export function useCaja(id: string | null) {
  return useQuery({
    queryKey: clavesCaja.una(id ?? ''),
    queryFn: () => solicitar<CajaChicaDetalleResponse>(`/caja-chica/${id}`),
    enabled: Boolean(id),
  })
}

function useMutacionCaja<V, R>(mutationFn: (variables: V) => Promise<R>, exito: Parameters<typeof useMutation>[0]['meta']) {
  const consultas = useQueryClient()
  return useMutation({
    meta: exito,
    mutationFn,
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesCaja.todas })
    },
  })
}

export const useAbrirCaja = () =>
  useMutacionCaja(
    (datos: AperturaCajaRequest) =>
      solicitar<CajaChicaDetalleResponse>('/caja-chica/apertura', { metodo: 'POST', cuerpo: datos }),
    { exito: 'Caja abierta' },
  )

export const useCerrarCaja = () =>
  useMutacionCaja(
    (datos: CierreCajaRequest) =>
      solicitar<CajaChicaDetalleResponse>('/caja-chica/cierre', { metodo: 'POST', cuerpo: datos }),
    { exito: 'Caja cerrada' },
  )

/** Cada tipo tiene su ruta y su permiso: caja.registrar_ingreso o caja.registrar_egreso. */
export const useRegistrarMovimientoCaja = () =>
  useMutacionCaja(
    (datos: RegistrarMovimientoCajaRequest) =>
      solicitar<MovimientoCajaResponse>(
        datos.tipo === TIPO_MOVIMIENTO_CAJA.ingreso ? '/caja-chica/ingresos' : '/caja-chica/egresos',
        { metodo: 'POST', cuerpo: datos },
      ),
    {
      exito: avisoSegun<RegistrarMovimientoCajaRequest>((datos) =>
        datos.tipo === TIPO_MOVIMIENTO_CAJA.ingreso ? 'Ingreso registrado' : 'Egreso registrado',
      ),
    },
  )
