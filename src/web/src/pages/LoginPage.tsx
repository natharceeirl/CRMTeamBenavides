import { Button, Form, Input } from 'antd'
import { useNavigate } from 'react-router'
import { Logo } from '../components/Logo'

export function LoginPage() {
  const navigate = useNavigate()

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
        <Form layout="vertical" requiredMark={false} onFinish={() => navigate('/')}>
          <Form.Item label="Correo" name="correo" rules={[{ required: true, message: 'Ingresa tu correo' }]}>
            <Input type="email" autoComplete="username" placeholder="nombre@empresa.pe" />
          </Form.Item>
          <Form.Item
            label="Contraseña"
            name="contrasena"
            rules={[{ required: true, message: 'Ingresa tu contraseña' }]}
          >
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" size="large" block>
            Ingresar
          </Button>
        </Form>
        <a href="#recuperar">¿Olvidaste tu contraseña?</a>
      </div>
    </div>
  )
}
