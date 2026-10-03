import dayjs, { type Dayjs } from 'dayjs'
import { DatePicker, Form, Input, Modal, Select } from 'antd'
import { useClientes } from '../api/clientes'
import { useVehiculos } from '../api/vehiculos'
import { useCrearCita } from '../api/citas'
import type { CitaDetalleResponse } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { AvisoError } from './AvisoError'
import { identificadorUnidad } from '../utils/unidades'
import { DURACIONES_CITA, reglaFechaCita } from '../utils/agenda'

type Props = {
  abierto: boolean
  /** El día en que se pulsó «+» en la agenda, a las 09:00. */
  fechaInicial?: Dayjs
  onCerrar: () => void
  onCreada?: (cita: CitaDetalleResponse) => void
}

type Campos = {
  clienteId: string
  vehiculoId: string
  fechaHora: Dayjs
  duracionMinutos: number
  motivo: string
  observaciones?: string
}

export function ModalCita({ abierto, fechaInicial, onCerrar, onCreada }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const { tienePermiso } = useSesion()
  const clientes = useClientes()
  const crear = useCrearCita()

  const clienteId = Form.useWatch('clienteId', formulario)
  const unidades = useVehiculos(clienteId, abierto && Boolean(clienteId) && tienePermiso(PERMISOS.unidadesVer))

  const cerrar = () => {
    crear.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    const cita = await crear.mutateAsync({
      clienteId: campos.clienteId,
      vehiculoId: campos.vehiculoId,
      fechaHoraProgramada: campos.fechaHora.second(0).millisecond(0).toISOString(),
      duracionMinutos: campos.duracionMinutos,
      motivo: campos.motivo.trim(),
      observaciones: campos.observaciones?.trim() || null,
    })
    cerrar()
    onCreada?.(cita)
  }

  return (
    <Modal
      title="Nueva cita"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Agendar"
      cancelText="Cancelar"
      confirmLoading={crear.isPending}
      width={640}
      destroyOnHidden
    >
      <AvisoError error={crear.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{ duracionMinutos: 60, fechaHora: fechaInicial }}
        onValuesChange={(cambios: Partial<Campos>) => {
          // Las unidades son del cliente: al cambiarlo, la elegida ya no vale.
          if ('clienteId' in cambios) formulario.setFieldValue('vehiculoId', undefined)
        }}
      >
        <div className="formulario-grid">
          <Form.Item label="Cliente" name="clienteId" rules={[{ required: true, message: 'Elige el cliente' }]}>
            <Select
              showSearch
              optionFilterProp="label"
              loading={clientes.isPending}
              placeholder="Buscar cliente"
              options={(clientes.data ?? []).map((cliente) => ({
                value: cliente.id,
                label: cliente.nombreCompleto,
              }))}
            />
          </Form.Item>
          <Form.Item label="Unidad" name="vehiculoId" rules={[{ required: true, message: 'Elige la unidad' }]}>
            <Select
              disabled={!clienteId}
              loading={Boolean(clienteId) && unidades.isPending}
              placeholder={clienteId ? 'Unidad del cliente' : 'Primero el cliente'}
              notFoundContent="El cliente no tiene unidades registradas"
              options={(unidades.data ?? [])
                .filter((unidad) => unidad.activo)
                .map((unidad) => ({
                  value: unidad.id,
                  label: `${unidad.marca} ${unidad.modelo} · ${identificadorUnidad(unidad)}`,
                }))}
            />
          </Form.Item>
          <Form.Item
            label="Fecha y hora"
            name="fechaHora"
            rules={[reglaFechaCita]}
          >
            <DatePicker
              showTime={{ format: 'HH:mm', minuteStep: 15 }}
              format="DD/MM/YYYY HH:mm"
              disabledDate={(dia) => dia.isBefore(dayjs(), 'day')}
              style={{ width: '100%' }}
            />
          </Form.Item>
          <Form.Item label="Duración" name="duracionMinutos">
            <Select options={DURACIONES_CITA} />
          </Form.Item>
          <Form.Item
            label="Motivo"
            name="motivo"
            className="ancho-completo"
            rules={[{ required: true, whitespace: true, message: 'Escribe el motivo de la cita' }]}
          >
            <Input.TextArea rows={2} maxLength={500} placeholder="Mantenimiento, revisión, garantía…" />
          </Form.Item>
          <Form.Item label="Observaciones" name="observaciones" className="ancho-completo">
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}
