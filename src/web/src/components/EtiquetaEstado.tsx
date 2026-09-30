import type { CSSProperties, ReactNode } from 'react'
import { Tag } from 'antd'
import { colores } from '../theme/tokens'

/**
 * Tonos de las etiquetas de estado, solo con los colores de la marca:
 * - alerta: pide una acción (pendiente, por responder).
 * - suave: a medias o rechazado.
 * - hecho: resuelto (pagado, aprobado).
 * - neutro: informativo.
 * - apagado: no aplica o anulado.
 */
export type TonoEstado = 'alerta' | 'suave' | 'hecho' | 'neutro' | 'apagado'

const estilos: Record<TonoEstado, CSSProperties> = {
  alerta: { background: 'transparent', color: colores.acento700, borderColor: colores.acento600 },
  suave: { background: colores.acento100, color: colores.acento700, borderColor: colores.acento200 },
  hecho: { background: colores.texto, color: colores.blanco, borderColor: colores.texto },
  neutro: { background: colores.neutro100, color: colores.neutro800, borderColor: colores.neutro300 },
  apagado: { background: 'transparent', color: colores.textoSecundario, borderColor: colores.neutro300 },
}

export function EtiquetaEstado({ tono, children }: Readonly<{ tono: TonoEstado; children: ReactNode }>) {
  return <Tag style={{ ...estilos[tono], marginInlineEnd: 0, fontWeight: 600 }}>{children}</Tag>
}
