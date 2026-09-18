import { Form, Input, InputNumber, Modal, Select } from 'antd'
import { useCategoriasProducto, useGuardarProducto } from '../api/inventario'
import type { ProductoResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  producto?: ProductoResponse | null
  onCerrar: () => void
}

type Campos = {
  categoriaId: string
  codigo: string
  nombre: string
  descripcion?: string
  unidad?: string
  precioVenta: number
  stockInicial?: number
  stockMinimo: number
}

const sinVacios = (valor?: string) => (valor && valor.trim() !== '' ? valor.trim() : null)

export function ModalProducto({ abierto, producto, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const categorias = useCategoriasProducto()
  const guardar = useGuardarProducto()

  const editando = Boolean(producto)

  const cerrar = () => {
    guardar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    const comunes = {
      categoriaId: campos.categoriaId,
      codigo: campos.codigo.trim(),
      nombre: campos.nombre.trim(),
      descripcion: sinVacios(campos.descripcion),
      unidad: sinVacios(campos.unidad),
      precioVenta: campos.precioVenta,
      stockMinimo: campos.stockMinimo,
    }

    // El stock inicial solo existe al crear: después se mueve por entradas,
    // salidas o ajustes, que es lo que deja rastro en el kardex.
    await guardar.mutateAsync({
      id: producto?.id,
      datos: producto ? comunes : { ...comunes, stockInicial: campos.stockInicial ?? 0 },
    })

    cerrar()
  }

  return (
    <Modal
      title={editando ? 'Editar producto' : 'Registrar producto'}
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
          categoriaId: producto?.categoriaId,
          codigo: producto?.codigo ?? '',
          nombre: producto?.nombre ?? '',
          descripcion: producto?.descripcion ?? '',
          unidad: producto?.unidad ?? 'unidad',
          precioVenta: producto?.precioVenta ?? 0,
          stockInicial: 0,
          stockMinimo: producto?.stockMinimo ?? 0,
        }}
      >
        <Form.Item
          label="Categoría"
          name="categoriaId"
          rules={[{ required: true, message: 'Elige la categoría' }]}
        >
          <Select
            showSearch
            optionFilterProp="label"
            loading={categorias.isPending}
            placeholder="Lubricantes, frenos, filtros…"
            options={(categorias.data ?? []).map((categoria) => ({
              value: categoria.id,
              label: categoria.nombre,
            }))}
          />
        </Form.Item>
        <Form.Item label="Código" name="codigo" rules={[{ required: true, message: 'Ingresa el código' }]}>
          <Input placeholder="REP-0001" />
        </Form.Item>
        <Form.Item label="Nombre" name="nombre" rules={[{ required: true, message: 'Ingresa el nombre' }]}>
          <Input />
        </Form.Item>
        <Form.Item label="Descripción" name="descripcion">
          <Input.TextArea rows={2} />
        </Form.Item>
        <Form.Item label="Unidad" name="unidad">
          <Input placeholder="unidad, litro, juego…" />
        </Form.Item>
        <Form.Item
          label="Precio de venta"
          name="precioVenta"
          rules={[{ required: true, message: 'Ingresa el precio' }]}
        >
          <InputNumber min={0} precision={2} style={{ width: '100%' }} />
        </Form.Item>
        {!editando && (
          <Form.Item label="Stock inicial" name="stockInicial">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        )}
        <Form.Item
          label="Stock mínimo"
          name="stockMinimo"
          rules={[{ required: true, message: 'Ingresa el stock mínimo' }]}
        >
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
