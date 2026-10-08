import { soles } from '../utils/formato'

// Diferencias menores a medio céntimo son el mismo precio.
const TOLERANCIA = 0.005

export const precioDistintoAlDeLista = (precio: number | null | undefined, deLista: number | null | undefined) =>
  precio != null && deLista != null && Math.abs(precio - deLista) > TOLERANCIA

/**
 * Cualquier precio distinto al de lista, suba o baje, deja la operación pendiente
 * de Gerencia hasta que lo apruebe. Volver al precio de lista la libera.
 */
export function AvisoPrecioGerencia({
  precio,
  deLista,
}: Readonly<{ precio: number | null | undefined; deLista: number | null | undefined }>) {
  if (!precioDistintoAlDeLista(precio, deLista)) {
    return null
  }

  return (
    <p className="aviso-precio">
      El precio de lista es {soles(deLista ?? 0)}. Con otro precio, la operación queda pendiente hasta que Gerencia
      lo apruebe.
    </p>
  )
}
