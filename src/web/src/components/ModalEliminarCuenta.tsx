import { Form, Input, Modal } from 'antd'
import { Link } from 'react-router'
import { useEliminarMiCuenta } from '../api/usuarios'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  onCerrar: () => void
  /** La cuenta ya no existe: quien abrió el modal cierra la sesión local. */
  onEliminada: () => void
}

type Campos = { password: string }

/**
 * El cliente elimina su cuenta del portal y de la app. Lo exigen Google Play y Apple;
 * el personal no lo ve, porque a sus cuentas las da de baja Gerencia.
 */
export function ModalEliminarCuenta({ abierto, onCerrar, onEliminada }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const eliminar = useEliminarMiCuenta()

  const cerrar = () => {
    eliminar.reset()
    onCerrar()
  }

  // Con mutate y no mutateAsync: el error lo muestra AvisoError y no queda una promesa rechazada sin atender.
  const enviar = ({ password }: Campos) => {
    eliminar.mutate({ password }, { onSuccess: onEliminada })
  }

  return (
    <Modal
      title="Eliminar mi cuenta"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Eliminar mi cuenta"
      okButtonProps={{ danger: true }}
      cancelText="Cancelar"
      confirmLoading={eliminar.isPending}
      destroyOnHidden
    >
      <p>
        Se borran el correo, el teléfono y la contraseña de tu cuenta, y se cierran tus sesiones en la web y en la
        app. No se puede deshacer.
      </p>
      <p>
        El taller conserva tu ficha de cliente, tus unidades y el historial de servicios y comprobantes, que la ley
        le pide guardar. Si más adelante quieres volver a usar el portal, pide un código nuevo en el taller. Más
        detalle en la <Link to="/privacidad#eliminar-cuenta">política de privacidad</Link>.
      </p>
      <AvisoError error={eliminar.error} />
      <Form<Campos> form={formulario} layout="vertical" requiredMark={false} onFinish={enviar}>
        <Form.Item
          label="Tu contraseña, para confirmar"
          name="password"
          rules={[{ required: true, message: 'Ingresa tu contraseña' }]}
        >
          <Input.Password autoComplete="current-password" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
