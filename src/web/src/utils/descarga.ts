import { solicitarBlob } from '../api/http'

/**
 * Descarga un archivo de la API (los Excel de citas y pedidos) con el token
 * puesto: un enlace directo respondería 401.
 */
export async function descargarArchivo(ruta: string, nombre: string): Promise<void> {
  const archivo = await solicitarBlob(ruta)
  const url = URL.createObjectURL(archivo)
  const enlace = document.createElement('a')
  enlace.href = url
  enlace.download = nombre
  document.body.appendChild(enlace)
  enlace.click()
  enlace.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 10_000)
}
