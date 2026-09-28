import { Form, Input, InputNumber, Modal } from 'antd'
import { useGuardarCategoria } from '../api/inventario'
import type { CategoriaProductoResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  categoria?: CategoriaProductoResponse | null
  onCerrar: () => void
}

type Campos = {
  nombre: string
  stockMinimoDefault?: number | null
}

export function ModalCategoria({ abierto, categoria, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const guardar = useGuardarCategoria()

  const cerrar = () => {
    guardar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    await guardar.mutateAsync({
      id: categoria?.id,
      datos: {
        nombre: campos.nombre.trim(),
        stockMinimoDefault:
          campos.stockMinimoDefault != null && !Number.isNaN(campos.stockMinimoDefault)
            ? campos.stockMinimoDefault
            : null,
      },
    })
    cerrar()
  }

  return (
    <Modal
      title={categoria ? 'Editar categoría' : 'Nueva categoría'}
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
          nombre: categoria?.nombre ?? '',
          stockMinimoDefault: categoria?.stockMinimoDefault ?? undefined,
        }}
      >
        <Form.Item label="Nombre" name="nombre" rules={[{ required: true, message: 'Ingresa el nombre' }]}>
          <Input placeholder="Lubricantes, frenos, filtros…" />
        </Form.Item>
        <Form.Item
          label="Stock mínimo por defecto"
          name="stockMinimoDefault"
          help="Opcional. Se aplica a los productos de esta categoría que no tengan stock mínimo propio (o 4 si se deja vacío)."
        >
          <InputNumber min={0} placeholder="Ej: 5 (opcional)" style={{ width: '100%' }} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
