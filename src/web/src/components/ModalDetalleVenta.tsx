import { Button, Form, Input, Modal, Popconfirm, Select, Space, Table, Tag, type TableProps } from 'antd'
import { useState } from 'react'
import { useSesion } from '../auth/sesion'
import { ACCESO_ORDENES, PERMISOS, cumpleAcceso } from '../auth/acceso'
import { useOrden } from '../api/ordenes'
import {
  ESTADO_VENTA,
  nombresEstadoVenta,
  puedeAnularComprobante,
  puedeRegistrarComprobante,
  useAnularComprobante,
  useRegistrarComprobante,
  useVenta,
} from '../api/ventas'
import { tieneSaldo, useMetodosPago, useRegistrarPagoVenta } from '../api/pagos'
import { useConfiguracionEmpresa } from '../api/configuracion'
import { nombresTipoItem, type DetalleVentaResponse, type RegistrarComprobanteRequest } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { ModalRegistrarPago } from './ModalRegistrarPago'
import { ResumenCobro } from './ResumenCobro'
import { colores } from '../theme/tokens'
import { fechaHora, importe, referenciaOrden, soles } from '../utils/formato'
import { htmlComprobante } from '../utils/comprobante'
import { abrirDocumento } from '../utils/impresion'

type Props = {
  abierto: boolean
  ventaId?: string | null
  onCerrar: () => void
}

const columnas: TableProps<DetalleVentaResponse>['columns'] = [
  {
    title: 'Concepto',
    key: 'concepto',
    render: (_, detalle) => {
      const tipo = nombresTipoItem[detalle.tipoItem ?? 0] ?? detalle.tipoItemNombre
      return (
        <>
          <div>{detalle.productoNombre}</div>
          <div className="texto-secundario">{[tipo, detalle.productoCodigo].filter(Boolean).join(' · ')}</div>
        </>
      )
    },
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
    title: 'IGV',
    dataIndex: 'montoIgv',
    align: 'right',
    className: 'num',
    render: (igv: number | undefined) => importe(igv ?? 0),
  },
  {
    title: 'Total',
    key: 'total',
    align: 'right',
    className: 'num',
    render: (_, detalle) => importe(detalle.total ?? detalle.subtotal),
  },
]

/** Catálogo cerrado: el tipo de comprobante no se escribe a mano. */
const TIPOS_COMPROBANTE = [
  { value: 'Boleta', label: 'Boleta' },
  { value: 'Factura', label: 'Factura' },
]

export function ModalDetalleVenta({ abierto, ventaId, onCerrar }: Readonly<Props>) {
  const sesion = useSesion()
  const { tienePermiso } = sesion
  const puedeRegistrar = tienePermiso(PERMISOS.ventasCrear)
  const puedeAnular = tienePermiso(PERMISOS.ventasAnular)
  const [mostrarForm, setMostrarForm] = useState(false)
  const [modalPago, setModalPago] = useState(false)
  const [errorImpresion, setErrorImpresion] = useState<unknown>(null)
  const [formulario] = Form.useForm<RegistrarComprobanteRequest>()
  const venta = useVenta(abierto ? (ventaId ?? undefined) : undefined)
  const empresa = useConfiguracionEmpresa()
  const metodos = useMetodosPago(abierto && mostrarForm)
  const registrar = useRegistrarComprobante()
  const anular = useAnularComprobante()
  const pagar = useRegistrarPagoVenta()
  const datos = venta.data
  // La venta solo trae el id de la orden: el número (OS-000024) se consulta
  // aparte, y solo si el usuario puede ver órdenes.
  const orden = useOrden(
    datos?.ordenServicioId && cumpleAcceso(ACCESO_ORDENES, sesion) ? datos.ordenServicioId : undefined,
  )
  const numeroOrden = orden.data?.numeroOrden ?? null

  const handleCerrar = () => {
    setMostrarForm(false)
    setErrorImpresion(null)
    formulario.resetFields()
    registrar.reset()
    anular.reset()
    pagar.reset()
    onCerrar()
  }

  const handleRegistrar = async (valores: RegistrarComprobanteRequest) => {
    if (!ventaId) return
    await registrar.mutateAsync({
      ventaId,
      datos: {
        tipo: valores.tipo.trim(),
        serie: valores.serie?.trim() || null,
        numero: valores.numero?.trim() || null,
        metodoPagoPrincipal: valores.metodoPagoPrincipal || null,
        observaciones: valores.observaciones?.trim() || null,
      },
    })
    setMostrarForm(false)
    formulario.resetFields()
  }

  const handleAnular = async () => {
    if (!ventaId) return
    await anular.mutateAsync(ventaId)
  }

  const handleImprimir = () => {
    if (!datos) return
    setErrorImpresion(null)
    abrirDocumento(async () => htmlComprobante(datos, empresa.data, numeroOrden)).catch(setErrorImpresion)
  }

  const confirmada = datos?.estadoId === ESTADO_VENTA.confirmada
  const puedeCobrar = Boolean(datos) && confirmada && puedeRegistrar && tieneSaldo(datos!)

  return (
    <Modal
      title={ventaId ? `Venta ${referenciaOrden(ventaId)}` : 'Venta'}
      open={abierto}
      onCancel={handleCerrar}
      onOk={handleCerrar}
      okText="Cerrar"
      cancelButtonProps={{ style: { display: 'none' } }}
      width={820}
      destroyOnHidden
    >
      <AvisoError error={venta.error ?? anular.error ?? errorImpresion} />
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
                <td>{nombresEstadoVenta[datos.estadoId] ?? datos.estado}</td>
              </tr>
              <tr>
                <td>Orden de servicio</td>
                <td>{datos.ordenServicioId ? (numeroOrden ?? referenciaOrden(datos.ordenServicioId)) : '—'}</td>
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
              <div className="etiqueta">Op. gravadas</div>
              <div className="valor">{soles(datos.subtotalGravado)}</div>
            </div>
            {datos.subtotalExonerado > 0 && (
              <div>
                <div className="etiqueta">Exoneradas</div>
                <div className="valor">{soles(datos.subtotalExonerado)}</div>
              </div>
            )}
            {datos.subtotalInafecto > 0 && (
              <div>
                <div className="etiqueta">Inafectas</div>
                <div className="valor">{soles(datos.subtotalInafecto)}</div>
              </div>
            )}
            <div>
              <div className="etiqueta">IGV</div>
              <div className="valor">{soles(datos.montoIgv)}</div>
            </div>
            <div>
              <div className="etiqueta">Total</div>
              <div className="valor total">{soles(datos.total)}</div>
            </div>
          </div>

          {confirmada && (
            <section style={{ marginTop: 28 }}>
              <div className="seccion-titulo">
                <h2>Cobro</h2>
                {puedeCobrar && (
                  <div className="acciones">
                    <Button type="primary" onClick={() => setModalPago(true)}>
                      Registrar pago
                    </Button>
                  </div>
                )}
              </div>
              <ResumenCobro
                total={datos.total}
                totalPagado={datos.totalPagado}
                saldo={datos.saldo}
                estadoPago={datos.estadoPago}
                pagos={datos.pagos ?? []}
              />
            </section>
          )}

          <section style={{ marginTop: 28 }}>
            <div className="seccion-titulo">
              <h2>Comprobante</h2>
              <div className="acciones">
                {datos.comprobante && <Button onClick={handleImprimir}>Ver e imprimir</Button>}
                {!datos.comprobante &&
                  puedeRegistrar &&
                  !mostrarForm &&
                  puedeRegistrarComprobante(datos.estadoId, false) && (
                    <Button type="primary" onClick={() => setMostrarForm(true)}>
                      Registrar comprobante
                    </Button>
                  )}
              </div>
            </div>

            {datos.comprobante ? (
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
                <div>
                  <strong>{datos.comprobante.tipo}</strong>
                  {(datos.comprobante.serie || datos.comprobante.numero) && (
                    <span> {[datos.comprobante.serie, datos.comprobante.numero].filter(Boolean).join('-')}</span>
                  )}{' '}
                  <Tag
                    style={{
                      marginInlineEnd: 0,
                      background: puedeAnularComprobante(datos.comprobante.estado) ? colores.acento600 : 'transparent',
                      color: puedeAnularComprobante(datos.comprobante.estado) ? colores.blanco : colores.textoSecundario,
                      borderColor: puedeAnularComprobante(datos.comprobante.estado) ? colores.acento600 : colores.neutro300,
                    }}
                  >
                    {datos.comprobante.estado}
                  </Tag>
                  {datos.comprobante.metodoPagoPrincipal && (
                    <div className="texto-secundario">Pago: {datos.comprobante.metodoPagoPrincipal}</div>
                  )}
                </div>
                {puedeAnular && puedeAnularComprobante(datos.comprobante.estado) && (
                  <Popconfirm
                    title="¿Anular comprobante?"
                    description="El comprobante quedará registrado administrativamente como Anulado."
                    onConfirm={handleAnular}
                    okText="Sí, anular"
                    cancelText="Cancelar"
                    okButtonProps={{ danger: true, loading: anular.isPending }}
                  >
                    <Button danger type="link" loading={anular.isPending}>
                      Anular comprobante
                    </Button>
                  </Popconfirm>
                )}
              </div>
            ) : (
              !mostrarForm && (
                <p className="texto-secundario">
                  {confirmada ? 'Sin comprobante registrado.' : 'Solo una venta confirmada lleva comprobante.'}
                </p>
              )
            )}

            {mostrarForm && puedeRegistrarComprobante(datos.estadoId, Boolean(datos.comprobante)) && (
              <div
                style={{
                  marginTop: 8,
                  padding: 16,
                  background: colores.neutro100,
                  border: `1px solid ${colores.neutro300}`,
                }}
              >
                <AvisoError error={registrar.error} />
                <Form form={formulario} layout="vertical" requiredMark={false} onFinish={handleRegistrar}>
                  <div className="formulario-grid">
                    <Form.Item
                      label="Tipo de comprobante"
                      name="tipo"
                      rules={[{ required: true, message: 'Elige el tipo de comprobante' }]}
                    >
                      <Select placeholder="Boleta o factura" options={TIPOS_COMPROBANTE} />
                    </Form.Item>
                    <Form.Item label="Método de pago principal" name="metodoPagoPrincipal">
                      <Select
                        allowClear
                        loading={metodos.isPending}
                        placeholder="El del pago más grande"
                        options={(metodos.data ?? []).map((metodo) => ({ value: metodo.nombre, label: metodo.nombre }))}
                      />
                    </Form.Item>
                    <Form.Item label="Serie" name="serie">
                      <Input placeholder="Ej. B001" maxLength={10} />
                    </Form.Item>
                    <Form.Item label="Número" name="numero">
                      <Input placeholder="Ej. 000124" maxLength={20} />
                    </Form.Item>
                    <Form.Item label="Observaciones" name="observaciones" className="ancho-completo">
                      <Input.TextArea rows={2} maxLength={500} />
                    </Form.Item>
                  </div>
                  <Space>
                    <Button type="primary" htmlType="submit" loading={registrar.isPending}>
                      Guardar comprobante
                    </Button>
                    <Button
                      onClick={() => {
                        setMostrarForm(false)
                        formulario.resetFields()
                        registrar.reset()
                      }}
                    >
                      Cancelar
                    </Button>
                  </Space>
                </Form>
              </div>
            )}
          </section>

          <ModalRegistrarPago
            abierto={modalPago}
            titulo="Registrar pago de la venta"
            saldo={datos.saldo}
            esAnticipo={false}
            guardando={pagar.isPending}
            error={pagar.error}
            onRegistrar={(pago) => pagar.mutateAsync({ ventaId: datos.id, datos: pago })}
            onCerrar={() => {
              pagar.reset()
              setModalPago(false)
            }}
          />
        </>
      )}
    </Modal>
  )
}
