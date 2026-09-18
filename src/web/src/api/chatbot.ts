import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import type {
  ConsultaBandejaResponse,
  ConsultaChatbotRequest,
  ConsultaChatbotResponse,
  FaqRequest,
  FaqResponse,
  ResolverConsultaRequest,
  SolicitarAgenteRequest,
  SolicitudAgenteResponse,
} from './tipos'

/** Estados de atención de una consulta (enum EstadoAtencionConsulta). */
export const ATENCION = {
  pendiente: 0,
  enAtencion: 1,
  resuelto: 2,
  descartado: 3,
} as const

export const nombresAtencion: Record<number, string> = {
  0: 'Pendiente',
  1: 'En atención',
  2: 'Resuelto',
  3: 'Descartado',
}

/** Solo se toma una consulta que nadie atiende todavía. */
export const puedeAsignarse = (estadoId: number) => estadoId === ATENCION.pendiente

/** Se cierra mientras no esté ya cerrada. */
export const puedeCerrarse = (estadoId: number) =>
  estadoId !== ATENCION.resuelto && estadoId !== ATENCION.descartado

export function rutaFaqs(base: string, filtros: { categoria?: string; busqueda?: string } = {}): string {
  const parametros = new URLSearchParams()
  if (filtros.categoria) parametros.set('categoria', filtros.categoria)
  if (filtros.busqueda?.trim()) parametros.set('busqueda', filtros.busqueda.trim())

  const consulta = parametros.toString()
  return `${base}${consulta ? `?${consulta}` : ''}`
}

export const clavesChatbot = {
  faqs: ['chatbot', 'faqs'] as const,
  faqsAdmin: ['chatbot', 'faqs', 'admin'] as const,
  bandeja: ['chatbot', 'consultas'] as const,
}

/** FAQs públicas: no necesitan sesión. */
export function useFaqs(filtros: { categoria?: string; busqueda?: string } = {}) {
  return useQuery({
    queryKey: [...clavesChatbot.faqs, filtros.categoria ?? '', filtros.busqueda ?? ''],
    queryFn: () => solicitar<FaqResponse[]>(rutaFaqs('/chatbot/faqs', filtros)),
  })
}

export function useConsultarChatbot() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (datos: ConsultaChatbotRequest) =>
      solicitar<ConsultaChatbotResponse>('/chatbot/consultar', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      // Cada consulta entra a la bandeja: si está abierta, que se entere.
      await consultas.invalidateQueries({ queryKey: clavesChatbot.bandeja })
    },
  })
}

export function useSolicitarAgente() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (datos: SolicitarAgenteRequest) =>
      solicitar<SolicitudAgenteResponse>('/chatbot/solicitar-agente', {
        metodo: 'POST',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesChatbot.bandeja })
    },
  })
}

export function useFaqsAdmin() {
  return useQuery({
    queryKey: clavesChatbot.faqsAdmin,
    queryFn: () => solicitar<FaqResponse[]>('/chatbot/admin/faqs'),
  })
}

export function useGuardarFaq() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id?: string; datos: FaqRequest }) =>
      id
        ? solicitar<FaqResponse>(`/chatbot/admin/faqs/${id}`, { metodo: 'PUT', cuerpo: datos })
        : solicitar<FaqResponse>('/chatbot/admin/faqs', { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesChatbot.faqs })
    },
  })
}

export function useEliminarFaq() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: (id: string) =>
      solicitar<void>(`/chatbot/admin/faqs/${id}`, { metodo: 'DELETE' }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesChatbot.faqs })
    },
  })
}

export function useBandeja() {
  return useQuery({
    queryKey: clavesChatbot.bandeja,
    queryFn: () => solicitar<ConsultaBandejaResponse[]>('/chatbot/admin/consultas'),
  })
}

export function useAsignarAgente() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, agenteId }: { id: string; agenteId: string }) =>
      solicitar<ConsultaBandejaResponse>(`/chatbot/admin/consultas/${id}/asignar`, {
        metodo: 'PUT',
        cuerpo: { agenteId },
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesChatbot.bandeja })
    },
  })
}

export function useResolverConsulta() {
  const consultas = useQueryClient()

  return useMutation({
    mutationFn: ({ id, datos }: { id: string; datos: ResolverConsultaRequest }) =>
      solicitar<ConsultaBandejaResponse>(`/chatbot/admin/consultas/${id}/resolver`, {
        metodo: 'PUT',
        cuerpo: datos,
      }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesChatbot.bandeja })
    },
  })
}
