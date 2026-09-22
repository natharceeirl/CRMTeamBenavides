import { Tooltip } from 'antd'
import { colores } from '../theme/tokens'

export type BarraDato = {
  etiqueta: string
  valor: number
  /**
   * La barra que es contexto, no el dato: se pinta en gris para que el acento
   * quede solo en lo que hay que mirar.
   */
  atenuada?: boolean
}

type Props = {
  datos: readonly BarraDato[]
  /** Cómo se escribe el valor en la columna de la derecha y en el tooltip. */
  formatearValor?: (valor: number) => string
  /** Lo que se nombra en el tooltip después del valor, como «órdenes». */
  unidad?: string
  leyenda?: readonly { texto: string; atenuada?: boolean }[]
  cargando?: boolean
  vacio?: string
}

const identidad = (valor: number) => String(valor)

/**
 * Barras horizontales para comparar magnitudes entre categorías.
 *
 * Horizontales y no verticales porque los nombres de estado son largos y en
 * columnas habría que girarlos. Un solo tono: el largo de la barra ya dice
 * cuánto, así que el color queda libre para separar lo que está vivo de lo que
 * ya cerró.
 */
export function GraficoBarras({
  datos,
  formatearValor = identidad,
  unidad,
  leyenda,
  cargando = false,
  vacio = 'Sin datos para mostrar',
}: Readonly<Props>) {
  const maximo = Math.max(...datos.map((dato) => dato.valor), 0)

  if (cargando) {
    return <div className="grafico-vacio">Cargando…</div>
  }

  if (datos.length === 0 || maximo === 0) {
    return <div className="grafico-vacio">{vacio}</div>
  }

  return (
    <div className="grafico-barras">
      {leyenda && leyenda.length > 1 && (
        <ul className="grafico-leyenda">
          {leyenda.map((item) => (
            <li key={item.texto}>
              <span
                className="grafico-leyenda-marca"
                style={{ background: item.atenuada ? colores.neutro500 : colores.acento600 }}
              />
              {item.texto}
            </li>
          ))}
        </ul>
      )}

      {datos.map((dato) => {
        const porcentaje = (dato.valor / maximo) * 100
        const detalle = [formatearValor(dato.valor), unidad].filter(Boolean).join(' ')

        return (
          <div className="grafico-barra-fila" key={dato.etiqueta}>
            <div className="grafico-barra-etiqueta">{dato.etiqueta}</div>
            <Tooltip title={`${dato.etiqueta} · ${detalle}`}>
              <div className="grafico-barra-pista" tabIndex={0}>
                <div
                  className="grafico-barra-marca"
                  style={{
                    width: `${porcentaje}%`,
                    background: dato.atenuada ? colores.neutro500 : colores.acento600,
                  }}
                />
              </div>
            </Tooltip>
            <div className="grafico-barra-valor">{formatearValor(dato.valor)}</div>
          </div>
        )
      })}
    </div>
  )
}
