import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { solicitar } from './http'
import { avisoSegun } from './avisos'
import { clavesOrdenes } from './ordenes'
import { clavesVentas } from './ventas'
import type { MetodoPagoResponse, PagoResponse, RegistrarPagoRequest } from './tipos'

/** Estado de pago que calcula el backend a partir de lo pagado y el saldo. */
export const ESTADO_PAGO = {
  pendiente: 'Pendiente',
  parcial: 'Parcial',
  pagado: 'Pagado',
} as const

/** Solo se cobra lo que tiene saldo y un total mayor a cero. */
export const tieneSaldo = (situacion: { total?: number; saldo?: number }) =>
  (situacion.total ?? 0) > 0 && (situacion.saldo ?? 0) > 0

export function useMetodosPago(habilitado = true) {
  return useQuery({
    queryKey: ['metodos-pago'],
    queryFn: () => solicitar<MetodoPagoResponse[]>('/metodos-pago'),
    enabled: habilitado,
    staleTime: 5 * 60_000,
  })
}

const avisoDePago = avisoSegun<{ datos: RegistrarPagoRequest }>(({ datos }) =>
  datos.esAnticipo ? 'Adelanto registrado' : 'Pago registrado',
)

/** Pago o saldo de una venta confirmada. Si la venta liquidó una orden, también cambia su saldo. */
export function useRegistrarPagoVenta() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: avisoDePago },
    mutationFn: ({ ventaId, datos }: { ventaId: string; datos: RegistrarPagoRequest }) =>
      solicitar<PagoResponse>(`/ventas/${ventaId}/pagos`, { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await Promise.all([
        consultas.invalidateQueries({ queryKey: clavesVentas.todas }),
        consultas.invalidateQueries({ queryKey: clavesOrdenes.todas }),
      ])
    },
  })
}

/** Adelanto sobre una orden que todavía no se liquida. Al liquidarla pasa a la venta. */
export function useRegistrarPagoOrden() {
  const consultas = useQueryClient()

  return useMutation({
    meta: { exito: avisoDePago },
    mutationFn: ({ ordenId, datos }: { ordenId: string; datos: RegistrarPagoRequest }) =>
      solicitar<PagoResponse>(`/ordenes-servicio/${ordenId}/pagos`, { metodo: 'POST', cuerpo: datos }),
    onSuccess: async () => {
      await consultas.invalidateQueries({ queryKey: clavesOrdenes.todas })
    },
  })
}
