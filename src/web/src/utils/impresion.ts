/**
 * Abre en otra pestaña un documento HTML para ver e imprimir. La pestaña se
 * abre en el mismo clic, antes de esperar a la API: si se abriera después, el
 * navegador la tomaría por una ventana emergente y la bloquearía. El documento
 * trae su propio botón de imprimir, así la persona revisa antes.
 */
export async function abrirDocumento(obtenerHtml: () => Promise<string>): Promise<void> {
  const ventana = window.open('', '_blank')
  if (!ventana) {
    throw new Error('El navegador bloqueó la pestaña del documento. Permite las ventanas emergentes de este sitio.')
  }

  try {
    const html = await obtenerHtml()
    const url = URL.createObjectURL(new Blob([html], { type: 'text/html;charset=utf-8' }))
    ventana.location.href = url
    // La pestaña ya cargó el documento; la URL temporal se libera después.
    window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
  } catch (fallo) {
    ventana.close()
    throw fallo
  }
}

/** Escapa un texto para insertarlo en HTML. */
export function escaparHtml(texto: string | number | null | undefined): string {
  return String(texto ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;')
}
