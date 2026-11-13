import { useEffect } from 'react'
import { Checkbox, Form, Input, InputNumber, Modal, Select } from 'antd'
import { useActualizarServicio, useCrearServicio } from '../api/servicios'
import { AvisoError } from './AvisoError'
import { TIPO_AFECTACION_IGV, opcionesAfectacionIgv, type ServicioResponse, type TipoAfectacionIgv } from '../api/tipos'

type Props = {
  abierto: boolean
  servicio: ServicioResponse | null
  onCerrar: () => void
}

type Campos = {
  nombre: string
  precioSugerido: number
  precioIncluyeIgv: boolean
  tipoAfectacionIgv: TipoAfectacionIgv
}

export function ModalServicio({ abierto, servicio, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const crear = useCrearServicio()
  const actualizar = useActualizarServicio()

  const enEdicion = Boolean(servicio)
  const cargando = crear.isPending || actualizar.isPending
  const error = crear.error ?? actualizar.error

  useEffect(() => {
    if (abierto) {
      if (servicio) {
        formulario.setFieldsValue({
          nombre: servicio.nombre,
          precioSugerido: servicio.precioSugerido,
          precioIncluyeIgv: servicio.precioIncluyeIgv,
          tipoAfectacionIgv: servicio.tipoAfectacionIgv,
        })
      } else {
        formulario.resetFields()
        formulario.setFieldsValue({
          tipoAfectacionIgv: TIPO_AFECTACION_IGV.gravado,
          precioSugerido: 0,
          // Como el repuesto: un servicio nuevo va con IGV incluido.
          precioIncluyeIgv: true,
        })
      }
    }
  }, [abierto, servicio, formulario])

  const cerrar = () => {
    crear.reset()
    actualizar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    if (servicio) {
      await actualizar.mutateAsync({
        id: servicio.id,
        datos: {
          nombre: campos.nombre,
          precioSugerido: campos.precioSugerido,
          precioIncluyeIgv: campos.precioIncluyeIgv,
          tipoAfectacionIgv: campos.tipoAfectacionIgv,
        },
      })
    } else {
      await crear.mutateAsync({
        nombre: campos.nombre,
        precioSugerido: campos.precioSugerido,
        precioIncluyeIgv: campos.precioIncluyeIgv,
        tipoAfectacionIgv: campos.tipoAfectacionIgv,
      })
    }

    cerrar()
  }

  return (
    <Modal
      title={enEdicion ? 'Editar servicio' : 'Nuevo servicio'}
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText={enEdicion ? 'Guardar' : 'Crear'}
      cancelText="Cancelar"
      confirmLoading={cargando}
      destroyOnHidden
    >
      <AvisoError error={error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
      >
        <Form.Item
          label="Nombre del servicio"
          name="nombre"
          rules={[{ required: true, message: 'Indica el nombre del servicio' }]}
        >
          <Input placeholder="Ej. Cambio de aceite y filtro" autoFocus />
        </Form.Item>
        <div className="precio-con-igv">
          <Form.Item
            label="Precio sugerido (S/)"
            name="precioSugerido"
            rules={[{ required: true, message: 'Indica el precio sugerido' }]}
          >
            <InputNumber min={0} step={0.5} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="precioIncluyeIgv" valuePropName="checked" label=" ">
            <Checkbox>Incluye IGV</Checkbox>
          </Form.Item>
        </div>
        <Form.Item
          label="Afectación IGV"
          name="tipoAfectacionIgv"
          rules={[{ required: true, message: 'Selecciona la afectación' }]}
        >
          <Select options={opcionesAfectacionIgv} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
