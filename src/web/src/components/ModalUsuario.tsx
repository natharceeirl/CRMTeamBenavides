import { Form, Input, Modal } from 'antd'
import { useActualizarUsuario, useCrearUsuario } from '../api/usuarios'
import type { UsuarioResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  usuario?: UsuarioResponse | null
  onCerrar: () => void
}

type Campos = {
  email: string
  password: string
  nombreCompleto: string
  phoneNumber?: string
}

const sinVacios = (valor?: string) => (valor && valor.trim() !== '' ? valor.trim() : null)

export function ModalUsuario({ abierto, usuario, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const crear = useCrearUsuario()
  const actualizar = useActualizarUsuario()

  const editando = Boolean(usuario)
  const guardando = crear.isPending || actualizar.isPending

  const cerrar = () => {
    crear.reset()
    actualizar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    // El backend solo deja cambiar nombre y teléfono: el correo y la contraseña
    // se definen al crear el usuario y todavía no tienen endpoint para editarse.
    if (usuario) {
      await actualizar.mutateAsync({
        id: usuario.id,
        datos: { nombreCompleto: campos.nombreCompleto.trim(), phoneNumber: sinVacios(campos.phoneNumber) },
      })
    } else {
      await crear.mutateAsync({
        email: campos.email.trim(),
        password: campos.password,
        nombreCompleto: campos.nombreCompleto.trim(),
        phoneNumber: sinVacios(campos.phoneNumber),
      })
    }

    cerrar()
  }

  return (
    <Modal
      title={editando ? 'Editar usuario' : 'Crear usuario'}
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={guardando}
      destroyOnHidden
    >
      <AvisoError error={crear.error ?? actualizar.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{
          email: usuario?.email ?? '',
          password: '',
          nombreCompleto: usuario?.nombreCompleto ?? '',
          phoneNumber: usuario?.phoneNumber ?? '',
        }}
      >
        <Form.Item
          label="Nombre completo"
          name="nombreCompleto"
          rules={[{ required: true, message: 'Ingresa el nombre' }]}
        >
          <Input />
        </Form.Item>
        <Form.Item
          label="Correo"
          name="email"
          rules={[
            { required: !editando, message: 'Ingresa el correo' },
            { type: 'email', message: 'Correo inválido' },
          ]}
          extra={editando ? 'El correo no se puede cambiar desde la web.' : undefined}
        >
          <Input disabled={editando} autoComplete="off" />
        </Form.Item>
        {!editando && (
          <Form.Item
            label="Contraseña"
            name="password"
            rules={[{ required: true, message: 'Ingresa una contraseña' }]}
            extra="La define quien crea el usuario."
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
        )}
        <Form.Item label="Teléfono" name="phoneNumber">
          <Input />
        </Form.Item>
      </Form>
    </Modal>
  )
}
