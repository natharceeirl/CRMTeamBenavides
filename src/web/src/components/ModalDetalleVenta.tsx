import { Modal, Table, type TableProps } from 'antd'
import { useVenta } from '../api/ventas'
import type { DetalleVentaResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { fechaHora, importe, referenciaOrden, soles } from '../utils/formato'

type Props = {
  abierto: boolean
  ventaId?: string | null
  onCerrar: () => void
}

const columnas: TableProps<DetalleVentaResponse>['columns'] = [
  {
    title: 'Producto',
    key: 'producto',
    render: (_, detalle) => `${detalle.productoCodigo} · ${detalle.productoNombre}`,
  },
  { title: 'Cant.', dataIndex: 'cantidad', align: 'right', className: 'num' },
  {
    title: 'P. unit.',
    dataIndex: 'precioUnitario',
    align: 'right',
    className: 'num',
    render: (precio: number) => importe(precio),
  },
  {
    title: 'Importe',
    dataIndex: 'subtotal',
    align: 'right',
    className: 'num',
    render: (subtotal: number) => importe(subtotal),
  },
]

export function ModalDetalleVenta({ abierto, ventaId, onCerrar }: Readonly<Props>) {
  const venta = useVenta(abierto ? (ventaId ?? undefined) : undefined)
  const datos = venta.data

  return (
    <Modal
      title={ventaId ? `Venta ${referenciaOrden(ventaId)}` : 'Venta'}
      open={abierto}
      onCancel={onCerrar}
      onOk={onCerrar}
      okText="Cerrar"
      cancelButtonProps={{ style: { display: 'none' } }}
      width={720}
      destroyOnHidden
    >
      <AvisoError error={venta.error} />
      {datos && (
        <>
          <table className="tabla-simple">
            <tbody>
              <tr>
                <td>Cliente</td>
                <td>{datos.clienteNombre}</td>
              </tr>
              <tr>
                <td>Documento</td>
                <td>{datos.clienteDocumento ?? '—'}</td>
              </tr>
              <tr>
                <td>Teléfono</td>
                <td>{datos.clienteTelefono ?? '—'}</td>
              </tr>
              <tr>
                <td>Fecha</td>
                <td>{fechaHora(datos.fecha)}</td>
              </tr>
              <tr>
                <td>Estado</td>
                <td>{datos.estado}</td>
              </tr>
              <tr>
                <td>Orden de servicio</td>
                <td>{datos.ordenServicioId ? referenciaOrden(datos.ordenServicioId) : '—'}</td>
              </tr>
            </tbody>
          </table>
          <Table
            rowKey="id"
            columns={columnas}
            dataSource={datos.detalles}
            pagination={false}
            loading={venta.isPending}
            style={{ marginTop: 16 }}
          />
          <div className="totales">
            <div>
              <div className="etiqueta">Total</div>
              <div className="valor total">{soles(datos.total)}</div>
            </div>
          </div>
        </>
      )}
    </Modal>
  )
}
