import { useState } from 'react'
import { AutoComplete, Button, Form, Input, InputNumber, Modal, Select } from 'antd'
import {
  useAltaRapidaCategoria,
  useCategoriasProducto,
  useGuardarProducto,
  useMarcasProductos,
} from '../api/inventario'
import type { ProductoResponse } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { AvisoError } from './AvisoError'
import { ConversorDolares } from './ConversorDolares'

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
  costo?: number
  stockInicial?: number
  stockMinimo?: number | null
  marca?: string
  fotoUrl?: string
}

const sinVacios = (valor?: string) => (valor && valor.trim() !== '' ? valor.trim() : null)

export function ModalProducto({ abierto, producto, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const categorias = useCategoriasProducto()
  const marcas = useMarcasProductos()
  const guardar = useGuardarProducto()
  const nuevaCategoria = useAltaRapidaCategoria()
  const { tienePermiso } = useSesion()
  const puedeCrearCategoria = tienePermiso(PERMISOS.inventarioCrear)
  const [creandoCategoria, setCreandoCategoria] = useState(false)
  const fotoUrl = Form.useWatch('fotoUrl', formulario)

  const crearCategoria = async (nombre: string) => {
    if (!nombre.trim()) return
    const categoria = await nuevaCategoria.mutateAsync(nombre.trim())
    formulario.setFieldsValue({ categoriaId: categoria.id })
    setCreandoCategoria(false)
  }

  const editando = Boolean(producto)

  const cerrar = () => {
    guardar.reset()
    nuevaCategoria.reset()
    setCreandoCategoria(false)
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
      costo: campos.costo ?? 0,
      stockMinimo: campos.stockMinimo != null && !Number.isNaN(campos.stockMinimo) ? campos.stockMinimo : null,
      marca: sinVacios(campos.marca),
      fotoUrl: sinVacios(campos.fotoUrl),
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
      <AvisoError error={guardar.error ?? nuevaCategoria.error} />
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
          costo: producto?.costo ?? 0,
          stockInicial: 0,
          stockMinimo: producto?.stockMinimo ?? undefined,
          marca: producto?.marca ?? '',
          fotoUrl: producto?.fotoUrl ?? '',
        }}
      >
        <Form.Item
          label="Categoría"
          name="categoriaId"
          rules={[{ required: true, message: 'Elige la categoría' }]}
          extra={
            puedeCrearCategoria &&
            (creandoCategoria ? (
              <Input.Search
                autoFocus
                placeholder="Nombre de la categoría nueva"
                enterButton="Crear"
                loading={nuevaCategoria.isPending}
                onSearch={crearCategoria}
                style={{ marginTop: 8 }}
              />
            ) : (
              <Button type="link" size="small" style={{ paddingInline: 0 }} onClick={() => setCreandoCategoria(true)}>
                ¿No está? Crear categoría
              </Button>
            ))
          }
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
        <Form.Item label="Marca" name="marca">
          <AutoComplete
            placeholder="Yamaha, Motul, NGK…"
            options={(marcas.data ?? []).map((marca) => ({ value: marca }))}
            filterOption={(texto, opcion) => (opcion?.value ?? '').toLowerCase().includes(texto.toLowerCase())}
          />
        </Form.Item>
        <Form.Item
          label="Foto"
          name="fotoUrl"
          rules={[{ type: 'url', message: 'Pega el enlace completo de la imagen, con https://' }]}
          extra="Enlace a una imagen del repuesto, por ejemplo del catálogo del proveedor."
        >
          <Input placeholder="https://…" />
        </Form.Item>
        {fotoUrl && /^https?:\/\//.test(fotoUrl) && (
          <img src={fotoUrl} alt="Foto del repuesto" className="foto-repuesto" />
        )}
        <Form.Item label="Descripción" name="descripcion">
          <Input.TextArea rows={2} />
        </Form.Item>
        <Form.Item label="Unidad" name="unidad">
          <Input placeholder="unidad, litro, juego…" />
        </Form.Item>
        <Form.Item
          label="Precio de venta"
          name="precioVenta"
          rules={[{ required: true, message: 'Ingresa el precio de venta' }]}
        >
          <InputNumber min={0} precision={2} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="¿El precio está en dólares?">
          <ConversorDolares onAplicar={(montoPen) => formulario.setFieldsValue({ precioVenta: montoPen })} />
        </Form.Item>
        <Form.Item
          label="Costo"
          name="costo"
          rules={[{ required: true, message: 'Ingresa el costo' }]}
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
          help="Opcional. Si se deja vacío, tomará el stock mínimo de la categoría o 4 por defecto."
        >
          <InputNumber min={0} style={{ width: '100%' }} placeholder="Hereda categoría o 4" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
