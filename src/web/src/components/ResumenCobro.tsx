import { Table, Tag, type TableProps } from 'antd'
import type { PagoResponse } from '../api/tipos'
import { fechaHora, importe, soles } from '../utils/formato'
import { EstadoPagoTag } from './EstadoPagoTag'

type Props = {
  total: number
  totalPagado: number
  saldo: number
  estadoPago: string | null | undefined
  pagos: PagoResponse[]
}

const columnas: TableProps<PagoResponse>['columns'] = [
  { title: 'Fecha', dataIndex: 'fecha', className: 'num', render: (fecha: string) => fechaHora(fecha) },
  {
    title: 'Método',
    key: 'metodo',
    render: (_, pago) => (
      <>
        <div>{pago.metodoPagoNombre}</div>
        {(pago.referencia || pago.usuarioNombre) && (
          <div className="texto-secundario">
            {[pago.referencia, pago.usuarioNombre].filter(Boolean).join(' · ')}
          </div>
        )}
      </>
    ),
  },
  {
    title: '',
    key: 'tipo',
    render: (_, pago) => (pago.esAnticipo ? <Tag style={{ marginInlineEnd: 0 }}>Adelanto</Tag> : null),
  },
  { title: 'Monto', dataIndex: 'monto', align: 'right', className: 'num', render: (monto: number) => importe(monto) },
]

/** Total, lo pagado, el saldo y los pagos de una venta u orden. */
export function ResumenCobro({ total, totalPagado, saldo, estadoPago, pagos }: Readonly<Props>) {
  return (
    <>
      <div className="totales">
        <div>
          <div className="etiqueta">Total</div>
          <div className="valor">{soles(total)}</div>
        </div>
        <div>
          <div className="etiqueta">Pagado</div>
          <div className="valor">{soles(totalPagado)}</div>
        </div>
        <div>
          <div className="etiqueta">Saldo</div>
          <div className="valor total">{soles(saldo)}</div>
        </div>
        <div>
          <div className="etiqueta">Estado</div>
          <div className="valor">
            <EstadoPagoTag estado={estadoPago} />
          </div>
        </div>
      </div>
      {pagos.length > 0 && (
        <Table
          rowKey="id"
          size="small"
          columns={columnas}
          dataSource={pagos}
          pagination={false}
          style={{ marginTop: 16 }}
        />
      )}
    </>
  )
}
