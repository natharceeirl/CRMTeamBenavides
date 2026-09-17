import { Form, Input, Modal } from 'antd'
import { useGuardarCliente } from '../api/clientes'
import type { ClienteRequest, ClienteResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  cliente?: ClienteResponse | null
  onCerrar: () => void
}

type Campos = {
  nombreCompleto: string
  razonSocial?: string
  documentoIdentidad?: string
  telefono?: string
  email?: string
  direccion?: string
  observaciones?: string
}

const sinVacios = (valor?: string) => (valor && valor.trim() !== '' ? valor.trim() : null)

export function ModalCliente({ abierto, cliente, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const guardar = useGuardarCliente()

  const enviar = async (campos: Campos) => {
    const datos: ClienteRequest = {
      nombreCompleto: campos.nombreCompleto.trim(),
      razonSocial: sinVacios(campos.razonSocial),
      documentoIdentidad: sinVacios(campos.documentoIdentidad),
      telefono: sinVacios(campos.telefono),
      email: sinVacios(campos.email),
      direccion: sinVacios(campos.direccion),
      observaciones: sinVacios(campos.observaciones),
    }

    await guardar.mutateAsync({ id: cliente?.id, datos })
    guardar.reset()
    onCerrar()
  }

  return (
    <Modal
      title={cliente ? 'Editar cliente' : 'Registrar cliente'}
      open={abierto}
      onCancel={() => {
        guardar.reset()
        onCerrar()
      }}
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
        initialValues={{
          nombreCompleto: cliente?.nombreCompleto ?? '',
          razonSocial: cliente?.razonSocial ?? '',
          documentoIdentidad: cliente?.documentoIdentidad ?? '',
          telefono: cliente?.telefono ?? '',
          email: cliente?.email ?? '',
          direccion: cliente?.direccion ?? '',
          observaciones: cliente?.observaciones ?? '',
        }}
      >
        <Form.Item
          label="Nombre completo"
          name="nombreCompleto"
          rules={[{ required: true, message: 'Ingresa el nombre del cliente' }]}
        >
          <Input placeholder="Nombre y apellidos, o contacto de la empresa" />
        </Form.Item>
        <Form.Item label="Razón social" name="razonSocial">
          <Input placeholder="Solo si el cliente es empresa" />
        </Form.Item>
        {/* El backend guarda un solo campo de documento: el tipo (DNI/RUC/CE) está pendiente. */}
        <Form.Item label="Documento (DNI o RUC)" name="documentoIdentidad">
          <Input />
        </Form.Item>
        <Form.Item label="Teléfono" name="telefono">
          <Input />
        </Form.Item>
        <Form.Item label="Correo" name="email" rules={[{ type: 'email', message: 'Correo inválido' }]}>
          <Input />
        </Form.Item>
        <Form.Item label="Dirección" name="direccion">
          <Input />
        </Form.Item>
        <Form.Item label="Observaciones" name="observaciones">
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
