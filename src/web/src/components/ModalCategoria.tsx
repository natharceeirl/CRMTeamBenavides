import { Form, Input, Modal } from 'antd'
import { useGuardarCategoria } from '../api/inventario'
import type { CategoriaProductoResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  categoria?: CategoriaProductoResponse | null
  onCerrar: () => void
}

type Campos = {
  nombre: string
}

export function ModalCategoria({ abierto, categoria, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const guardar = useGuardarCategoria()

  const cerrar = () => {
    guardar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    await guardar.mutateAsync({ id: categoria?.id, datos: { nombre: campos.nombre.trim() } })
    cerrar()
  }

  return (
    <Modal
      title={categoria ? 'Editar categoría' : 'Nueva categoría'}
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
        initialValues={{ nombre: categoria?.nombre ?? '' }}
      >
        <Form.Item label="Nombre" name="nombre" rules={[{ required: true, message: 'Ingresa el nombre' }]}>
          <Input placeholder="Lubricantes, frenos, filtros…" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
