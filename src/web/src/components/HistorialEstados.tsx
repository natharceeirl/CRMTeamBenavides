import { Timeline } from 'antd'
import { fechaHora } from '../utils/formato'

type Cambio = {
  id: string
  estadoAnterior: string | null
  estadoNuevo: string
  fecha: string
  usuarioNombre: string | null
  observacion: string | null
}

type Props = {
  cambios: Cambio[]
  /** Nombre de cada estado, del módulo que corresponda (citas o pedidos). */
  nombres: Record<string, string>
}

/** Historial de una cita o un pedido: cambios de estado, y en gris las anotaciones que no lo cambian. */
export function HistorialEstados({ cambios, nombres }: Readonly<Props>) {
  if (cambios.length === 0) {
    return <p className="texto-secundario">Sin cambios registrados.</p>
  }

  const ordenados = [...cambios].sort((uno, otro) => new Date(uno.fecha).getTime() - new Date(otro.fecha).getTime())

  return (
    <Timeline
      items={ordenados.map((cambio) => {
        const cambiaEstado = cambio.estadoAnterior !== cambio.estadoNuevo
        return {
          key: cambio.id,
          color: cambiaEstado ? undefined : 'gray',
          content: (
            <div>
              <strong>{cambiaEstado ? nombres[cambio.estadoNuevo] : (cambio.observacion ?? 'Actualización')}</strong>
              <div className="texto-secundario">
                {fechaHora(cambio.fecha)} · {cambio.usuarioNombre ?? 'Sistema'}
              </div>
              {cambiaEstado && cambio.observacion && <div>{cambio.observacion}</div>}
            </div>
          ),
        }
      })}
    />
  )
}
