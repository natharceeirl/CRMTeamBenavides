import type { CSSProperties } from 'react'
import { Tag } from 'antd'
import { colores } from '../theme/tokens'
import { estadosOrden, type EstadoOrden } from '../data/ejemplo'

const estilos: Record<EstadoOrden, CSSProperties> = {
  recepcion: { background: 'transparent', color: colores.acento700, borderColor: colores.acento },
  diagnostico: { background: colores.neutro100, color: colores.neutro800, borderColor: colores.neutro300 },
  esperando_aprobacion: { background: colores.texto, color: colores.fondo, borderColor: colores.texto },
  en_reparacion: { background: colores.acento100, color: colores.acento800, borderColor: colores.acento200 },
  esperando_repuesto: { background: colores.texto, color: colores.fondo, borderColor: colores.texto },
  control_calidad: { background: colores.acento100, color: colores.acento800, borderColor: colores.acento200 },
  lista_entrega: { background: colores.acento600, color: colores.blanco, borderColor: colores.acento600 },
  entregada: { background: colores.neutro100, color: colores.neutro800, borderColor: colores.neutro300 },
  anulada: {
    background: 'transparent',
    color: colores.textoSecundario,
    borderColor: colores.neutro300,
    textDecoration: 'line-through',
  },
}

export function EstadoOrdenTag({ estado }: Readonly<{ estado: EstadoOrden }>) {
  return (
    <Tag style={{ ...estilos[estado], marginInlineEnd: 0, fontWeight: 600 }}>{estadosOrden[estado]}</Tag>
  )
}
