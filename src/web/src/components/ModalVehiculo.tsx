import { Form, Input, InputNumber, Modal, Select } from 'antd'
import { useClientes } from '../api/clientes'
import { useGuardarVehiculo } from '../api/vehiculos'
import type { VehiculoRequest, VehiculoResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  vehiculo?: VehiculoResponse | null
  /** Cuando se abre desde la ficha de un cliente, el propietario no se elige. */
  clienteFijo?: string
  onCerrar: () => void
}

type Campos = {
  clienteId: string
  placa: string
  marca: string
  modelo: string
  anio?: number
  kilometraje?: number
  color?: string
  observaciones?: string
}

const sinVacios = (valor?: string) => (valor && valor.trim() !== '' ? valor.trim() : null)

export function ModalVehiculo({ abierto, vehiculo, clienteFijo, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const guardar = useGuardarVehiculo()
  const { data: clientes } = useClientes()

  const enviar = async (campos: Campos) => {
    const datos: VehiculoRequest = {
      clienteId: campos.clienteId,
      placa: campos.placa.trim(),
      marca: campos.marca.trim(),
      modelo: campos.modelo.trim(),
      anio: campos.anio ?? null,
      kilometraje: campos.kilometraje ?? null,
      color: sinVacios(campos.color),
      observaciones: sinVacios(campos.observaciones),
    }

    await guardar.mutateAsync({ id: vehiculo?.id, datos })
    guardar.reset()
    onCerrar()
  }

  return (
    <Modal
      title={vehiculo ? 'Editar unidad' : 'Registrar unidad'}
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
          clienteId: vehiculo?.clienteId ?? clienteFijo,
          placa: vehiculo?.placa ?? '',
          marca: vehiculo?.marca ?? 'Yamaha',
          modelo: vehiculo?.modelo ?? '',
          anio: vehiculo?.anio ?? undefined,
          kilometraje: vehiculo?.kilometraje ?? undefined,
          color: vehiculo?.color ?? '',
          observaciones: vehiculo?.observaciones ?? '',
        }}
      >
        {!clienteFijo && (
          <Form.Item
            label="Propietario"
            name="clienteId"
            rules={[{ required: true, message: 'Elige el cliente propietario' }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder="Buscar cliente"
              options={(clientes ?? []).map((cliente) => ({ value: cliente.id, label: cliente.nombreCompleto }))}
            />
          </Form.Item>
        )}
        {/* La placa es obligatoria en el backend; las unidades sin placa (náutica y
            línea de fuerza) quedan pendientes de que el Ingeniero confirme el modelo. */}
        <Form.Item label="Placa" name="placa" rules={[{ required: true, message: 'Ingresa la placa' }]}>
          <Input />
        </Form.Item>
        <Form.Item label="Marca" name="marca" rules={[{ required: true, message: 'Ingresa la marca' }]}>
          <Input />
        </Form.Item>
        <Form.Item label="Modelo" name="modelo" rules={[{ required: true, message: 'Ingresa el modelo' }]}>
          <Input />
        </Form.Item>
        <Form.Item label="Año" name="anio">
          <InputNumber min={1970} max={2100} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="Kilometraje" name="kilometraje">
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="Color" name="color">
          <Input />
        </Form.Item>
        <Form.Item label="Observaciones" name="observaciones">
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
