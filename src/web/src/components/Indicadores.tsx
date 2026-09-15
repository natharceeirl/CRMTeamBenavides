type Indicador = {
  etiqueta: string
  valor: string | number
  destacado?: boolean
  /** Letra algo menor para valores largos, como montos. */
  compacto?: boolean
}

type Props = {
  items: Indicador[]
  tamano?: 'grande' | 'mediano'
}

// Solo etiqueta y valor: sin subtextos ni variaciones debajo del número.
export function Indicadores({ items, tamano = 'grande' }: Readonly<Props>) {
  return (
    <div className="indicadores">
      {items.map((item) => {
        const clases = [
          'indicador-valor',
          item.destacado ? 'destacado' : '',
          item.compacto ? 'compacto' : '',
          tamano === 'mediano' ? 'mediano' : '',
        ]
          .filter(Boolean)
          .join(' ')
        return (
          <div className="indicador" key={item.etiqueta}>
            <div className="etiqueta">{item.etiqueta}</div>
            <div className={clases}>{item.valor}</div>
          </div>
        )
      })}
    </div>
  )
}
