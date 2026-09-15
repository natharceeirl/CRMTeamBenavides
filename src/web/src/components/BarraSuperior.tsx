import type { ReactNode } from 'react'

type Props = {
  titulo: string
  antetitulo?: string
  acciones?: ReactNode
}

export function BarraSuperior({ titulo, antetitulo, acciones }: Readonly<Props>) {
  return (
    <header className="barra-superior">
      <div>
        {antetitulo && <div className="etiqueta">{antetitulo}</div>}
        <h1>{titulo}</h1>
      </div>
      {acciones && <div className="acciones">{acciones}</div>}
    </header>
  )
}
