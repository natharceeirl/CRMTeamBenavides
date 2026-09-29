/**
 * Cada mutación declara en `meta.exito` el aviso que ve el usuario cuando la
 * API responde bien; la caché de mutaciones lo muestra (ver main.tsx). Así
 * ninguna acción queda sin confirmación. Los errores ya los muestra AvisoError.
 */
export type AvisoExito = string | ((variables: unknown, respuesta: unknown) => string | null)

declare module '@tanstack/react-query' {
  interface Register {
    mutationMeta: { exito?: AvisoExito }
  }
}

/** Aviso que depende de lo que se mandó o de lo que respondió la API. */
export function avisoSegun<V, R = unknown>(texto: (variables: V, respuesta: R) => string | null): AvisoExito {
  return texto as AvisoExito
}

export function textoDeAviso(
  meta: { exito?: AvisoExito } | undefined,
  variables: unknown,
  respuesta: unknown,
): string | null {
  const exito = meta?.exito
  if (exito === undefined) return null
  return typeof exito === 'function' ? exito(variables, respuesta) : exito
}
