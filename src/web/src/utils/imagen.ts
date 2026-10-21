import { colores } from '../theme/tokens'

/** La misma regla que la app (src/movil/lib/pantallas/fotos_orden.dart): lado mayor de 1920 px y JPEG al 80 %. */
export const LADO_MAXIMO = 1920
export const CALIDAD_JPEG = 0.8

/** El límite de la API para una foto (Almacenamiento:TamanioMaximoMb). */
export const PESO_MAXIMO_FOTO = 3 * 1024 * 1024

/** Hasta este peso se intenta achicar en el navegador; más grande ya no es una foto de celular. */
export const PESO_MAXIMO_ORIGINAL = 25 * 1024 * 1024

/** Una foto que ya cumple las medidas y pesa menos que esto se sube como está. */
const PESO_SIN_ACHICAR = 1024 * 1024

/** Medidas para que el lado mayor no pase de `maximo`, sin agrandar ni deformar. */
export function medidasAchicadas(ancho: number, alto: number, maximo = LADO_MAXIMO) {
  const escala = Math.min(1, maximo / Math.max(ancho, alto))
  return { ancho: Math.round(ancho * escala), alto: Math.round(alto * escala) }
}

/**
 * Achica una foto en el navegador antes de subirla, como la app: una foto de celular
 * pesa de 3 a 5 MB y así queda en unos cientos de KB, que es lo que se guarda en el
 * disco del servidor. Respeta la orientación con que la tomó la cámara. Si no hace
 * falta achicarla, o si el resultado pesaría más, devuelve la original.
 */
export async function achicarFoto(archivo: File): Promise<File> {
  const imagen = await createImageBitmap(archivo, { imageOrientation: 'from-image' })
  try {
    const { ancho, alto } = medidasAchicadas(imagen.width, imagen.height)
    if (ancho === imagen.width && alto === imagen.height && archivo.size <= PESO_SIN_ACHICAR) {
      return archivo
    }

    const lienzo = document.createElement('canvas')
    lienzo.width = ancho
    lienzo.height = alto
    const contexto = lienzo.getContext('2d')
    if (!contexto) return archivo

    // JPEG no tiene transparencia: un PNG con fondo transparente queda sobre blanco.
    contexto.fillStyle = colores.blanco
    contexto.fillRect(0, 0, ancho, alto)
    contexto.drawImage(imagen, 0, 0, ancho, alto)
    const achicada = await new Promise<Blob | null>((listo) => lienzo.toBlob(listo, 'image/jpeg', CALIDAD_JPEG))
    if (!achicada || achicada.size >= archivo.size) return archivo

    const nombre = `${archivo.name.replace(/\.[^.]+$/, '')}.jpg`
    return new File([achicada], nombre, { type: 'image/jpeg', lastModified: archivo.lastModified })
  } finally {
    imagen.close()
  }
}

/** «412 KB», «2.4 MB». */
export function pesoLegible(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / (1024 * 1024)).toLocaleString('es-PE', { maximumFractionDigits: 1 })} MB`
}
