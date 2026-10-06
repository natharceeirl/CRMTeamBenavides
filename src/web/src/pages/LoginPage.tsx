import { useState } from 'react'
import { Alert, Button, Form, Input } from 'antd'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { Logo } from '../components/Logo'
import { useSesion } from '../auth/sesion'
import { ErrorApi } from '../api/http'

type Credenciales = {
  // Correo del personal o DNI del cliente: la API acepta cualquiera de los dos.
  usuario: string
  contrasena: string
}

export function LoginPage() {
  const navigate = useNavigate()
  const ubicacion = useLocation()
  const { autenticado, entrar } = useSesion()
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  const destino = (ubicacion.state as { desde?: string } | null)?.desde ?? '/'

  if (autenticado) {
    return <Navigate to={destino} replace />
  }

  const ingresar = async ({ usuario, contrasena }: Credenciales) => {
    setError(null)
    setEnviando(true)

    try {
      await entrar(usuario.trim(), contrasena)
      navigate(destino, { replace: true })
    } catch (fallo) {
      setError(fallo instanceof ErrorApi ? fallo.message : 'No se pudo iniciar sesión.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="login">
      <div className="login-marca">
        <Logo variante="oscuro" alto={40} />
        <h1>Gestión del taller y la tienda</h1>
        <p>Concesionario autorizado Yamaha · Arequipa</p>
      </div>
      <div className="login-formulario">
        <div>
          <div className="etiqueta">Plataforma Team Benavides</div>
          <h2 className="login-titulo">Iniciar sesión</h2>
        </div>
        {error && <Alert type="error" message={error} showIcon />}
        <Form<Credenciales> layout="vertical" requiredMark={false} onFinish={ingresar} disabled={enviando}>
          <Form.Item
            label="Correo o DNI"
            name="usuario"
            rules={[{ required: true, whitespace: true, message: 'Ingresa tu correo o DNI' }]}
          >
            <Input autoComplete="username" placeholder="nombre@empresa.pe o 12345678" />
          </Form.Item>
          <Form.Item
            label="Contraseña"
            name="contrasena"
            rules={[{ required: true, message: 'Ingresa tu contraseña' }]}
          >
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" size="large" block loading={enviando}>
            Ingresar
          </Button>
        </Form>
        {/* La recuperación de contraseña vuelve cuando la API tenga su endpoint. */}
      </div>
    </div>
  )
}
