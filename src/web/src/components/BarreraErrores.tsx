import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Button } from 'antd'
import { useNavigate } from 'react-router'
import { esFallaDeCarga } from '../utils/errores'

function AvisoRecargar({ titulo, texto }: Readonly<{ titulo: string; texto: string }>) {
  return (
    <div className="pagina" role="alert">
      <section>
        <h2>{titulo}</h2>
        <p className="texto-secundario">{texto}</p>
        <Button type="primary" onClick={() => window.location.reload()} style={{ marginTop: 16 }}>
          Recargar
        </Button>
      </section>
    </div>
  )
}

/** Botón con el estilo de enlace del tema; la barrera es una clase y no puede usar hooks. */
function IrAlInicio() {
  const navigate = useNavigate()
  return (
    <Button type="link" onClick={() => navigate('/')} style={{ paddingInline: 0 }}>
      Ir al inicio
    </Button>
  )
}

type Props = {
  children: ReactNode
  /**
   * `pantalla` deja visible el menú y ofrece volver a intentar; `aplicacion` es el
   * último recurso si falla el propio marco y solo puede recargar.
   */
  alcance: 'pantalla' | 'aplicacion'
  /** Al cambiar (por ejemplo, la ruta), se olvida el error y se vuelve a mostrar el contenido. */
  claveReinicio?: string
}

type Estado = { error: Error | null }

/**
 * Si una pantalla falla al dibujarse, React desmonta todo y queda la página en
 * blanco. Esta barrera atrapa el error y muestra qué hacer, sin perder el menú.
 */
export class BarreraErrores extends Component<Props, Estado> {
  state: Estado = { error: null }

  static getDerivedStateFromError(error: Error): Estado {
    return { error }
  }

  componentDidCatch(error: Error, informacion: ErrorInfo) {
    console.error('Error al mostrar la pantalla', error, informacion.componentStack)
  }

  componentDidUpdate(anteriores: Props) {
    if (this.state.error && anteriores.claveReinicio !== this.props.claveReinicio) {
      this.setState({ error: null })
    }
  }

  private readonly reintentar = () => this.setState({ error: null })

  render() {
    const { error } = this.state
    if (!error) {
      return this.props.children
    }

    if (esFallaDeCarga(error)) {
      return (
        <AvisoRecargar
          titulo="Hay una versión nueva de la plataforma"
          texto="Recarga la página para seguir trabajando con la versión actual."
        />
      )
    }

    if (this.props.alcance === 'aplicacion') {
      return (
        <AvisoRecargar
          titulo="La plataforma tuvo un problema"
          texto="Recarga la página. Si vuelve a pasar, avisa al equipo de sistemas."
        />
      )
    }

    return (
      <div className="pagina" role="alert">
        <section>
          <h2>No se pudo mostrar esta pantalla</h2>
          <p className="texto-secundario">
            Puedes volver a intentarlo o ir a otra pantalla desde el menú. Si se repite, avisa al equipo de sistemas.
          </p>
          {import.meta.env.DEV && <pre className="texto-secundario">{error.message}</pre>}
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginTop: 16, alignItems: 'center' }}>
            <Button type="primary" onClick={this.reintentar}>
              Volver a intentar
            </Button>
            <IrAlInicio />
          </div>
        </section>
      </div>
    )
  }
}
