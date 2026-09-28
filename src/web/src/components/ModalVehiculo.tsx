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
  tipoUnidad?: number
  placa?: string
  numeroSerieVIN?: string
  numeroMotor?: string
  marca: string
  modelo: string
  anio?: number
  tipoMedidor?: number
  lecturaMedidorActual?: number
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
    const medidor =
      campos.lecturaMedidorActual != null && !Number.isNaN(campos.lecturaMedidorActual)
        ? campos.lecturaMedidorActual
        : campos.kilometraje != null
          ? campos.kilometraje
          : null

    const datos: VehiculoRequest = {
      clienteId: campos.clienteId,
      tipoUnidad: campos.tipoUnidad != null ? Number(campos.tipoUnidad) : 0,
      placa: sinVacios(campos.placa),
      numeroSerieVIN: sinVacios(campos.numeroSerieVIN),
      numeroMotor: sinVacios(campos.numeroMotor),
      marca: campos.marca.trim(),
      modelo: campos.modelo.trim(),
      anio: campos.anio ?? null,
      tipoMedidor: campos.tipoMedidor != null ? Number(campos.tipoMedidor) : 0,
      lecturaMedidorActual: medidor,
      kilometraje: campos.kilometraje ?? (medidor != null ? Math.round(medidor) : null),
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
          tipoUnidad: vehiculo?.tipoUnidadId ?? 0,
          placa: vehiculo?.placa ?? '',
          numeroSerieVIN: vehiculo?.numeroSerieVIN ?? '',
          numeroMotor: vehiculo?.numeroMotor ?? '',
          marca: vehiculo?.marca ?? 'Yamaha',
          modelo: vehiculo?.modelo ?? '',
          anio: vehiculo?.anio ?? undefined,
          tipoMedidor: vehiculo?.tipoMedidorId ?? 0,
          lecturaMedidorActual: vehiculo?.lecturaMedidorActual ?? vehiculo?.kilometraje ?? undefined,
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
        <Form.Item label="Tipo de unidad" name="tipoUnidad">
          <Select
            options={[
              { value: 0, label: 'Motocicleta' },
              { value: 1, label: 'Cuatrimoto' },
              { value: 2, label: 'Moto acuática' },
              { value: 3, label: 'Generador' },
              { value: 4, label: 'Otro' },
            ]}
          />
        </Form.Item>
        <Form.Item
          label="Placa"
          name="placa"
          rules={[
            ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value && !getFieldValue('numeroSerieVIN')) {
                  return Promise.reject(new Error('Ingresa la placa o el VIN / Nro. de serie'))
                }
                return Promise.resolve()
              },
            }),
          ]}
        >
          <Input placeholder="Requerido si no tiene VIN / Serie" />
        </Form.Item>
        <Form.Item label="VIN / Nro. de serie" name="numeroSerieVIN">
          <Input placeholder="Requerido para náutica / línea de fuerza sin placa" />
        </Form.Item>
        <Form.Item label="Nro. de motor" name="numeroMotor">
          <Input placeholder="Número o serie de motor" />
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
        <Form.Item label="Tipo de medidor" name="tipoMedidor">
          <Select
            options={[
              { value: 0, label: 'Kilómetros (km)' },
              { value: 1, label: 'Horas (hrs)' },
            ]}
          />
        </Form.Item>
        <Form.Item label="Lectura actual del medidor" name="lecturaMedidorActual">
          <InputNumber min={0} precision={2} style={{ width: '100%' }} placeholder="Km u horas actuales" />
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
