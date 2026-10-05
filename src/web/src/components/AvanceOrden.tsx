import { ESTADO } from '../api/ordenes'
import { pasosDeAvance } from '../utils/avance'
import { fechaHora } from '../utils/formato'

type Props = {
  orden: Parameters<typeof pasosDeAvance>[0]
}

/** El avance de la orden con los nombres que entiende el cliente, de «Recibida» a «Entregada». */
export function AvanceOrden({ orden }: Readonly<Props>) {
  if (orden.estadoId === ESTADO.cancelada) {
    return <p className="texto-secundario">Esta orden fue cancelada.</p>
  }

  return (
    <ol className="avance">
      {pasosDeAvance(orden).map((paso) => (
        <li
          key={paso.titulo}
          className={`avance-paso ${paso.situacion}`}
          aria-current={paso.situacion === 'actual' ? 'step' : undefined}
        >
          <span className="avance-marca" aria-hidden="true" />
          <div>
            <div className="avance-titulo">{paso.titulo}</div>
            {paso.fecha && (
              <div className="texto-secundario">
                {paso.estimada ? `Estimado ${fechaHora(paso.fecha)}` : fechaHora(paso.fecha)}
              </div>
            )}
          </div>
        </li>
      ))}
    </ol>
  )
}
