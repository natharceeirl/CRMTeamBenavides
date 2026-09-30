import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type {
  ActualizarConfiguracionEmpresaRequest,
  ConfiguracionEmpresaResponse,
  HistorialTipoCambioResponse,
  RegistrarTipoCambioRequest,
  TipoCambioResponse,
} from './tipos'

export const clavesConfiguracion = {
  empresa: ['configuracion', 'empresa'] as const,
  tipoCambio: ['configuracion', 'tipo-cambio'] as const,
  historialTipoCambio: ['configuracion', 'tipo-cambio', 'historial'] as const,
}

export function useConfiguracionEmpresa() {
  return useQuery({
    queryKey: clavesConfiguracion.empresa,
    queryFn: () => solicitar<ConfiguracionEmpresaResponse>('/configuracion/empresa'),
  })
}

export function useActualizarConfiguracionEmpresa() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Configuración guardada' },
    mutationFn: (datos: ActualizarConfiguracionEmpresaRequest) =>
      solicitar<ConfiguracionEmpresaResponse>('/configuracion/empresa', {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesConfiguracion.empresa })
    },
  })
}

/** Lo lee cualquier usuario con sesión: se muestra en toda la web. */
export function useTipoCambio(habilitado = true) {
  return useQuery({
    queryKey: clavesConfiguracion.tipoCambio,
    queryFn: () => solicitar<TipoCambioResponse>('/configuracion/tipo-cambio'),
    enabled: habilitado,
    staleTime: 5 * 60_000,
  })
}

export function useHistorialTipoCambio(habilitado = true) {
  return useQuery({
    queryKey: clavesConfiguracion.historialTipoCambio,
    queryFn: () => solicitar<HistorialTipoCambioResponse[]>('/configuracion/tipo-cambio/historial'),
    enabled: habilitado,
  })
}

/** Solo con `configuracion.editar`. Queda en el historial con el usuario. */
export function useRegistrarTipoCambio() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: 'Tipo de cambio registrado' },
    mutationFn: (datos: RegistrarTipoCambioRequest) =>
      solicitar<TipoCambioResponse>('/configuracion/tipo-cambio', { metodo: 'PUT', cuerpo: datos }),
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesConfiguracion.tipoCambio }),
        consultas.invalidateQueries({ queryKey: clavesConfiguracion.empresa }),
      ])
    },
  })
}
