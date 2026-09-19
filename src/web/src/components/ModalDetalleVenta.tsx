import { Button, Form, Input, Modal, Popconfirm, Space, Table, Tag, type TableProps } from 'antd'
import { useState } from 'react'
import { useAnularComprobante, useRegistrarComprobante, useVenta } from '../api/ventas'
import type { DetalleVentaResponse, RegistrarComprobanteRequest } from '../api/tipos'
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
  const [mostrarForm, setMostrarForm] = useState(false)
  const [formulario] = Form.useForm<RegistrarComprobanteRequest>()
  const venta = useVenta(abierto ? (ventaId ?? undefined) : undefined)
  const registrar = useRegistrarComprobante()
  const anular = useAnularComprobante()
  const datos = venta.data

  const handleCerrar = () => {
    setMostrarForm(false)
    formulario.resetFields()
    registrar.reset()
    anular.reset()
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
      },
    })
    setMostrarForm(false)
    formulario.resetFields()
  }

  const handleAnular = async () => {
    if (!ventaId) return
    await anular.mutateAsync(ventaId)
  }

  return (
    <Modal
      title={ventaId ? `Venta ${referenciaOrden(ventaId)}` : 'Venta'}
      open={abierto}
      onCancel={handleCerrar}
      onOk={handleCerrar}
      okText="Cerrar"
      cancelButtonProps={{ style: { display: 'none' } }}
      width={720}
      destroyOnHidden
    >
      <AvisoError error={venta.error} />
      <AvisoError error={anular.error} />
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
              <tr>
                <td>Comprobante</td>
                <td>
                  {datos.comprobante ? (
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
                      <div>
                        <strong>{datos.comprobante.tipo}</strong>
                        {(datos.comprobante.serie || datos.comprobante.numero) && (
                          <span>
                            {' '}
                            ({[datos.comprobante.serie, datos.comprobante.numero].filter(Boolean).join('-')})
                          </span>
                        )}
                        {' '}
                        <Tag color={datos.comprobante.estado === 'Emitido' ? 'success' : 'default'}>
                          {datos.comprobante.estado}
                        </Tag>
                      </div>
                      {datos.comprobante.estado === 'Emitido' && (
                        <Popconfirm
                          title="¿Anular comprobante?"
                          description="El comprobante quedará registrado administrativamente como Anulado."
                          onConfirm={handleAnular}
                          okText="Sí, anular"
                          cancelText="Cancelar"
                          okButtonProps={{ danger: true, loading: anular.isPending }}
                        >
                          <Button size="small" danger type="link" loading={anular.isPending}>
                            Anular comprobante
                          </Button>
                        </Popconfirm>
                      )}
                    </div>
                  ) : datos.estadoId === 1 ? (
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
                      <span className="texto-secundario">Sin comprobante registrado</span>
                      {!mostrarForm && (
                        <Button size="small" type="primary" onClick={() => setMostrarForm(true)}>
                          Registrar comprobante
                        </Button>
                      )}
                    </div>
                  ) : (
                    <span className="texto-secundario">—</span>
                  )}
                </td>
              </tr>
            </tbody>
          </table>

          {mostrarForm && !datos.comprobante && datos.estadoId === 1 && (
            <div
              style={{
                marginTop: 16,
                padding: 16,
                background: 'var(--ant-color-fill-alter, #fafafa)',
                border: '1px solid var(--ant-color-border-secondary, #f0f0f0)',
                borderRadius: 8,
              }}
            >
              <h4 style={{ margin: '0 0 12px 0' }}>Registrar comprobante administrativo</h4>
              <AvisoError error={registrar.error} />
              <Form
                form={formulario}
                layout="vertical"
                onFinish={handleRegistrar}
                initialValues={{ tipo: '', serie: '', numero: '' }}
              >
                <Form.Item
                  label="Tipo de comprobante"
                  name="tipo"
                  rules={[{ required: true, message: 'Ingresa el tipo de comprobante' }]}
                >
                  <Input placeholder="Ej. Boleta, Factura, etc." />
                </Form.Item>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                  <Form.Item label="Serie (opcional)" name="serie">
                    <Input placeholder="Ej. B001" />
                  </Form.Item>
                  <Form.Item label="Número (opcional)" name="numero">
                    <Input placeholder="Ej. 000124" />
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
