import { useEffect, type ReactNode } from 'react'
import { Alert } from 'antd'
import { Link, useLocation } from 'react-router'
import { Logo } from '../components/Logo'

/** Lo que tiene que completar Team Benavides: queda a la vista hasta que llegue. */
function Pendiente({ children }: Readonly<{ children: ReactNode }>) {
  return <mark className="pendiente">[Pendiente: {children}]</mark>
}

/**
 * Política de privacidad, pública: la piden Google Play y el App Store, y la app la
 * enlaza. La sección #eliminar-cuenta es el enlace web para eliminar la cuenta que
 * pide Google. Es un borrador según la Ley 29733 y su reglamento: falta la revisión
 * de Team Benavides y de su asesor legal, y los datos marcados como pendientes.
 */
export function PrivacidadPage() {
  const { hash } = useLocation()

  // El enlace desde la app o desde Google Play llega con #eliminar-cuenta.
  useEffect(() => {
    if (hash) document.getElementById(hash.slice(1))?.scrollIntoView()
  }, [hash])

  return (
    <div className="documento-publico">
      <header className="documento-publico-cabecera">
        <Logo alto={28} />
        <Link to="/login">Iniciar sesión</Link>
      </header>

      <main>
        <h1>Política de privacidad</h1>
        <p className="texto-secundario">Plataforma de gestión del taller y la tienda · Versión preliminar del 20/10/2026</p>

        <Alert
          type="warning"
          showIcon
          title="Borrador"
          description="Falta la revisión de Team Benavides y de su asesor legal. Lo marcado como pendiente lo completa Team Benavides antes de publicar la app."
        />

        <section>
          <h2>Quién es responsable de tus datos</h2>
          <p>
            Team Benavides S.R.L., concesionario autorizado Yamaha en Arequipa, con RUC <Pendiente>RUC</Pendiente> y
            domicilio en <Pendiente>dirección</Pendiente>. Para cualquier consulta sobre tus datos personales, escribe a{' '}
            <Pendiente>correo de contacto</Pendiente>.
          </p>
          <p>
            Tus datos forman parte del banco de datos de clientes, inscrito ante la Autoridad Nacional de Protección de
            Datos Personales con el código <Pendiente>código de inscripción</Pendiente>.
          </p>
        </section>

        <section>
          <h2>Qué datos tratamos</h2>
          <ul>
            <li>
              <strong>Identificación y contacto:</strong> nombre o razón social, tipo y número de documento (DNI, RUC o
              carné de extranjería), teléfono, correo y dirección.
            </li>
            <li>
              <strong>Tus unidades:</strong> marca, modelo, placa, número de serie o VIN, motor y lectura del medidor.
            </li>
            <li>
              <strong>Los servicios:</strong> órdenes de servicio, diagnósticos, trabajos y repuestos, presupuestos y su
              aprobación, citas, fotos de la unidad tomadas en el taller, ventas, pagos y comprobantes.
            </li>
            <li>
              <strong>Tu cuenta:</strong> el correo o DNI con el que entras, tu contraseña, que se guarda de forma que nadie
              puede leerla, y el registro de accesos con fecha, hora e IP.
            </li>
            <li>
              <strong>Tu teléfono, si usas la app y das permiso:</strong> un identificador para enviarte avisos. La app no
              accede a tus contactos ni a tu ubicación.
            </li>
          </ul>
        </section>

        <section>
          <h2>Para qué los usamos</h2>
          <ul>
            <li>Atender tus órdenes de servicio, citas, presupuestos y compras, y avisarte de su avance.</li>
            <li>Emitir y guardar comprobantes, y cumplir las obligaciones tributarias y legales.</li>
            <li>
              Registrar ventas, servicios y garantías de unidades Yamaha ante Yamaha Motor del Perú, como concesionario
              autorizado.
            </li>
            <li>Darte acceso al portal y a la app, y protegerlos; por ejemplo, bloqueando una cuenta tras varios intentos fallidos.</li>
          </ul>
          <p>No vendemos tus datos ni los usamos para publicidad.</p>
        </section>

        <section>
          <h2>Con quién los compartimos</h2>
          <ul>
            <li>
              <strong>Yamaha Motor del Perú:</strong> los datos de las ventas y de los servicios técnicos de unidades Yamaha,
              incluido tu teléfono de contacto, para las garantías y el seguimiento de la marca.{' '}
              <Pendiente>confirmar qué datos se envían y desde cuándo</Pendiente>
            </li>
            <li>
              <strong>Proveedores que trabajan para nosotros, solo para ese fin:</strong> el alojamiento de la plataforma (
              <Pendiente>proveedor de hosting</Pendiente>) y Google Firebase, que envía los avisos al teléfono. Algunos de
              sus servidores están fuera del Perú.
            </li>
            <li>
              <strong>Autoridades,</strong> cuando la ley lo exige, como la SUNAT.
            </li>
          </ul>
        </section>

        <section>
          <h2>Cuánto tiempo los guardamos</h2>
          <ul>
            <li>Los datos de tu cuenta, mientras la tengas. Si la eliminas, se borran.</li>
            <li>
              Tu ficha de cliente, tus unidades y el historial de servicios, mientras seas cliente del taller y para
              atender garantías: <Pendiente>plazo</Pendiente>.
            </li>
            <li>Los comprobantes y registros contables, el plazo que exige la ley tributaria.</li>
            <li>Las copias de seguridad de la plataforma se renuevan cada día y se guardan hasta 14 días.</li>
          </ul>
        </section>

        <section>
          <h2>Tus derechos</h2>
          <p>
            Puedes pedir el acceso, la rectificación, la cancelación o la oposición al tratamiento de tus datos, y revocar
            tu consentimiento. Escríbenos a <Pendiente>correo de contacto</Pendiente> o acércate al taller con tu documento.
            Respondemos en los plazos de la Ley 29733 y su reglamento. Si no estás conforme con la respuesta, puedes acudir
            a la Autoridad Nacional de Protección de Datos Personales.
          </p>
        </section>

        <section id="eliminar-cuenta">
          <h2>Eliminar tu cuenta</h2>
          <ul>
            <li>
              <strong>Desde la app:</strong> en el menú de tu cuenta, «Eliminar mi cuenta».
            </li>
            <li>
              <strong>Desde la web:</strong> <Link to="/login">inicia sesión</Link> y elige «Eliminar mi cuenta» en la barra
              lateral.
            </li>
            <li>
              <strong>Si no puedes entrar:</strong> escríbenos a <Pendiente>correo de contacto</Pendiente> o acércate al
              taller.
            </li>
          </ul>
          <p>
            Se borran el correo, el teléfono y la contraseña de la cuenta, tus avisos y los teléfonos registrados para
            recibirlos, y se cierran tus sesiones. El taller conserva tu ficha de cliente, tus unidades y el historial de
            servicios y comprobantes, que la ley le pide guardar y que sirven para tus garantías. Si quieres que también se
            cancelen, pídelo como se explica en «Tus derechos»: se conservará solo lo que la ley obliga.
          </p>
        </section>

        <section>
          <h2>Cómo protegemos tus datos</h2>
          <p>
            La plataforma funciona solo por conexiones cifradas (HTTPS). Cada persona del taller ve únicamente lo que su
            rol necesita, cada cambio importante queda registrado con su autor, y la base de datos tiene copias de
            seguridad diarias.
          </p>
        </section>

        <section>
          <h2>Menores de edad</h2>
          <p>
            La plataforma es para clientes mayores de edad. <Pendiente>confirmar con el asesor legal</Pendiente>
          </p>
        </section>

        <section>
          <h2>Cambios a esta política</h2>
          <p>Si la política cambia, publicamos aquí la versión vigente con su fecha.</p>
        </section>
      </main>
    </div>
  )
}
