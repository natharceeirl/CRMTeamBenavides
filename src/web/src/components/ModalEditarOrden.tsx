import dayjs, { type Dayjs } from 'dayjs'
import { DatePicker, Form, Input, InputNumber, Modal, Select } from 'antd'
import { MODALIDADES_ATENCION, TIPOS_ATENCION, TIPOS_FALLA, useActualizarOrden } from '../api/ordenes'
import type { OrdenServicioDetalleResponse } from '../api/tipos'
import { ordenMidePorHoras } from '../utils/unidades'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  orden: OrdenServicioDetalleResponse
  onCerrar: () => void
}

type Campos = {
  tipoAtencion?: number
  modalidadAtencion?: number
  tipoFalla?: number
  motivoFalla?: string
  observaciones?: string
  fechaEstimadaEntrega?: Dayjs | null
  lecturaIngreso?: number | null
}

/**
 * Datos de recepción y seguimiento de la orden. La API solo cambia lo que se
 * manda: un campo en null queda como estaba. El técnico se asigna aparte, con
 * su propio endpoint.
 */
export function ModalEditarOrden({ abierto, orden, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const actualizar = useActualizarOrden()

  const enHoras = ordenMidePorHoras(orden)

  const cerrar = () => {
    actualizar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    await actualizar.mutateAsync({
      id: orden.id,
      datos: {
        tipoAtencion: campos.tipoAtencion,
        modalidadAtencion: campos.modalidadAtencion,
        tipoFalla: campos.tipoFalla ?? null,
        motivoFalla: campos.motivoFalla?.trim() ?? '',
        observaciones: campos.observaciones?.trim() ?? '',
        fechaEstimadaEntrega: campos.fechaEstimadaEntrega?.toISOString() ?? null,
        kilometrajeIngreso: !enHoras && campos.lecturaIngreso != null ? Math.round(campos.lecturaIngreso) : null,
        horasUsoIngreso: enHoras ? (campos.lecturaIngreso ?? null) : null,
      },
    })
    cerrar()
  }

  return (
    <Modal
      title="Editar datos de la orden"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={actualizar.isPending}
      width={640}
      destroyOnHidden
    >
      <AvisoError error={actualizar.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{
          tipoAtencion: orden.tipoAtencionId ?? 0,
          modalidadAtencion: orden.modalidadAtencionId ?? 0,
          tipoFalla: orden.tipoFallaId ?? undefined,
          motivoFalla: orden.motivoFalla ?? '',
          observaciones: orden.observaciones ?? '',
          fechaEstimadaEntrega: orden.fechaEstimadaEntrega ? dayjs(orden.fechaEstimadaEntrega) : null,
          lecturaIngreso: (enHoras ? orden.horasUsoIngreso : orden.kilometrajeIngreso) ?? null,
        }}
      >
        <div className="formulario-grid">
          <Form.Item label="Tipo de atención" name="tipoAtencion">
            <Select options={TIPOS_ATENCION} />
          </Form.Item>
          <Form.Item label="Modalidad" name="modalidadAtencion">
            <Select options={MODALIDADES_ATENCION} />
          </Form.Item>
          <Form.Item label="Tipo de falla" name="tipoFalla">
            <Select allowClear placeholder="Sin clasificar" options={TIPOS_FALLA} />
          </Form.Item>
          <Form.Item label="Entrega estimada" name="fechaEstimadaEntrega">
            <DatePicker showTime={{ format: 'HH:mm' }} format="DD/MM/YYYY HH:mm" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label={enHoras ? 'Horas de uso al ingresar' : 'Kilometraje al ingresar'} name="lecturaIngreso">
            <InputNumber min={0} precision={enHoras ? 1 : 0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Falla o pedido del cliente" name="motivoFalla" className="ancho-completo">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item label="Observaciones de recepción" name="observaciones" className="ancho-completo">
            <Input.TextArea rows={2} />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}
