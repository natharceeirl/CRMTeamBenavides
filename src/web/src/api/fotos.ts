import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar, solicitarFormData } from './http'
import type { FotoOrdenServicioResponse } from './tipos'

export const CLAVES_FOTOS = {
  todas: ['fotos-orden'] as const,
  porOrden: (ordenServicioId: string) => [...CLAVES_FOTOS.todas, ordenServicioId] as const,
}

export function useFotosOrden(ordenServicioId?: string | null) {
  return useQuery({
    queryKey: CLAVES_FOTOS.porOrden(ordenServicioId ?? ''),
    queryFn: () => solicitar<FotoOrdenServicioResponse[]>(`/ordenes-servicio/${ordenServicioId}/fotos`),
    enabled: Boolean(ordenServicioId),
  })
}

export type SubirFotoParametros = {
  ordenServicioId: string
  archivo: File
  etapa: number
  observacion?: string | null
}

export function useSubirFotoOrden() {
  const queryClient = useQueryClient()

  return useMutation({
    meta: { exito: 'Foto agregada' },
    mutationFn: async ({ ordenServicioId, archivo, etapa, observacion }: SubirFotoParametros) => {
      const formData = new FormData()
      formData.append('archivo', archivo)
      formData.append('etapa', etapa.toString())
      if (observacion?.trim()) {
        formData.append('observacion', observacion.trim())
      }

      return await solicitarFormData<FotoOrdenServicioResponse>(
        `/ordenes-servicio/${ordenServicioId}/fotos`,
        formData,
      )
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: CLAVES_FOTOS.porOrden(variables.ordenServicioId) })
    },
  })
}

export type EliminarFotoParametros = {
  ordenServicioId: string
  fotoId: string
}

export function useEliminarFotoOrden() {
  const queryClient = useQueryClient()

  return useMutation({
    meta: { exito: 'Foto quitada' },
    mutationFn: async ({ ordenServicioId, fotoId }: EliminarFotoParametros) => {
      return await solicitar<void>(`/ordenes-servicio/${ordenServicioId}/fotos/${fotoId}`, {
        metodo: 'DELETE',
      })
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: CLAVES_FOTOS.porOrden(variables.ordenServicioId) })
    },
  })
}
