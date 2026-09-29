import { Link } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { useSesion } from '../auth/sesion'
import { rutaInicial } from '../auth/acceso'

export function SinAccesoPage() {
  const sesion = useSesion()
  const inicio = rutaInicial(sesion)

  return (
    <>
      <BarraSuperior titulo="Sin acceso" />
      <div className="pagina">
        <section>
          <p>Tu usuario no tiene permiso para ver esta pantalla.</p>
          <p className="texto-secundario" style={{ marginTop: 8 }}>
            Si lo necesitas para tu trabajo, pide a Gerencia que revise tus permisos.
          </p>
          {inicio && (
            <p style={{ marginTop: 16 }}>
              <Link to={inicio}>Ir a mi pantalla de inicio</Link>
            </p>
          )}
        </section>
      </div>
    </>
  )
}
