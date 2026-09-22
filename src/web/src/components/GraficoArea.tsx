import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { colores } from '../theme/tokens'
import { indicesVisibles, ticksDeEje, topeDeEje, type PuntoSerie } from '../utils/series'

type Props = {
  puntos: readonly PuntoSerie[]
  /** Cómo se escribe el valor en el tooltip y en la punta. */
  formatearValor?: (valor: number) => string
  /**
   * Cómo se escriben los cortes del eje. Por defecto igual que el valor, pero
   * los montos conviene pasarlos sin decimales: en el eje son siempre redondos
   * y «3,000.00» no entra en el margen.
   */
  formatearEje?: (valor: number) => string
  /** Qué se está midiendo; va en el texto alternativo y en el tooltip. */
  nombreSerie: string
  /** Cortes del eje en números enteros, para conteos como «órdenes». */
  entero?: boolean
  alto?: number
  cargando?: boolean
  vacio?: string
}

const MARGEN = { arriba: 16, derecha: 16, abajo: 28 }
const ANCHO_MINIMO = 320
/** Ancho de un dígito de IBM Plex Sans a 12px, que es el del eje. */
const ANCHO_CARACTER = 7

const identidad = (valor: number) => String(valor)

/** Ancho real del contenedor, para dibujar en píxeles y no escalar los trazos. */
function useAnchoDe(referencia: React.RefObject<HTMLDivElement | null>): number {
  const [ancho, setAncho] = useState(0)

  useLayoutEffect(() => {
    const elemento = referencia.current
    if (!elemento) {
      return
    }

    setAncho(elemento.clientWidth)

    if (typeof ResizeObserver === 'undefined') {
      return
    }

    const observador = new ResizeObserver(([entrada]) => {
      setAncho(entrada.contentRect.width)
    })
    observador.observe(elemento)
    return () => observador.disconnect()
  }, [referencia])

  return ancho
}

/**
 * Serie de tiempo de una sola medida: línea con el área debajo apenas teñida.
 *
 * Una sola serie, así que no lleva leyenda; el título de la sección dice qué
 * se está midiendo. El valor del último día va escrito sobre la punta, y el
 * resto se lee pasando el cursor o en la tabla que va debajo.
 */
export function GraficoArea({
  puntos,
  formatearValor = identidad,
  formatearEje,
  nombreSerie,
  entero = false,
  alto = 220,
  cargando = false,
  vacio = 'Sin datos en el periodo',
}: Readonly<Props>) {
  const contenedor = useRef<HTMLDivElement>(null)
  const ancho = Math.max(useAnchoDe(contenedor), ANCHO_MINIMO)
  const [activo, setActivo] = useState<number | null>(null)

  // Si cambia el periodo, el punto resaltado ya no significa lo mismo.
  useEffect(() => setActivo(null), [puntos])

  const hayDatos = puntos.length > 0
  const cortes = ticksDeEje(Math.max(...puntos.map((punto) => punto.valor), 0), { entero })
  const tope = topeDeEje(cortes)

  const escribirEje = formatearEje ?? formatearValor
  // El margen se calcula del corte más largo: con un margen fijo, «3,000.00»
  // se corta y el eje queda mintiendo.
  const largoEje = Math.max(...cortes.map((corte) => escribirEje(corte).length))
  const margenIzquierda = Math.max(36, largoEje * ANCHO_CARACTER + 16)

  const anchoTrazado = ancho - margenIzquierda - MARGEN.derecha
  const altoTrazado = alto - MARGEN.arriba - MARGEN.abajo

  const x = (indice: number) =>
    puntos.length <= 1
      ? margenIzquierda + anchoTrazado / 2
      : margenIzquierda + (indice / (puntos.length - 1)) * anchoTrazado
  const y = (valor: number) => MARGEN.arriba + altoTrazado * (1 - valor / tope)

  const coordenadas = puntos.map((punto, indice) => `${x(indice)},${y(punto.valor)}`)
  const lineaD = coordenadas.length > 0 ? `M${coordenadas.join('L')}` : ''
  const base = MARGEN.arriba + altoTrazado
  const areaD =
    coordenadas.length > 0
      ? `${lineaD}L${x(puntos.length - 1)},${base}L${x(0)},${base}Z`
      : ''

  const visibles = new Set(indicesVisibles(puntos.length, 6))
  const ultimo = puntos.length - 1
  const punto = activo === null ? null : puntos[activo]

  const total = puntos.reduce((suma, item) => suma + item.valor, 0)
  const resumen = hayDatos
    ? `${nombreSerie} por día, del ${puntos[0].etiqueta} al ${puntos[ultimo].etiqueta}. Total ${formatearValor(total)}.`
    : `${nombreSerie}: sin datos.`

  const moverCon = (evento: React.KeyboardEvent<SVGSVGElement>) => {
    if (!hayDatos) {
      return
    }
    const teclas: Record<string, number> = {
      ArrowLeft: (activo ?? puntos.length) - 1,
      ArrowRight: (activo ?? -1) + 1,
      Home: 0,
      End: ultimo,
    }
    if (evento.key === 'Escape') {
      setActivo(null)
      return
    }
    const siguiente = teclas[evento.key]
    if (siguiente === undefined) {
      return
    }
    evento.preventDefault()
    setActivo(Math.min(Math.max(siguiente, 0), ultimo))
  }

  const apuntarA = (clientX: number) => {
    const caja = contenedor.current?.getBoundingClientRect()
    if (!caja || puntos.length === 0) {
      return
    }
    const relativo = clientX - caja.left - margenIzquierda
    const paso = puntos.length <= 1 ? anchoTrazado : anchoTrazado / (puntos.length - 1)
    const indice = Math.round(relativo / paso)
    setActivo(Math.min(Math.max(indice, 0), ultimo))
  }

  if (cargando) {
    return <div className="grafico-vacio">Cargando…</div>
  }

  if (!hayDatos) {
    return <div className="grafico-vacio">{vacio}</div>
  }

  return (
    <div className="grafico-area" ref={contenedor}>
      <svg
        width={ancho}
        height={alto}
        role="img"
        aria-label={resumen}
        tabIndex={0}
        onPointerMove={(evento) => apuntarA(evento.clientX)}
        onPointerLeave={() => setActivo(null)}
        onBlur={() => setActivo(null)}
        onKeyDown={moverCon}
      >
        {cortes.map((corte) => (
          <g key={corte}>
            <line
              x1={margenIzquierda}
              x2={ancho - MARGEN.derecha}
              y1={y(corte)}
              y2={y(corte)}
              stroke={colores.divisorSuave}
              strokeWidth={1}
            />
            <text
              x={margenIzquierda - 10}
              y={y(corte)}
              textAnchor="end"
              dominantBaseline="middle"
              className="grafico-eje-texto"
            >
              {escribirEje(corte)}
            </text>
          </g>
        ))}

        <path d={areaD} fill={colores.acento600} fillOpacity={0.1} />
        <path
          d={lineaD}
          fill="none"
          stroke={colores.acento600}
          strokeWidth={2}
          strokeLinecap="round"
          strokeLinejoin="round"
        />

        {puntos.map((item, indice) =>
          visibles.has(indice) ? (
            <text
              key={item.dia}
              x={x(indice)}
              y={alto - 8}
              textAnchor={indice === 0 ? 'start' : indice === ultimo ? 'end' : 'middle'}
              className="grafico-eje-texto"
            >
              {item.etiqueta}
            </text>
          ) : null,
        )}

        {activo !== null && (
          <line
            x1={x(activo)}
            x2={x(activo)}
            y1={MARGEN.arriba}
            y2={base}
            stroke={colores.divisor}
            strokeWidth={1}
          />
        )}

        {/* La punta lleva el valor escrito; los demás días se leen al pasar. */}
        <circle
          cx={x(ultimo)}
          cy={y(puntos[ultimo].valor)}
          r={4}
          fill={colores.acento600}
          stroke={colores.fondo}
          strokeWidth={2}
        />
        <text
          x={x(ultimo)}
          dx={-8}
          y={Math.max(y(puntos[ultimo].valor) - 14, MARGEN.arriba + 10)}
          textAnchor="end"
          className="grafico-punta-valor"
        >
          {formatearValor(puntos[ultimo].valor)}
        </text>
        {activo !== null && activo !== ultimo && (
          <circle
            cx={x(activo)}
            cy={y(punto!.valor)}
            r={4}
            fill={colores.acento600}
            stroke={colores.fondo}
            strokeWidth={2}
          />
        )}
      </svg>

      {punto && (
        <div
          className="grafico-tooltip"
          style={{
            left: x(activo!),
            transform: `translate(${x(activo!) > ancho / 2 ? 'calc(-100% - 12px)' : '12px'}, 0)`,
          }}
        >
          <strong>{formatearValor(punto.valor)}</strong>
          <span>
            {nombreSerie} · {punto.etiqueta}
          </span>
        </div>
      )}
    </div>
  )
}
