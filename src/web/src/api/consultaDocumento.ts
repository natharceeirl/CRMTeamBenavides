import { useEffect, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { FormInstance } from 'antd'
import { solicitar } from './http'
import type { ConsultaDocumentoResponse } from './tipos'

/** Un DNI tiene 8 dígitos y un RUC 11: solo esos se consultan. */
export const documentoConsultable = (numero?: string | null) => /^(\d{8}|\d{11})$/.test(numero?.trim() ?? '')

/**
 * Nombres por DNI (RENIEC) o razón social por RUC (SUNAT), con Decolecta. Cada número
 * se consulta una vez: lo que vuelve se recuerda mientras la web está abierta. Si falla,
 * el formulario sigue igual y el nombre se escribe a mano.
 */
export function useConsultaDocumento(numero: string | null | undefined, habilitado = true) {
  const limpio = numero?.trim() ?? ''
  return useQuery({
    queryKey: ['consulta-documento', limpio],
    queryFn: () => solicitar<ConsultaDocumentoResponse>(`/consulta-documento/${limpio}`),
    enabled: habilitado && documentoConsultable(limpio),
    retry: false,
    staleTime: Infinity,
    gcTime: 30 * 60_000,
  })
}

type Campo = string | (string | number)[]

/** [campo, valor] o [campo, valor, true] para llenarlo aunque tenga algo (el tipo de documento). */
export type CampoConsultado = [Campo, string | number | null | undefined] | [Campo, string | number | null | undefined, boolean]

/**
 * Pasa al formulario lo que trajo la consulta, sin pisar lo que la persona escribió:
 * solo llena un campo vacío o uno que la consulta anterior había llenado. `campos` se
 * define fuera del componente, para que no cambie en cada render.
 */
export function useRellenarConDocumento(
  formulario: FormInstance,
  datos: ConsultaDocumentoResponse | undefined,
  campos: (datos: ConsultaDocumentoResponse) => CampoConsultado[],
) {
  const autollenado = useRef(new Map<string, unknown>())

  useEffect(() => {
    if (!datos) return
    for (const [campo, valor, forzar] of campos(datos)) {
      if (valor === null || valor === undefined || valor === '') continue
      const clave = JSON.stringify(campo)
      const actual: unknown = formulario.getFieldValue(campo)
      const vacio = actual === null || actual === undefined || (typeof actual === 'string' && actual.trim() === '')
      if (forzar || vacio || actual === autollenado.current.get(clave)) {
        formulario.setFieldValue(campo, valor)
        autollenado.current.set(clave, valor)
      }
    }
  }, [datos, formulario, campos])
}
