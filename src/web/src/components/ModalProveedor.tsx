import { Form, Input, Modal, Select } from 'antd'
import { useGuardarProveedor } from '../api/compras'
import { TIPO_DOCUMENTO_CLIENTE, type ProveedorResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  /** Con proveedor se edita; sin él se registra uno nuevo. */
  proveedor?: ProveedorResponse | null
  onCerrar: () => void
  onGuardado?: (proveedor: ProveedorResponse) => void
}

type Campos = {
  tipoDocumento: number
  numeroDocumento: string
  razonSocial: string
  telefono?: string
  email?: string
  direccion?: string
  contacto?: string
}

const LARGO_DOCUMENTO: Record<number, number> = {
  [TIPO_DOCUMENTO_CLIENTE.dni]: 8,
  [TIPO_DOCUMENTO_CLIENTE.ruc]: 11,
}

const sinVacios = (valor?: string) => (valor?.trim() ? valor.trim() : null)

export function ModalProveedor({ abierto, proveedor, onCerrar, onGuardado }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const guardar = useGuardarProveedor()

  const cerrar = () => {
    guardar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    const guardado = await guardar.mutateAsync({
      id: proveedor?.id,
      datos: {
        tipoDocumento: campos.tipoDocumento,
        numeroDocumento: campos.numeroDocumento.trim(),
        razonSocial: campos.razonSocial.trim(),
        telefono: sinVacios(campos.telefono),
        email: sinVacios(campos.email),
        direccion: sinVacios(campos.direccion),
        contacto: sinVacios(campos.contacto),
      },
    })
    guardar.reset()
    onGuardado?.(guardado)
    onCerrar()
  }

  return (
    <Modal
      title={proveedor ? 'Editar proveedor' : 'Registrar proveedor'}
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
        initialValues={{
          tipoDocumento: proveedor?.tipoDocumento ?? TIPO_DOCUMENTO_CLIENTE.ruc,
          numeroDocumento: proveedor?.numeroDocumento ?? '',
          razonSocial: proveedor?.razonSocial ?? '',
          telefono: proveedor?.telefono ?? '',
          email: proveedor?.email ?? '',
          direccion: proveedor?.direccion ?? '',
          contacto: proveedor?.contacto ?? '',
        }}
      >
        <div className="formulario-grid">
          <Form.Item label="Tipo de documento" name="tipoDocumento">
            <Select
              options={[
                { value: TIPO_DOCUMENTO_CLIENTE.ruc, label: 'RUC' },
                { value: TIPO_DOCUMENTO_CLIENTE.dni, label: 'DNI' },
                { value: TIPO_DOCUMENTO_CLIENTE.otro, label: 'Otro' },
              ]}
            />
          </Form.Item>
          <Form.Item
            label="Número de documento"
            name="numeroDocumento"
            dependencies={['tipoDocumento']}
            rules={[
              { required: true, whitespace: true, message: 'Ingresa el documento' },
              ({ getFieldValue }) => ({
                validator: async (_, valor: string | undefined) => {
                  const largo = LARGO_DOCUMENTO[getFieldValue('tipoDocumento') as number]
                  if (valor?.trim() && largo && !new RegExp(`^\\d{${largo}}$`).test(valor.trim())) {
                    throw new Error(`Debe tener ${largo} dígitos`)
                  }
                },
              }),
            ]}
          >
            <Input maxLength={20} />
          </Form.Item>
        </div>
        <Form.Item
          label="Razón social o nombre"
          name="razonSocial"
          rules={[{ required: true, whitespace: true, message: 'Ingresa el nombre del proveedor' }]}
        >
          <Input maxLength={200} />
        </Form.Item>
        <div className="formulario-grid">
          <Form.Item label="Teléfono" name="telefono">
            <Input maxLength={30} />
          </Form.Item>
          <Form.Item label="Correo" name="email" rules={[{ type: 'email', message: 'Correo inválido' }]}>
            <Input maxLength={150} />
          </Form.Item>
          <Form.Item label="Contacto" name="contacto" extra="Vendedor o asesor con quien se trata.">
            <Input maxLength={150} />
          </Form.Item>
          <Form.Item label="Dirección" name="direccion">
            <Input maxLength={300} />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}
