import { AutoComplete, Form, Input, InputNumber, Modal, Select } from 'antd'
import { useAltaRapidaProducto, useMarcasProductos } from '../api/inventario'
import { useAltaRapidaServicio } from '../api/servicios'
import { TIPO_AFECTACION_IGV, type ProductoResponse, type ServicioResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { ConversorDolares } from './ConversorDolares'

const sinVacios = (valor?: string) => (valor?.trim() ? valor.trim() : null)

type CamposRepuesto = {
  nombre: string
  codigo?: string
  marca?: string
  precioVenta: number
  stockInicial?: number
}

/**
 * Registro mínimo de un repuesto que no está en el catálogo. Sin código, el
 * backend genera uno; sin categoría, usa la general. Si el código ya existe,
 * devuelve ese repuesto. Desde una compra no se pide stock inicial: lo suma la
 * compra, y escribirlo aquí lo haría entrar dos veces.
 */
export function ModalAltaRapidaRepuesto({
  abierto,
  sinStockInicial = false,
  onCerrar,
  onRegistrado,
}: Readonly<{
  abierto: boolean
  sinStockInicial?: boolean
  onCerrar: () => void
  onRegistrado: (repuesto: ProductoResponse) => void
}>) {
  const [formulario] = Form.useForm<CamposRepuesto>()
  const marcas = useMarcasProductos()
  const registrar = useAltaRapidaProducto()

  const cerrar = () => {
    registrar.reset()
    onCerrar()
  }

  const enviar = async (campos: CamposRepuesto) => {
    const repuesto = await registrar.mutateAsync({
      nombre: campos.nombre.trim(),
      codigo: sinVacios(campos.codigo)?.toUpperCase() ?? null,
      marca: sinVacios(campos.marca),
      precioVenta: campos.precioVenta,
      categoriaId: null,
      stockInicial: sinStockInicial ? 0 : (campos.stockInicial ?? 0),
    })
    registrar.reset()
    onRegistrado(repuesto)
  }

  return (
    <Modal
      title="Registrar repuesto"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Registrar"
      cancelText="Cancelar"
      confirmLoading={registrar.isPending}
      destroyOnHidden
    >
      <AvisoError error={registrar.error} />
      <Form<CamposRepuesto>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{ stockInicial: 0 }}
      >
        <Form.Item
          label="Nombre"
          name="nombre"
          rules={[{ required: true, whitespace: true, message: 'Ingresa el nombre del repuesto' }]}
        >
          <Input />
        </Form.Item>
        <div className="formulario-grid">
          <Form.Item label="Código" name="codigo" extra="Opcional: si se deja vacío se genera uno.">
            <Input style={{ textTransform: 'uppercase' }} />
          </Form.Item>
          <Form.Item label="Marca" name="marca">
            <AutoComplete
              options={(marcas.data ?? []).map((marca) => ({ value: marca }))}
              filterOption={(texto, opcion) => (opcion?.value ?? '').toLowerCase().includes(texto.toLowerCase())}
            />
          </Form.Item>
          <Form.Item
            label="Precio de venta (sin IGV)"
            name="precioVenta"
            rules={[{ required: true, message: 'Ingresa el precio' }]}
          >
            <InputNumber min={0} precision={2} style={{ width: '100%' }} />
          </Form.Item>
          {!sinStockInicial && (
            <Form.Item label="Stock inicial" name="stockInicial">
              <InputNumber min={0} precision={0} style={{ width: '100%' }} />
            </Form.Item>
          )}
        </div>
        <Form.Item label="¿El precio está en dólares?">
          <ConversorDolares onAplicar={(montoPen) => formulario.setFieldsValue({ precioVenta: montoPen })} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

type CamposServicio = {
  nombre: string
  precioSugerido: number
  tipoAfectacionIgv: number
}

/** Registro de un servicio del catálogo sin salir de la orden. */
export function ModalAltaRapidaServicio({
  abierto,
  onCerrar,
  onRegistrado,
}: Readonly<{ abierto: boolean; onCerrar: () => void; onRegistrado: (servicio: ServicioResponse) => void }>) {
  const [formulario] = Form.useForm<CamposServicio>()
  const registrar = useAltaRapidaServicio()

  const cerrar = () => {
    registrar.reset()
    onCerrar()
  }

  const enviar = async (campos: CamposServicio) => {
    const servicio = await registrar.mutateAsync({
      nombre: campos.nombre.trim(),
      precioSugerido: campos.precioSugerido,
      tipoAfectacionIgv: campos.tipoAfectacionIgv,
    })
    registrar.reset()
    onRegistrado(servicio)
  }

  return (
    <Modal
      title="Registrar servicio"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Registrar"
      cancelText="Cancelar"
      confirmLoading={registrar.isPending}
      destroyOnHidden
    >
      <AvisoError error={registrar.error} />
      <Form<CamposServicio>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{ tipoAfectacionIgv: TIPO_AFECTACION_IGV.gravado }}
      >
        <Form.Item
          label="Nombre"
          name="nombre"
          rules={[{ required: true, whitespace: true, message: 'Ingresa el nombre del servicio' }]}
        >
          <Input placeholder="Ej. Mantenimiento de 5 000 km" />
        </Form.Item>
        <div className="formulario-grid">
          <Form.Item
            label="Precio sugerido (sin IGV)"
            name="precioSugerido"
            rules={[{ required: true, message: 'Ingresa el precio' }]}
          >
            <InputNumber min={0} precision={2} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Afectación de IGV" name="tipoAfectacionIgv">
            <Select
              options={[
                { value: TIPO_AFECTACION_IGV.gravado, label: 'Gravado' },
                { value: TIPO_AFECTACION_IGV.exonerado, label: 'Exonerado' },
                { value: TIPO_AFECTACION_IGV.inafecto, label: 'Inafecto' },
              ]}
            />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}
