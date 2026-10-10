import { useState } from 'react'
import { Button, Input, Modal, Table, Tag, type TableProps } from 'antd'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { nombresTipoComprobanteCompra, useAnularCompra, useAnularPagoCompra, useCompra } from '../api/compras'
import {
  ESTADO_COMPRA,
  MONEDA_COMPRA,
  nombresTipoAfectacion,
  type CompraResponse,
  type DetalleCompraResponse,
  type PagoCompraResponse,
} from '../api/tipos'
import { montoEnMoneda, numeroComprobante } from '../utils/compras'
import { fechaDia, fechaHora, importe, soles } from '../utils/formato'
import { AvisoError } from './AvisoError'
import { EstadoCompraTag } from './EstadoCompraTag'
import { ModalPagoCompra } from './ModalPagoCompra'
import { ModalEditarCompra } from './ModalEditarCompra'

type Props = {
  compraId: string | null
  onCerrar: () => void
}

/** Lo que se confirma con motivo: anular un pago o la compra entera. */
type Anulacion = { tipo: 'pago'; pago: PagoCompraResponse } | { tipo: 'compra' }

function columnasLineas(compra: CompraResponse): TableProps<DetalleCompraResponse>['columns'] {
  return [
    {
      title: 'Repuesto o concepto',
      key: 'descripcion',
      render: (_, linea) => (
        <>
          <div>{linea.descripcion}</div>
          <div className="texto-secundario">
            {[
              linea.productoCodigo,
              linea.tipoAfectacionIgv !== 0 ? nombresTipoAfectacion[linea.tipoAfectacionIgv] : null,
              linea.productoId && !linea.mueveStock ? 'No suma stock' : null,
            ]
              .filter(Boolean)
              .join(' · ')}
          </div>
        </>
      ),
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
      title: 'Total',
      dataIndex: 'total',
      align: 'right',
      className: 'num',
      render: (total: number) => montoEnMoneda(compra.moneda, total),
    },
    {
      // Lo que vale cada unidad en el inventario: sin IGV con factura, en soles.
      title: 'Costo unit.',
      key: 'costo',
      align: 'right',
      className: 'num',
      render: (_, linea) => (linea.mueveStock ? soles(linea.costoUnitarioSoles) : '—'),
    },
  ]
}

export function ModalDetalleCompra({ compraId, onCerrar }: Readonly<Props>) {
  const { tienePermiso } = useSesion()
  const puedeRegistrar = tienePermiso(PERMISOS.comprasRegistrar)
  const puedeAnular = tienePermiso(PERMISOS.comprasAnular)

  const compra = useCompra(compraId)
  const anularPago = useAnularPagoCompra()
  const anularCompra = useAnularCompra()

  const [pagando, setPagando] = useState(false)
  const [editando, setEditando] = useState(false)
  const [anulacion, setAnulacion] = useState<Anulacion | null>(null)
  const [motivo, setMotivo] = useState('')

  const datos = compra.data
  const registrada = datos?.estado === ESTADO_COMPRA.registrada
  const enDolares = datos?.moneda === MONEDA_COMPRA.usd
  const pagosVigentes = datos?.pagos.filter((pago) => !pago.anulado) ?? []
  const devuelveEfectivo =
    anulacion?.tipo === 'pago' ? anulacion.pago.salioDeCaja : pagosVigentes.some((pago) => pago.salioDeCaja)

  const columnasPagos: TableProps<PagoCompraResponse>['columns'] = [
    { title: 'Fecha', dataIndex: 'fecha', className: 'num', render: (fecha: string) => fechaHora(fecha) },
    {
      title: 'Método',
      key: 'metodo',
      render: (_, pago) => (
        <>
          <div>
            {pago.metodoPagoNombre}
            {pago.salioDeCaja && <Tag style={{ marginInlineStart: 8 }}>De caja chica</Tag>}
          </div>
          {(pago.referencia || pago.usuarioNombre) && (
            <div className="texto-secundario">{[pago.referencia, pago.usuarioNombre].filter(Boolean).join(' · ')}</div>
          )}
          {pago.anulado && (
            <div className="texto-secundario">
              Anulado {fechaHora(pago.fechaAnulacion)}
              {pago.usuarioAnulacionNombre ? ` por ${pago.usuarioAnulacionNombre}` : ''}: {pago.motivoAnulacion}
            </div>
          )}
        </>
      ),
    },
    {
      title: 'Monto',
      key: 'monto',
      align: 'right',
      className: 'num',
      render: (_, pago) => (
        <span style={pago.anulado ? { textDecoration: 'line-through' } : undefined}>
          {datos ? montoEnMoneda(datos.moneda, pago.monto) : importe(pago.monto)}
          {enDolares && <div className="texto-secundario">{soles(pago.montoSoles)}</div>}
        </span>
      ),
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, pago) =>
        registrada && puedeAnular && !pago.anulado ? (
          <Button type="link" onClick={() => setAnulacion({ tipo: 'pago', pago })}>
            Anular
          </Button>
        ) : null,
    },
  ]

  const cerrarAnulacion = () => {
    setAnulacion(null)
    setMotivo('')
    anularPago.reset()
    anularCompra.reset()
  }

  const cerrar = () => {
    cerrarAnulacion()
    setPagando(false)
    setEditando(false)
    onCerrar()
  }

  const confirmarAnulacion = async () => {
    if (!datos || !anulacion) return
    if (anulacion.tipo === 'pago') {
      await anularPago.mutateAsync({ id: datos.id, pagoId: anulacion.pago.id, motivo: motivo.trim() })
    } else {
      await anularCompra.mutateAsync({ id: datos.id, motivo: motivo.trim() })
    }
    cerrarAnulacion()
  }

  return (
    <Modal
      title={datos ? `Compra ${datos.numeroCompra}` : 'Compra'}
      open={compraId !== null}
      onCancel={cerrar}
      onOk={cerrar}
      okText="Cerrar"
      cancelButtonProps={{ style: { display: 'none' } }}
      width={860}
      destroyOnHidden
    >
      <AvisoError error={compra.error} />
      {datos && (
        <>
          <table className="tabla-simple">
            <tbody>
              <tr>
                <td>Proveedor</td>
                <td>
                  {datos.proveedorNombre} · {datos.proveedorDocumento}
                </td>
              </tr>
              <tr>
                <td>Comprobante</td>
                <td>
                  {nombresTipoComprobanteCompra[datos.tipoComprobante]} {numeroComprobante(datos)}
                </td>
              </tr>
              <tr>
                <td>Emisión</td>
                <td>{fechaDia(datos.fechaEmision)}</td>
              </tr>
              <tr>
                <td>Vencimiento</td>
                <td>{fechaDia(datos.fechaVencimiento)}</td>
              </tr>
              <tr>
                <td>Moneda</td>
                <td>{enDolares ? `Dólares · tipo de cambio ${datos.tipoCambio}` : 'Soles'}</td>
              </tr>
              {datos.numeroPedidoLima && (
                <tr>
                  <td>Pedido a Lima</td>
                  <td>{datos.numeroPedidoLima} · su stock entra al recibir el pedido</td>
                </tr>
              )}
              {datos.guiaRemision && (
                <tr>
                  <td>Guía de remisión</td>
                  <td>{datos.guiaRemision}</td>
                </tr>
              )}
              <tr>
                <td>Registrada</td>
                <td>
                  {fechaHora(datos.fechaCreacion)}
                  {datos.usuarioNombre ? ` por ${datos.usuarioNombre}` : ''}
                </td>
              </tr>
              <tr>
                <td>Estado</td>
                <td>
                  <EstadoCompraTag compra={datos} />
                </td>
              </tr>
              {datos.estado === ESTADO_COMPRA.anulada && (
                <tr>
                  <td>Anulación</td>
                  <td>
                    {fechaHora(datos.fechaAnulacion)}
                    {datos.usuarioAnulacionNombre ? ` por ${datos.usuarioAnulacionNombre}` : ''}: {datos.motivoAnulacion}
                  </td>
                </tr>
              )}
              {datos.observaciones && (
                <tr>
                  <td>Observaciones</td>
                  <td>{datos.observaciones}</td>
                </tr>
              )}
            </tbody>
          </table>

          <Table
            rowKey="id"
            columns={columnasLineas(datos)}
            dataSource={datos.detalles}
            pagination={false}
            scroll={{ x: 'max-content' }}
            style={{ marginTop: 16 }}
          />
          <div className="totales">
            <div>
              <div className="etiqueta">Op. gravadas</div>
              <div className="valor">{montoEnMoneda(datos.moneda, datos.subtotalGravado)}</div>
            </div>
            {datos.subtotalExonerado > 0 && (
              <div>
                <div className="etiqueta">Exoneradas</div>
                <div className="valor">{montoEnMoneda(datos.moneda, datos.subtotalExonerado)}</div>
              </div>
            )}
            {datos.subtotalInafecto > 0 && (
              <div>
                <div className="etiqueta">Inafectas</div>
                <div className="valor">{montoEnMoneda(datos.moneda, datos.subtotalInafecto)}</div>
              </div>
            )}
            <div>
              <div className="etiqueta">IGV {datos.porcentajeIgv} %</div>
              <div className="valor">{montoEnMoneda(datos.moneda, datos.montoIgv)}</div>
            </div>
            <div>
              <div className="etiqueta">Total</div>
              <div className="valor total">{montoEnMoneda(datos.moneda, datos.total)}</div>
            </div>
            {enDolares && (
              <div>
                <div className="etiqueta">Total en soles</div>
                <div className="valor">{soles(datos.totalSoles)}</div>
              </div>
            )}
          </div>
          {datos.preciosIncluyenIgv && (
            <p className="texto-secundario" style={{ marginTop: 8 }}>
              Los precios unitarios se registraron con IGV.
            </p>
          )}

          <section style={{ marginTop: 28 }}>
            <div className="seccion-titulo">
              <h2>Pagos al proveedor</h2>
              {registrada && puedeRegistrar && datos.saldo > 0 && (
                <div className="acciones">
                  <Button type="primary" onClick={() => setPagando(true)}>
                    Registrar pago
                  </Button>
                </div>
              )}
            </div>
            {registrada && (
              <div className="totales" style={{ marginTop: 0, paddingTop: 0, borderTop: 0 }}>
                <div>
                  <div className="etiqueta">Pagado</div>
                  <div className="valor">{montoEnMoneda(datos.moneda, datos.totalPagado)}</div>
                </div>
                <div>
                  <div className="etiqueta">Saldo</div>
                  <div className="valor total">{montoEnMoneda(datos.moneda, datos.saldo)}</div>
                </div>
              </div>
            )}
            {datos.pagos.length > 0 ? (
              <Table
                rowKey="id"
                size="small"
                columns={columnasPagos}
                dataSource={datos.pagos}
                pagination={false}
                scroll={{ x: 'max-content' }}
                style={{ marginTop: 16 }}
              />
            ) : (
              <p className="texto-secundario" style={{ marginTop: 12 }}>
                Todavía no hay pagos.
              </p>
            )}
          </section>

          {registrada && (puedeRegistrar || puedeAnular) && (
            <section style={{ marginTop: 28 }}>
              <div className="seccion-titulo">
                <h2>Corregir</h2>
                <div className="acciones">
                  {puedeRegistrar && <Button onClick={() => setEditando(true)}>Editar datos del comprobante</Button>}
                  {puedeAnular && <Button onClick={() => setAnulacion({ tipo: 'compra' })}>Anular compra</Button>}
                </div>
              </div>
              <p className="texto-secundario">
                Si un repuesto, la cantidad, el precio o la moneda están mal, anula la compra y regístrala de nuevo.
              </p>
            </section>
          )}
        </>
      )}

      <ModalPagoCompra compra={pagando && datos ? datos : null} onCerrar={() => setPagando(false)} />
      <ModalEditarCompra compra={editando && datos ? datos : null} onCerrar={() => setEditando(false)} />

      <Modal
        title={anulacion?.tipo === 'pago' ? 'Anular pago' : 'Anular compra'}
        open={anulacion !== null}
        onCancel={cerrarAnulacion}
        onOk={confirmarAnulacion}
        okText={anulacion?.tipo === 'pago' ? 'Anular pago' : 'Anular compra'}
        cancelText="Volver"
        okButtonProps={{ danger: true, disabled: !motivo.trim() }}
        confirmLoading={anularPago.isPending || anularCompra.isPending}
        destroyOnHidden
      >
        <AvisoError error={anularPago.error ?? anularCompra.error} />
        {anulacion?.tipo === 'compra' && (
          <p>
            Se saca del stock lo que entró con esta compra, el costo de cada repuesto vuelve al anterior si nadie lo
            cambió después, y se anulan sus pagos.
          </p>
        )}
        {devuelveEfectivo && (
          <p>Lo pagado en efectivo vuelve como ingreso a la caja chica abierta; tiene que haber una abierta.</p>
        )}
        <Input.TextArea
          rows={3}
          maxLength={500}
          value={motivo}
          onChange={(evento) => setMotivo(evento.target.value)}
          placeholder="Motivo de la anulación"
        />
      </Modal>
    </Modal>
  )
}
