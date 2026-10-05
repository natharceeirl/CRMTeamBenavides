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
  /**
   * La pide el cliente desde su portal: no elige cliente (la API toma el suyo) y
   * solo ve sus unidades.
   */
  paraCliente?: boolean
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

export function ModalCita({ abierto, paraCliente = false, fechaInicial, onCerrar, onCreada }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const { tienePermiso } = useSesion()
  const clientes = useClientes(!paraCliente)
  const crear = useCrearCita()

  const clienteElegido = Form.useWatch('clienteId', formulario)
  // El cliente ve sus unidades sin filtro: la API ya le devuelve solo las suyas.
  const clienteId = paraCliente ? undefined : clienteElegido
  const unidades = useVehiculos(
    clienteId,
    abierto && (paraCliente || Boolean(clienteId)) && tienePermiso(PERMISOS.unidadesVer),
  )
  const hayCliente = paraCliente || Boolean(clienteId)

  const cerrar = () => {
    crear.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    const cita = await crear.mutateAsync({
      clienteId: paraCliente ? null : campos.clienteId,
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
      title={paraCliente ? 'Agendar cita' : 'Nueva cita'}
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
          {!paraCliente && (
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
          )}
          <Form.Item label="Unidad" name="vehiculoId" rules={[{ required: true, message: 'Elige la unidad' }]}>
            <Select
              disabled={!hayCliente}
              loading={hayCliente && unidades.isPending}
              placeholder={hayCliente ? (paraCliente ? 'Tu unidad' : 'Unidad del cliente') : 'Primero el cliente'}
              notFoundContent={paraCliente ? 'No tienes unidades registradas' : 'El cliente no tiene unidades registradas'}
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
