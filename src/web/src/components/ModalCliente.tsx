import { Form, Input, Modal, Select } from 'antd'
import { useGuardarCliente } from '../api/clientes'
import { useConsultaDocumento, useRellenarConDocumento, type CampoConsultado } from '../api/consultaDocumento'
import type { ClienteRequest, ClienteResponse, ConsultaDocumentoResponse, TipoDocumentoCliente } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { AyudaConsultaDocumento } from './AyudaConsultaDocumento'

type Props = {
  abierto: boolean
  cliente?: ClienteResponse | null
  onCerrar: () => void
  /** Con el cliente ya guardado: la cita lo deja elegido. */
  onGuardado?: (cliente: ClienteResponse) => void
}

type Campos = {
  nombreCompleto: string
  razonSocial?: string
  tipoDocumento?: TipoDocumentoCliente | null
  numeroDocumento?: string
  telefono?: string
  email?: string
  direccion?: string
  observaciones?: string
}

const sinVacios = (valor?: string) => (valor && valor.trim() !== '' ? valor.trim() : null)

// Con DNI se completa el nombre; con RUC, también la razón social y la dirección.
const camposDelDocumento = (datos: ConsultaDocumentoResponse): CampoConsultado[] => [
  ['tipoDocumento', datos.tipoDocumentoId, true],
  ['nombreCompleto', datos.nombreCompleto],
  ['razonSocial', datos.razonSocial],
  ['direccion', datos.direccion],
]

export function ModalCliente({ abierto, cliente, onCerrar, onGuardado }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const guardar = useGuardarCliente()
  const numeroDocumento = Form.useWatch('numeroDocumento', formulario)
  // Al editar, solo si cambió el documento: el cliente ya tiene su nombre.
  const documentoInicial = cliente?.numeroDocumento ?? cliente?.documentoIdentidad ?? ''
  const consulta = useConsultaDocumento(numeroDocumento, abierto && numeroDocumento?.trim() !== documentoInicial)
  useRellenarConDocumento(formulario, consulta.data, camposDelDocumento)

  const enviar = async (campos: Campos) => {
    const doc = sinVacios(campos.numeroDocumento)
    const datos: ClienteRequest = {
      nombreCompleto: campos.nombreCompleto.trim(),
      razonSocial: sinVacios(campos.razonSocial),
      tipoDocumento: campos.tipoDocumento ?? null,
      numeroDocumento: doc,
      documentoIdentidad: doc,
      telefono: sinVacios(campos.telefono),
      email: sinVacios(campos.email),
      direccion: sinVacios(campos.direccion),
      observaciones: sinVacios(campos.observaciones),
    }

    const guardado = await guardar.mutateAsync({ id: cliente?.id, datos })
    guardar.reset()
    onCerrar()
    onGuardado?.(guardado)
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
          tipoDocumento:
            cliente?.tipoDocumentoId ??
            (cliente?.tipoDocumento === 'DNI'
              ? 0
              : cliente?.tipoDocumento === 'RUC'
                ? 1
                : cliente?.tipoDocumento === 'Otro'
                  ? 2
                  : cliente?.documentoIdentidad?.length === 8
                    ? 0
                    : cliente?.documentoIdentidad?.length === 11
                      ? 1
                      : undefined),
          numeroDocumento: cliente?.numeroDocumento ?? cliente?.documentoIdentidad ?? '',
          telefono: cliente?.telefono ?? '',
          email: cliente?.email ?? '',
          direccion: cliente?.direccion ?? '',
          observaciones: cliente?.observaciones ?? '',
        }}
      >
        <Form.Item label="Tipo de documento" name="tipoDocumento">
          <Select
            allowClear
            placeholder="Seleccionar tipo de documento"
            options={[
              { value: 0, label: 'DNI (8 dígitos)' },
              { value: 1, label: 'RUC (11 dígitos)' },
              { value: 2, label: 'Otro' },
            ]}
          />
        </Form.Item>
        <Form.Item
          label="Número de documento"
          name="numeroDocumento"
          extra={<AyudaConsultaDocumento consulta={consulta} avisarClienteExistente={!cliente} />}
          rules={[
            ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value || !value.trim()) return Promise.resolve()
                const tipo = getFieldValue('tipoDocumento')
                if (tipo === 0 && !/^\d{8}$/.test(value.trim())) {
                  return Promise.reject(new Error('El DNI debe tener exactamente 8 dígitos numéricos'))
                }
                if (tipo === 1 && !/^\d{11}$/.test(value.trim())) {
                  return Promise.reject(new Error('El RUC debe tener exactamente 11 dígitos numéricos'))
                }
                return Promise.resolve()
              },
            }),
          ]}
        >
          <Input placeholder="DNI o RUC: el nombre se completa solo" />
        </Form.Item>
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
