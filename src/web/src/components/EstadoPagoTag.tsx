import { Tag } from 'antd'
import { ESTADO_PAGO } from '../api/pagos'
import { colores } from '../theme/tokens'

const estilos: Record<string, { background: string; color: string; borderColor: string }> = {
  // Lo que se debe llama la atención; lo pagado queda sobrio.
  [ESTADO_PAGO.pendiente]: { background: 'transparent', color: colores.acento700, borderColor: colores.acento600 },
  [ESTADO_PAGO.parcial]: { background: colores.acento100, color: colores.acento700, borderColor: colores.acento200 },
  [ESTADO_PAGO.pagado]: { background: colores.texto, color: colores.blanco, borderColor: colores.texto },
}

/** Pendiente, Parcial o Pagado, tal como lo calcula el backend. */
export function EstadoPagoTag({ estado }: Readonly<{ estado: string | null | undefined }>) {
  const nombre = estado ?? ESTADO_PAGO.pendiente
  return (
    <Tag style={{ ...(estilos[nombre] ?? estilos[ESTADO_PAGO.pendiente]), marginInlineEnd: 0, fontWeight: 600 }}>
      {nombre}
    </Tag>
  )
}
