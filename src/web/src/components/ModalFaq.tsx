import { Form, Input, InputNumber, Modal, Switch } from 'antd'
import { useGuardarFaq } from '../api/chatbot'
import type { FaqResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  faq?: FaqResponse | null
  onCerrar: () => void
}

type Campos = {
  categoria: string
  pregunta: string
  respuesta: string
  palabrasClave?: string
  orden: number
  activo: boolean
}

export function ModalFaq({ abierto, faq, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const guardar = useGuardarFaq()

  const cerrar = () => {
    guardar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    await guardar.mutateAsync({
      id: faq?.id,
      datos: {
        categoria: campos.categoria.trim(),
        pregunta: campos.pregunta.trim(),
        respuesta: campos.respuesta.trim(),
        palabrasClave: campos.palabrasClave?.trim() ? campos.palabrasClave.trim() : null,
        orden: campos.orden,
        // Solo la actualización acepta el campo activo.
        ...(faq ? { activo: campos.activo } : {}),
      },
    })

    cerrar()
  }

  return (
    <Modal
      title={faq ? 'Editar pregunta' : 'Nueva pregunta frecuente'}
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
          categoria: faq?.categoria ?? '',
          pregunta: faq?.pregunta ?? '',
          respuesta: faq?.respuesta ?? '',
          palabrasClave: faq?.palabrasClave ?? '',
          orden: faq?.orden ?? 0,
          activo: faq?.activo ?? true,
        }}
      >
        <Form.Item
          label="Categoría"
          name="categoria"
          rules={[{ required: true, message: 'Ingresa la categoría' }]}
        >
          <Input placeholder="Horarios, taller, repuestos…" />
        </Form.Item>
        <Form.Item
          label="Pregunta"
          name="pregunta"
          rules={[{ required: true, message: 'Ingresa la pregunta' }]}
        >
          <Input placeholder="¿En qué horario atienden?" />
        </Form.Item>
        <Form.Item
          label="Respuesta"
          name="respuesta"
          rules={[{ required: true, message: 'Ingresa la respuesta' }]}
        >
          <Input.TextArea rows={4} />
        </Form.Item>
        <Form.Item
          label="Palabras clave"
          name="palabrasClave"
          extra="Separadas por comas. Con esto el chatbot encuentra la pregunta."
        >
          <Input placeholder="horario, atención, sábado" />
        </Form.Item>
        <Form.Item label="Orden" name="orden">
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>
        {faq && (
          <Form.Item label="Activa" name="activo" valuePropName="checked">
            <Switch />
          </Form.Item>
        )}
      </Form>
    </Modal>
  )
}
