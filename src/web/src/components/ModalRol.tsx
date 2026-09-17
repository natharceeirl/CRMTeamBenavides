import { Form, Input, Modal } from 'antd'
import { useGuardarRol } from '../api/roles'
import type { RolResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  rol?: RolResponse | null
  onCerrar: () => void
}

type Campos = {
  nombre: string
  descripcion?: string
}

export function ModalRol({ abierto, rol, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const guardar = useGuardarRol()

  const cerrar = () => {
    guardar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    await guardar.mutateAsync({
      id: rol?.id,
      datos: {
        nombre: campos.nombre.trim(),
        descripcion: campos.descripcion?.trim() ? campos.descripcion.trim() : null,
      },
    })

    cerrar()
  }

  return (
    <Modal
      title={rol ? 'Editar rol' : 'Crear rol'}
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={guardar.isPending}
      destroyOnHidden
    >
      <AvisoError error={guardar.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{ nombre: rol?.nombre ?? '', descripcion: rol?.descripcion ?? '' }}
      >
        <Form.Item label="Nombre" name="nombre" rules={[{ required: true, message: 'Ingresa el nombre del rol' }]}>
          <Input placeholder="Asesor de servicio, Técnico, Almacén…" />
        </Form.Item>
        <Form.Item label="Descripción" name="descripcion">
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
