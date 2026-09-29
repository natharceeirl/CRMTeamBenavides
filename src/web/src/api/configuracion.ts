import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type {
  ActualizarConfiguracionEmpresaRequest,
  ConfiguracionEmpresaResponse,
} from './tipos'

export const clavesConfiguracion = {
  empresa: ['configuracion', 'empresa'] as const,
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
