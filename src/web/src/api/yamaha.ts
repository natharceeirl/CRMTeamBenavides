import { useQuery } from '@tanstack/react-query'
import { solicitar } from './http'
import type { YamahaConsultaMockResponse } from './tipos'

export const CLAVES_YAMAHA = {
  todas: ['yamaha-mock'] as const,
  porCriterio: (criterio: string) => [...CLAVES_YAMAHA.todas, criterio] as const,
}

export function useYamahaMockConsulta(criterio?: string | null) {
  const queryTerm = criterio?.trim() ?? ''

  return useQuery({
    queryKey: CLAVES_YAMAHA.porCriterio(queryTerm),
    queryFn: () =>
      solicitar<YamahaConsultaMockResponse>(`/yamaha/mock/consultar?criterio=${encodeURIComponent(queryTerm)}`),
    enabled: queryTerm.length > 0,
    retry: false,
  })
}
