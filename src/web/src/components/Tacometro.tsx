import { colores, fuentes } from '../theme/tokens'

// Dibujo del panel de inicio de sesión: un tacómetro de moto con su zona roja. Es solo
// decoración. La aguja sube al cargar la página (.tacometro-aguja en global.css).

const CENTRO = 160
const RADIO = 128
const MAXIMO = 12
const ZONA_ROJA = 9
// La escala recorre 240 grados, de abajo a la izquierda hasta abajo a la derecha.
const GRADOS_POR_MIL = 240 / MAXIMO

function punto(valor: number, radio: number) {
  const angulo = ((210 - valor * GRADOS_POR_MIL) * Math.PI) / 180
  return { x: CENTRO + radio * Math.cos(angulo), y: CENTRO - radio * Math.sin(angulo) }
}

function arco(desde: number, hasta: number, radio: number) {
  const inicio = punto(desde, radio)
  const fin = punto(hasta, radio)
  const grande = (hasta - desde) * GRADOS_POR_MIL > 180 ? 1 : 0
  return `M ${inicio.x} ${inicio.y} A ${radio} ${radio} 0 ${grande} 1 ${fin.x} ${fin.y}`
}

// Una marca cada 500 r/min; las de cada 1000 son más largas y llevan su número.
const MARCAS = Array.from({ length: MAXIMO * 2 + 1 }, (_, i) => i / 2)

export function Tacometro() {
  return (
    <svg className="tacometro" viewBox="0 0 320 236" aria-hidden="true" focusable="false">
      <path d={arco(0, MAXIMO, RADIO)} fill="none" stroke={colores.fondo} strokeOpacity={0.24} strokeWidth={2} />
      <path d={arco(ZONA_ROJA, MAXIMO, RADIO + 6)} fill="none" stroke={colores.acento} strokeWidth={8} />

      {MARCAS.map((valor) => {
        const mayor = Number.isInteger(valor)
        const afuera = punto(valor, RADIO)
        const adentro = punto(valor, RADIO - (mayor ? 16 : 8))
        const enZonaRoja = valor >= ZONA_ROJA
        return (
          <line
            key={valor}
            x1={afuera.x}
            y1={afuera.y}
            x2={adentro.x}
            y2={adentro.y}
            stroke={enZonaRoja ? colores.acento : colores.fondo}
            strokeOpacity={enZonaRoja || mayor ? 1 : 0.5}
            strokeWidth={mayor ? 3 : 1.5}
          />
        )
      })}

      {MARCAS.filter(Number.isInteger).map((valor) => {
        const { x, y } = punto(valor, RADIO - 34)
        return (
          <text
            key={valor}
            x={x}
            y={y}
            textAnchor="middle"
            dominantBaseline="central"
            fontFamily={fuentes.titulos}
            fontSize={16}
            fontWeight={600}
            fill={valor >= ZONA_ROJA ? colores.acento : colores.fondo}
          >
            {valor}
          </text>
        )
      })}

      <text
        x={CENTRO}
        y={CENTRO + 52}
        textAnchor="middle"
        fontFamily={fuentes.texto}
        fontSize={11}
        letterSpacing="0.14em"
        fill={colores.fondo}
        fillOpacity={0.6}
      >
        ×1000 R/MIN
      </text>

      {/* Dibujada apuntando arriba (6000 r/min); el giro final y la subida están en el CSS. */}
      <g className="tacometro-aguja">
        <line
          x1={CENTRO}
          y1={CENTRO + 18}
          x2={CENTRO}
          y2={CENTRO - RADIO + 24}
          stroke={colores.acento}
          strokeWidth={4}
          strokeLinecap="round"
        />
      </g>
      <circle cx={CENTRO} cy={CENTRO} r={11} fill={colores.acento} />
      <circle cx={CENTRO} cy={CENTRO} r={4} fill={colores.texto} />
    </svg>
  )
}
