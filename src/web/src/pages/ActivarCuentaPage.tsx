import { useState } from 'react'
import { Alert, Button, Form, Input } from 'antd'
import { Link, Navigate, useNavigate } from 'react-router'
import { Logo } from '../components/Logo'
import { useSesion } from '../auth/sesion'
import { ErrorApi, activarCuenta } from '../api/http'

type Datos = {
  documento: string
  codigo: string
  email?: string
  contrasena: string
  repetida: string
}

/**
 * El cliente activa su cuenta del portal con su DNI y el código de 6 dígitos que le dan
 * en el taller. Queda dentro de una vez; después entra con su DNI y su contraseña.
 */
export function ActivarCuentaPage() {
  const navigate = useNavigate()
  const { autenticado } = useSesion()
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  if (autenticado) {
    return <Navigate to="/" replace />
  }

  const activar = async ({ documento, codigo, email, contrasena }: Datos) => {
    setError(null)
    setEnviando(true)

    try {
      await activarCuenta({
        numeroDocumento: documento.trim(),
        codigo: codigo.trim(),
        password: contrasena,
        email: email?.trim() || null,
      })
      // El inicio lleva al cliente a su portal.
      navigate('/', { replace: true })
    } catch (fallo) {
      setError(fallo instanceof ErrorApi ? fallo.message : 'No se pudo activar la cuenta.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="login">
      <div className="login-marca">
        <Logo variante="oscuro" alto={40} />
        <div className="login-marca-texto">
          <h1>Portal del cliente</h1>
          <p>Sigue el trabajo de tu unidad, aprueba presupuestos y revisa tus comprobantes.</p>
        </div>
      </div>
      <div className="login-formulario">
        <div>
          <div className="etiqueta">Plataforma Team Benavides</div>
          <h2 className="login-titulo">Activar mi cuenta</h2>
          <p className="texto-secundario">
            Escribe tu DNI y el código que te dieron en el taller. Después entras con tu DNI y la contraseña que elijas.
          </p>
        </div>
        {error && <Alert type="error" title={error} showIcon />}
        <Form<Datos> layout="vertical" requiredMark={false} onFinish={activar} disabled={enviando}>
          <Form.Item label="DNI" name="documento" rules={[{ required: true, whitespace: true, message: 'Ingresa tu DNI' }]}>
            <Input autoComplete="username" inputMode="numeric" placeholder="12345678" />
          </Form.Item>
          <Form.Item
            label="Código de activación"
            name="codigo"
            rules={[{ required: true, pattern: /^\d{6}$/, message: 'El código tiene 6 dígitos' }]}
          >
            <Input autoComplete="one-time-code" inputMode="numeric" maxLength={6} placeholder="6 dígitos" />
          </Form.Item>
          <Form.Item
            label="Correo (opcional)"
            name="email"
            extra="También podrás entrar con él."
            rules={[{ type: 'email', message: 'Revisa el correo' }]}
          >
            <Input autoComplete="email" />
          </Form.Item>
          <Form.Item
            label="Contraseña nueva"
            name="contrasena"
            rules={[{ required: true, min: 6, message: 'Al menos 6 caracteres' }]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Form.Item
            label="Repite la contraseña"
            name="repetida"
            dependencies={['contrasena']}
            rules={[
              { required: true, message: 'Repite la contraseña' },
              ({ getFieldValue }) => ({
                validator: (_, valor) =>
                  !valor || valor === getFieldValue('contrasena')
                    ? Promise.resolve()
                    : Promise.reject(new Error('Las contraseñas no coinciden')),
              }),
            ]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" size="large" block loading={enviando}>
            Activar y entrar
          </Button>
        </Form>
        <Link to="/login" className="login-pie">
          Ya tengo cuenta: iniciar sesión
        </Link>
      </div>
    </div>
  )
}
