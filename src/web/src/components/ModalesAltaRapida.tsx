import { AutoComplete, Form, Input, InputNumber, Modal, Select } from 'antd'
import { useAltaRapidaCliente } from '../api/clientes'
import { useAltaRapidaVehiculo } from '../api/vehiculos'
import { useAltaRapidaProducto, useMarcasProductos } from '../api/inventario'
import { useAltaRapidaServicio } from '../api/servicios'
import {
  TIPO_AFECTACION_IGV,
  TIPO_DOCUMENTO_CLIENTE,
  type ClienteResponse,
  type ProductoResponse,
  type ServicioResponse,
  type VehiculoResponse,
} from '../api/tipos'
import { AvisoError } from './AvisoError'
import { ConversorDolares } from './ConversorDolares'

const sinVacios = (valor?: string) => (valor?.trim() ? valor.trim() : null)

type CamposCliente = {
  tipoDocumento: number
  numeroDocumento: string
  nombreCompleto: string
  telefono?: string
  email?: string
}

const LARGO_DOCUMENTO: Record<number, number> = {
  [TIPO_DOCUMENTO_CLIENTE.dni]: 8,
  [TIPO_DOCUMENTO_CLIENTE.ruc]: 11,
}

/** Registro mínimo de un cliente desde recepción. Si el documento ya existe, el backend devuelve ese cliente. */
export function ModalAltaRapidaCliente({
  abierto,
  numeroDocumento,
  onCerrar,
  onRegistrado,
}: Readonly<{
  abierto: boolean
  numeroDocumento: string
  onCerrar: () => void
  onRegistrado: (cliente: ClienteResponse) => void
}>) {
  const [formulario] = Form.useForm<CamposCliente>()
  const registrar = useAltaRapidaCliente()

  const cerrar = () => {
    registrar.reset()
    onCerrar()
  }

  const enviar = async (campos: CamposCliente) => {
    const cliente = await registrar.mutateAsync({
      tipoDocumento: campos.tipoDocumento,
      numeroDocumento: campos.numeroDocumento.trim(),
      nombreCompleto: campos.nombreCompleto.trim(),
      telefono: sinVacios(campos.telefono),
      email: sinVacios(campos.email),
      direccion: null,
    })
    registrar.reset()
    onRegistrado(cliente)
  }

  return (
    <Modal
      title="Registrar cliente"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Registrar"
      cancelText="Cancelar"
      confirmLoading={registrar.isPending}
      destroyOnHidden
    >
      <AvisoError error={registrar.error} />
      <Form<CamposCliente>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{
          tipoDocumento: numeroDocumento.length === 11 ? TIPO_DOCUMENTO_CLIENTE.ruc : TIPO_DOCUMENTO_CLIENTE.dni,
          numeroDocumento,
        }}
      >
        <div className="formulario-grid">
          <Form.Item label="Tipo de documento" name="tipoDocumento">
            <Select
              options={[
                { value: TIPO_DOCUMENTO_CLIENTE.dni, label: 'DNI' },
                { value: TIPO_DOCUMENTO_CLIENTE.ruc, label: 'RUC' },
                { value: TIPO_DOCUMENTO_CLIENTE.otro, label: 'Otro' },
              ]}
            />
          </Form.Item>
          <Form.Item
            label="Número de documento"
            name="numeroDocumento"
            dependencies={['tipoDocumento']}
            rules={[
              { required: true, whitespace: true, message: 'Ingresa el documento' },
              ({ getFieldValue }) => ({
                validator: async (_, valor: string | undefined) => {
                  const largo = LARGO_DOCUMENTO[getFieldValue('tipoDocumento') as number]
                  if (valor?.trim() && largo && !new RegExp(`^\\d{${largo}}$`).test(valor.trim())) {
                    throw new Error(`Debe tener ${largo} dígitos`)
                  }
                },
              }),
            ]}
          >
            <Input />
          </Form.Item>
        </div>
        <Form.Item
          label="Nombre completo"
          name="nombreCompleto"
          rules={[{ required: true, whitespace: true, message: 'Ingresa el nombre del cliente' }]}
        >
          <Input placeholder="Nombre y apellidos, o razón social" />
        </Form.Item>
        <div className="formulario-grid">
          <Form.Item label="Teléfono" name="telefono">
            <Input />
          </Form.Item>
          <Form.Item label="Correo" name="email" rules={[{ type: 'email', message: 'Correo inválido' }]}>
            <Input />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}

type CamposUnidad = {
  placa?: string
  marca: string
  modelo: string
  kilometraje?: number | null
  color?: string
}

/**
 * Registro mínimo de una unidad con kilometraje. Las que van por horas (náutica,
 * generadores) o sin placa se registran completas desde Unidades.
 */
export function ModalAltaRapidaUnidad({
  abierto,
  cliente,
  onCerrar,
  onRegistrada,
}: Readonly<{
  abierto: boolean
  cliente: Pick<ClienteResponse, 'id' | 'nombreCompleto'>
  onCerrar: () => void
  onRegistrada: (unidad: VehiculoResponse) => void
}>) {
  const [formulario] = Form.useForm<CamposUnidad>()
  const registrar = useAltaRapidaVehiculo()

  const cerrar = () => {
    registrar.reset()
    onCerrar()
  }

  const enviar = async (campos: CamposUnidad) => {
    const unidad = await registrar.mutateAsync({
      clienteId: cliente.id,
      placa: sinVacios(campos.placa)?.toUpperCase() ?? null,
      marca: campos.marca.trim(),
      modelo: campos.modelo.trim(),
      kilometraje: campos.kilometraje == null ? null : Math.round(campos.kilometraje),
      color: sinVacios(campos.color),
    })
    registrar.reset()
    onRegistrada(unidad)
  }

  return (
    <Modal
      title={`Registrar unidad de ${cliente.nombreCompleto}`}
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Registrar"
      cancelText="Cancelar"
      confirmLoading={registrar.isPending}
      destroyOnHidden
    >
      <AvisoError error={registrar.error} />
      <Form<CamposUnidad>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{ marca: 'Yamaha' }}
      >
        <div className="formulario-grid">
          <Form.Item label="Placa" name="placa">
            <Input placeholder="Opcional" style={{ textTransform: 'uppercase' }} />
          </Form.Item>
          <Form.Item label="Kilometraje" name="kilometraje">
            <InputNumber min={0} precision={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            label="Marca"
            name="marca"
            rules={[{ required: true, whitespace: true, message: 'Ingresa la marca' }]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            label="Modelo"
            name="modelo"
            rules={[{ required: true, whitespace: true, message: 'Ingresa el modelo' }]}
          >
            <Input />
          </Form.Item>
          <Form.Item label="Color" name="color">
            <Input />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}

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
