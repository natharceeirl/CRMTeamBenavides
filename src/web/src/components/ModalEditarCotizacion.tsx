import { Form, InputNumber, Modal, Table, type TableProps } from 'antd'
import { useProductos } from '../api/inventario'
import { ESTADO_VENTA, useActualizarVenta } from '../api/ventas'
import { GERENCIA } from '../api/ordenes'
import type { DetalleVentaResponse, VentaDetalleResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { precioDistintoAlDeLista } from './AvisoPrecioGerencia'
import { soles } from '../utils/formato'

type Linea = { detalleId: string; productoId: string; cantidad: number; precioUnitario: number }
type Campos = { lineas: Linea[] }

/**
 * Se editan las ventas de mostrador (todas sus líneas son productos) sin pagos ni
 * comprobante: una cotización, o una venta confirmada cuyo precio sigue esperando
 * a Gerencia o fue rechazado.
 */
export const esVentaEditable = (venta: VentaDetalleResponse) => {
  const lineasDeMostrador = venta.detalles.length > 0 && venta.detalles.every((detalle) => detalle.productoId)
  const sinCobroNiComprobante = (venta.pagos ?? []).length === 0 && !venta.comprobante
  const aprobacion = venta.estadoAprobacionGerenciaId ?? GERENCIA.noAplica
  const confirmadaPorCorregir =
    venta.estadoId === ESTADO_VENTA.confirmada &&
    (aprobacion === GERENCIA.pendiente || aprobacion === GERENCIA.rechazado)
  return (
    lineasDeMostrador && sinCobroNiComprobante && (venta.estadoId === ESTADO_VENTA.cotizacion || confirmadaPorCorregir)
  )
}

/**
 * Corrige cantidades y precios de una cotización o de una venta confirmada con el
 * precio por corregir. El backend reemplaza todas las líneas, así que se envían
 * todas, cambien o no; en una venta confirmada también ajusta el stock.
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

  const confirmada = venta.estadoId === ESTADO_VENTA.confirmada
  const operacion = confirmada ? 'venta' : 'cotización'

  return (
    <Modal
      title={confirmada ? 'Corregir la venta' : 'Editar cotización'}
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
      {confirmada && (
        <p className="texto-secundario" style={{ marginTop: 16 }}>
          La venta ya descontó el stock: si cambias una cantidad, el stock se ajusta a la diferencia.
        </p>
      )}
      {fueraDeLista > 0 && (
        <p className="aviso-precio" style={{ marginTop: 16 }}>
          {fueraDeLista === 1 ? 'Una línea tiene' : `${fueraDeLista} líneas tienen`} un precio distinto al de lista: la{' '}
          {operacion} queda pendiente hasta que Gerencia lo apruebe. Con todos los precios de lista, queda libre.
        </p>
      )}
    </Modal>
  )
}
