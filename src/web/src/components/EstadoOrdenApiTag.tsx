import type { CSSProperties } from 'react'
import { Tag } from 'antd'
import { colores } from '../theme/tokens'
import { ESTADO, nombresEstado } from '../api/ordenes'

/**
 * Estado de una orden que viene de la API.
 *
 * Convive con EstadoOrdenTag, que pinta los nueve estados propuestos en el mapa
 * funcional y todavía usan los wireframes del tablero. Cuando el Ingeniero
 * cierre el flujo con el cliente, los dos se unifican.
 */
const estilos: Record<number, CSSProperties> = {
  [ESTADO.abierta]: {
    background: 'transparent',
    color: colores.acento700,
    borderColor: colores.acento,
  },
  [ESTADO.diagnostico]: {
    background: colores.neutro100,
    color: colores.neutro800,
    borderColor: colores.neutro300,
  },
  [ESTADO.aprobada]: {
    background: colores.texto,
    color: colores.fondo,
    borderColor: colores.texto,
  },
  [ESTADO.enProceso]: {
    background: colores.acento100,
    color: colores.acento800,
    borderColor: colores.acento200,
  },
  [ESTADO.lista]: {
    background: colores.acento600,
    color: colores.blanco,
    borderColor: colores.acento600,
  },
  [ESTADO.entregada]: {
    background: colores.neutro100,
    color: colores.neutro800,
    borderColor: colores.neutro300,
  },
  [ESTADO.cancelada]: {
    background: 'transparent',
    color: colores.textoSecundario,
    borderColor: colores.neutro300,
    textDecoration: 'line-through',
  },
}

export function EstadoOrdenApiTag({ estadoId }: Readonly<{ estadoId: number }>) {
  return (
    <Tag style={{ ...estilos[estadoId], marginInlineEnd: 0, fontWeight: 600 }}>
      {nombresEstado[estadoId] ?? 'Desconocido'}
    </Tag>
  )
}
