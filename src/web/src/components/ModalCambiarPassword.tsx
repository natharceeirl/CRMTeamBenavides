import { Form, Input, Modal, message } from 'antd'
import { useCambiarMiPassword } from '../api/usuarios'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  onCerrar: () => void
}

type Campos = {
  passwordActual: string
  passwordNueva: string
  confirmacion: string
}

/** El usuario de la sesión cambia su propia contraseña. */
export function ModalCambiarPassword({ abierto, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const cambiar = useCambiarMiPassword()

  const cerrar = () => {
    cambiar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    await cambiar.mutateAsync({
      passwordActual: campos.passwordActual,
      passwordNueva: campos.passwordNueva,
    })
    message.success('Contraseña actualizada')
    cerrar()
  }

  return (
    <Modal
      title="Cambiar contraseña"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={cambiar.isPending}
      destroyOnHidden
    >
      <AvisoError error={cambiar.error} />
      <Form<Campos> form={formulario} layout="vertical" requiredMark={false} onFinish={enviar}>
        <Form.Item
          label="Contraseña actual"
          name="passwordActual"
          rules={[{ required: true, message: 'Ingresa tu contraseña actual' }]}
        >
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <Form.Item
          label="Nueva contraseña"
          name="passwordNueva"
          rules={[{ required: true, message: 'Ingresa la nueva contraseña' }]}
          extra="Al menos 6 caracteres, con mayúscula, minúscula, número y un símbolo."
        >
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Form.Item
          label="Repite la nueva contraseña"
          name="confirmacion"
          dependencies={['passwordNueva']}
          rules={[
            { required: true, message: 'Repite la nueva contraseña' },
            ({ getFieldValue }) => ({
              validator: (_, valor) =>
                !valor || valor === getFieldValue('passwordNueva')
                  ? Promise.resolve()
                  : Promise.reject(new Error('No coincide con la nueva contraseña')),
            }),
          ]}
        >
          <Input.Password autoComplete="new-password" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
