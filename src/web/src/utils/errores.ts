/**
 * Si el error es que no se pudo descargar el código de una pantalla. Pasa sobre todo
 * después de publicar una versión nueva: la pestaña abierta pide archivos que ya no
 * existen. Volver a intentar no sirve (React guarda la descarga fallida); hay que recargar.
 */
export function esFallaDeCarga(error: unknown): boolean {
  if (!(error instanceof Error)) return false
  if (error.name === 'ChunkLoadError') return true
  return [
    'Failed to fetch dynamically imported module', // Chrome y Edge
    'error loading dynamically imported module', // Firefox
    'Importing a module script failed', // Safari
  ].some((texto) => error.message.includes(texto))
}
