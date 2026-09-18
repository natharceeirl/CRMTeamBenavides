import { Tag } from 'antd'
import { colores } from '../theme/tokens'
import type { ProductoResponse } from '../api/tipos'

/** El backend ya calcula `esBajoStock`; acá solo se pinta. */
export function EstadoStock({ producto }: Readonly<{ producto: ProductoResponse }>) {
  if (producto.stockActual === 0) {
    return (
      <Tag
        style={{
          background: colores.texto,
          color: colores.fondo,
          borderColor: colores.texto,
          marginInlineEnd: 0,
        }}
      >
        Agotado
      </Tag>
    )
  }

  if (producto.esBajoStock) {
    return (
      <Tag
        style={{
          background: colores.acento100,
          color: colores.acento800,
          borderColor: colores.acento200,
          marginInlineEnd: 0,
        }}
      >
        Stock bajo
      </Tag>
    )
  }

  return (
    <Tag
      style={{
        background: colores.neutro100,
        color: colores.neutro800,
        borderColor: colores.neutro300,
        marginInlineEnd: 0,
      }}
    >
      Disponible
    </Tag>
  )
}
