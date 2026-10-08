import { useState } from 'react'
import { Button, Form, InputNumber, Modal, Radio, Select, Space } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useClientes } from '../api/clientes'
import { useProductos } from '../api/inventario'
import { useCrearVenta } from '../api/ventas'
import type { VentaDetalleResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { precioDistintoAlDeLista } from './AvisoPrecioGerencia'
import { ModalAltaRapidaRepuesto } from './ModalesAltaRapida'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { soles } from '../utils/formato'

type Props = {
  abierto: boolean
  onCerrar: () => void
  /** Recibe la venta creada, para abrirla y cobrarla. */
  onCreada?: (venta: VentaDetalleResponse) => void
}

type Linea = {
  productoId?: string
  cantidad?: number
  precioUnitario?: number | null
}

type Campos = {
  clienteId: string
  esCotizacion: boolean
  detalles: Linea[]
}

export function ModalVenta({ abierto, onCerrar, onCreada }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const clientes = useClientes()
  const productos = useProductos()
  const crear = useCrearVenta()
  const { tienePermiso } = useSesion()
  const puedeRegistrarRepuesto = tienePermiso(PERMISOS.inventarioCrear)
  const [altaRepuesto, setAltaRepuesto] = useState(false)

  const lineas = Form.useWatch('detalles', formulario) ?? []
  const esCotizacion = Form.useWatch('esCotizacion', formulario) ?? true
  const precioDeLista = (productoId?: string) =>
    (productos.data ?? []).find((item) => item.id === productoId)?.precioVenta

  // Solo informativo: el precio de verdad y el IGV los pone el backend.
  const totalEstimado = lineas.reduce((suma, linea) => {
    const precio = linea?.precioUnitario ?? precioDeLista(linea?.productoId) ?? 0
    return suma + precio * (linea?.cantidad ?? 0)
  }, 0)
  const lineasFueraDeLista = lineas.filter((linea) =>
    precioDistintoAlDeLista(linea?.precioUnitario, precioDeLista(linea?.productoId)),
  ).length

  const cerrar = () => {
    crear.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    const venta = await crear.mutateAsync({
      clienteId: campos.clienteId,
      ordenServicioId: null,
      esCotizacion: campos.esCotizacion,
      detalles: campos.detalles.map((linea) => ({
        productoId: linea.productoId!,
        cantidad: linea.cantidad ?? 1,
        // Sin precio, o con el de lista, el backend usa el de lista y no pide aprobación.
        precioUnitario: linea.precioUnitario ?? null,
      })),
    })

    cerrar()
    onCreada?.(venta)
  }

  return (
    <Modal
      title="Nueva venta o cotización"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={crear.isPending}
      width={720}
      destroyOnHidden
    >
      <AvisoError error={crear.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{ esCotizacion: true, detalles: [{}] }}
      >
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

        <Form.Item label="Tipo" name="esCotizacion">
          <Radio.Group
            options={[
              { value: true, label: 'Cotización' },
              { value: false, label: 'Venta directa' },
            ]}
            optionType="button"
          />
        </Form.Item>
        <p className="texto-secundario">
          La cotización no toca el stock. La venta directa lo descuenta al guardarse, igual que al confirmar una
          cotización.
        </p>

        <Form.List name="detalles">
          {(campos, { add, remove }) => (
            <>
              {campos.map((campo) => (
                <Space key={campo.key} align="baseline" style={{ display: 'flex', marginBottom: 8 }}>
                  <Form.Item
                    name={[campo.name, 'productoId']}
                    rules={[{ required: true, message: 'Elige el producto' }]}
                    style={{ width: 360, marginBottom: 0 }}
                  >
                    <Select
                      showSearch
                      optionFilterProp="label"
                      loading={productos.isPending}
                      placeholder="Producto"
                      onChange={(productoId: string) =>
                        formulario.setFieldValue(['detalles', campo.name, 'precioUnitario'], precioDeLista(productoId))
                      }
                      options={(productos.data ?? []).map((producto) => ({
                        value: producto.id,
                        label: `${producto.codigo} · ${producto.nombre} · ${soles(producto.precioVenta)} · stock ${producto.stockActual}`,
                      }))}
                    />
                  </Form.Item>
                  <Form.Item
                    name={[campo.name, 'cantidad']}
                    initialValue={1}
                    rules={[{ required: true, message: 'Cantidad' }]}
                    style={{ marginBottom: 0 }}
                  >
                    <InputNumber min={1} placeholder="Cant." />
                  </Form.Item>
                  <Form.Item name={[campo.name, 'precioUnitario']} style={{ marginBottom: 0 }}>
                    <InputNumber min={0} step={0.5} placeholder="Precio" prefix="S/" style={{ width: 130 }} />
                  </Form.Item>
                  {campos.length > 1 && (
                    <Button type="text" icon={<DeleteOutlined />} onClick={() => remove(campo.name)} />
                  )}
                </Space>
              ))}
              <Button type="dashed" onClick={() => add()} icon={<PlusOutlined />} block>
                Agregar producto
              </Button>
              {puedeRegistrarRepuesto && (
                <Button type="link" size="small" style={{ paddingInline: 0, marginTop: 4 }} onClick={() => setAltaRepuesto(true)}>
                  ¿No está en el catálogo? Registrar repuesto
                </Button>
              )}
            </>
          )}
        </Form.List>

        {lineasFueraDeLista > 0 && (
          <p className="aviso-precio" style={{ marginTop: 16 }}>
            {lineasFueraDeLista === 1 ? 'Una línea tiene' : `${lineasFueraDeLista} líneas tienen`} un precio distinto al
            de lista: la venta queda pendiente hasta que Gerencia lo apruebe.
            {!esCotizacion && ' Guárdala como cotización para poder corregir el precio si Gerencia lo rechaza.'}
          </p>
        )}

        <div className="totales" style={{ marginTop: 16 }}>
          <div>
            <div className="etiqueta">Subtotal estimado, sin IGV</div>
            <div className="valor total">{soles(totalEstimado)}</div>
          </div>
        </div>
      </Form>

      <ModalAltaRapidaRepuesto
        abierto={altaRepuesto}
        onCerrar={() => setAltaRepuesto(false)}
        onRegistrado={(repuesto) => {
          setAltaRepuesto(false)
          // Ocupa la primera línea vacía o agrega una nueva con el repuesto registrado.
          const actuales: Linea[] = formulario.getFieldValue('detalles') ?? []
          const vacia = actuales.findIndex((linea) => !linea?.productoId)
          const nueva = { productoId: repuesto.id, cantidad: 1, precioUnitario: repuesto.precioVenta }
          formulario.setFieldValue(
            'detalles',
            vacia === -1 ? [...actuales, nueva] : actuales.map((linea, indice) => (indice === vacia ? nueva : linea)),
          )
        }}
      />
    </Modal>
  )
}
