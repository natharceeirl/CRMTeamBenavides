import { Form, InputNumber, Modal, Table, type TableProps } from 'antd'
import { useProductos } from '../api/inventario'
import { useActualizarVenta } from '../api/ventas'
import type { DetalleVentaResponse, VentaDetalleResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { precioDistintoAlDeLista } from './AvisoPrecioGerencia'
import { soles } from '../utils/formato'

type Linea = { detalleId: string; productoId: string; cantidad: number; precioUnitario: number }
type Campos = { lineas: Linea[] }

/** Solo se editan cotizaciones de mostrador: todas sus líneas son productos. */
export const esCotizacionEditable = (venta: VentaDetalleResponse) =>
  venta.detalles.length > 0 && venta.detalles.every((detalle) => detalle.productoId) && (venta.pagos ?? []).length === 0

/**
 * Corrige cantidades y precios de una cotización. El backend reemplaza todas las
 * líneas, así que se envían todas, cambien o no.
 */
export function ModalEditarCotizacion({
  venta,
  onCerrar,
}: Readonly<{ venta: VentaDetalleResponse; onCerrar: () => void }>) {
  const [formulario] = Form.useForm<Campos>()
  const productos = useProductos()
  const actualizar = useActualizarVenta()
  const lineas = Form.useWatch('lineas', formulario) ?? []

  const precioDeLista = (productoId: string) =>
    (productos.data ?? []).find((producto) => producto.id === productoId)?.precioVenta
  const fueraDeLista = lineas.filter((linea) =>
    precioDistintoAlDeLista(linea?.precioUnitario, precioDeLista(linea?.productoId)),
  ).length

  const cerrar = () => {
    actualizar.reset()
    onCerrar()
  }

  const enviar = async ({ lineas: editadas }: Campos) => {
    await actualizar.mutateAsync({
      id: venta.id,
      datos: {
        detalles: editadas.map((linea) => ({
          productoId: linea.productoId,
          cantidad: linea.cantidad,
          precioUnitario: linea.precioUnitario,
        })),
        observaciones: null,
      },
    })
    cerrar()
  }

  const columnas: TableProps<DetalleVentaResponse & { indice: number }>['columns'] = [
    {
      title: 'Producto',
      key: 'producto',
      render: (_, detalle) => {
        const deLista = detalle.productoId ? precioDeLista(detalle.productoId) : undefined
        return (
          <>
            <div>{detalle.productoNombre}</div>
            {deLista != null && <div className="texto-secundario">De lista: {soles(deLista)}</div>}
          </>
        )
      },
    },
    {
      title: 'Cant.',
      key: 'cantidad',
      width: 110,
      render: (_, detalle) => (
        <Form.Item
          name={['lineas', detalle.indice, 'cantidad']}
          rules={[{ required: true, message: 'Cantidad' }]}
          style={{ marginBottom: 0 }}
        >
          <InputNumber min={1} precision={0} style={{ width: '100%' }} />
        </Form.Item>
      ),
    },
    {
      title: 'P. unit. (sin IGV)',
      key: 'precio',
      width: 160,
      render: (_, detalle) => (
        <Form.Item
          name={['lineas', detalle.indice, 'precioUnitario']}
          rules={[{ required: true, message: 'Precio' }]}
          style={{ marginBottom: 0 }}
        >
          <InputNumber min={0} step={0.5} prefix="S/" style={{ width: '100%' }} />
        </Form.Item>
      ),
    },
  ]

  return (
    <Modal
      title="Editar cotización"
      open
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={actualizar.isPending}
      width={720}
      destroyOnHidden
    >
      <AvisoError error={actualizar.error} />
      <Form<Campos>
        form={formulario}
        requiredMark={false}
        onFinish={enviar}
        initialValues={{
          lineas: venta.detalles.map((detalle) => ({
            detalleId: detalle.id,
            productoId: detalle.productoId ?? '',
            cantidad: detalle.cantidad,
            precioUnitario: detalle.precioUnitario,
          })),
        }}
      >
        <Table
          rowKey="id"
          columns={columnas}
          dataSource={venta.detalles.map((detalle, indice) => ({ ...detalle, indice }))}
          pagination={false}
        />
      </Form>
      {fueraDeLista > 0 && (
        <p className="aviso-precio" style={{ marginTop: 16 }}>
          {fueraDeLista === 1 ? 'Una línea tiene' : `${fueraDeLista} líneas tienen`} un precio distinto al de lista: la
          cotización queda pendiente hasta que Gerencia lo apruebe. Con todos los precios de lista, queda libre.
        </p>
      )}
    </Modal>
  )
}
