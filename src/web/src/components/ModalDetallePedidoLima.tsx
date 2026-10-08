import { useState } from 'react'
import type { Dayjs } from 'dayjs'
import dayjs from 'dayjs'
import { Button, DatePicker, Form, Input, InputNumber, Modal, Space, Table, type TableProps } from 'antd'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import {
  esPedidoFinal,
  nombresEstadoPedidoLima,
  siguienteEstadoPedido,
  useActualizarDetallePedidoLima,
  useActualizarPedidoLima,
  useCambiarEstadoPedidoLima,
  useCancelarPedidoLima,
  usePedidoLima,
  useRegistrarPagoPedidoLima,
} from '../api/pedidosLima'
import { GERENCIA, nombresGerencia } from '../api/ordenes'
import { useProductos } from '../api/inventario'
import { tieneSaldo } from '../api/pagos'
import {
  ESTADO_PEDIDO_LIMA,
  type DetallePedidoLimaResponse,
  type EstadoPedidoLima,
  type PedidoLimaResponse,
} from '../api/tipos'
import { AvisoError } from './AvisoError'
import { AvisoPrecioGerencia } from './AvisoPrecioGerencia'
import { EtiquetaEstado, type TonoEstado } from './EtiquetaEstado'
import { ModalRegistrarPago } from './ModalRegistrarPago'
import { ResumenCobro } from './ResumenCobro'
import { HistorialEstados } from './HistorialEstados'
import { EstadoPedidoLimaTag } from './EstadoPedidoLimaTag'
import { colores } from '../theme/tokens'
import { fechaCorta, fechaHora, importe, soles } from '../utils/formato'

type Props = {
  pedidoId: string | null
  onCerrar: () => void
}

type CamposEnvio = {
  empresaTransporte?: string
  numeroGuia?: string
  fechaEstimadaLlegada?: Dayjs | null
  observaciones?: string
}

/** Lo que se confirma en el modal chico: avanzar al estado siguiente o cancelar. */
type Paso = { tipo: 'avanzar'; destino: EstadoPedidoLima } | { tipo: 'cancelar' }

const tonoGerencia: Record<number, TonoEstado> = {
  [GERENCIA.pendiente]: 'alerta',
  [GERENCIA.aprobado]: 'hecho',
  [GERENCIA.rechazado]: 'suave',
}

const columnas: TableProps<DetallePedidoLimaResponse>['columns'] = [
  {
    title: 'Repuesto',
    key: 'repuesto',
    render: (_, detalle) => (
      <>
        <div>{detalle.productoNombre}</div>
        <div className="texto-secundario">{detalle.productoCodigo}</div>
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
    title: 'IGV',
    dataIndex: 'montoIgv',
    align: 'right',
    className: 'num',
    render: (igv: number) => importe(igv),
  },
  {
    title: 'Total',
    dataIndex: 'total',
    align: 'right',
    className: 'num',
    render: (total: number) => importe(total),
  },
]

function Totales({ pedido }: Readonly<{ pedido: PedidoLimaResponse }>) {
  return (
    <div className="totales">
      <div>
        <div className="etiqueta">Op. gravadas</div>
        <div className="valor">{soles(pedido.subtotalGravado)}</div>
      </div>
      {pedido.subtotalExonerado > 0 && (
        <div>
          <div className="etiqueta">Exoneradas</div>
          <div className="valor">{soles(pedido.subtotalExonerado)}</div>
        </div>
      )}
      {pedido.subtotalInafecto > 0 && (
        <div>
          <div className="etiqueta">Inafectas</div>
          <div className="valor">{soles(pedido.subtotalInafecto)}</div>
        </div>
      )}
      <div>
        <div className="etiqueta">IGV</div>
        <div className="valor">{soles(pedido.montoIgv)}</div>
      </div>
      <div>
        <div className="etiqueta">Total</div>
        <div className="valor total">{soles(pedido.total)}</div>
      </div>
    </div>
  )
}

type CamposLinea = { cantidad: number; precioUnitario: number }

/** Cantidad y precio de un repuesto del pedido, con el aviso cuando el precio sale de la lista. */
function ModalEditarLineaPedido({
  pedidoId,
  detalle,
  onCerrar,
}: Readonly<{ pedidoId: string; detalle: DetallePedidoLimaResponse; onCerrar: () => void }>) {
  const [formulario] = Form.useForm<CamposLinea>()
  const productos = useProductos()
  const actualizar = useActualizarDetallePedidoLima()
  const precioUnitario = Form.useWatch('precioUnitario', formulario)
  const deLista = (productos.data ?? []).find((producto) => producto.id === detalle.productoId)?.precioVenta

  const cerrar = () => {
    actualizar.reset()
    onCerrar()
  }

  const guardar = async (campos: CamposLinea) => {
    await actualizar.mutateAsync({
      id: pedidoId,
      detalleId: detalle.id,
      datos: {
        precioUnitario: campos.precioUnitario,
        cantidad: campos.cantidad === detalle.cantidad ? null : campos.cantidad,
      },
    })
    cerrar()
  }

  return (
    <Modal
      title={detalle.productoNombre}
      open
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={actualizar.isPending}
      destroyOnHidden
    >
      <AvisoError error={actualizar.error} />
      <Form<CamposLinea>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={guardar}
        initialValues={{ cantidad: detalle.cantidad, precioUnitario: detalle.precioUnitario }}
      >
        <div className="formulario-grid">
          <Form.Item label="Cantidad" name="cantidad" rules={[{ required: true, message: 'Indica la cantidad' }]}>
            <InputNumber min={1} precision={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            label="Precio unitario (PEN, sin IGV)"
            name="precioUnitario"
            rules={[{ required: true, message: 'Indica el precio' }]}
          >
            <InputNumber min={0} step={0.5} style={{ width: '100%' }} />
          </Form.Item>
        </div>
        <AvisoPrecioGerencia precio={precioUnitario} deLista={deLista} />
      </Form>
    </Modal>
  )
}

/** Se monta cada vez que se abre, para empezar con los datos vigentes y no con lo último que se escribió. */
function FormularioEnvio({ pedido, onListo }: Readonly<{ pedido: PedidoLimaResponse; onListo: () => void }>) {
  const [formulario] = Form.useForm<CamposEnvio>()
  const actualizar = useActualizarPedidoLima()

  const guardar = async (campos: CamposEnvio) => {
    // El backend deja como está lo que llega en null; un texto vacío sí borra el dato.
    await actualizar.mutateAsync({
      id: pedido.id,
      datos: {
        empresaTransporte: (campos.empresaTransporte ?? '').trim(),
        numeroGuia: (campos.numeroGuia ?? '').trim(),
        fechaEstimadaLlegada: campos.fechaEstimadaLlegada?.startOf('day').toISOString() ?? null,
        fechaLlegada: null,
        fechaEntrega: null,
        observaciones: (campos.observaciones ?? '').trim(),
      },
    })
    onListo()
  }

  return (
    <div style={{ padding: 16, background: colores.neutro100, border: `1px solid ${colores.neutro300}` }}>
      <AvisoError error={actualizar.error} />
      <Form<CamposEnvio>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={guardar}
        initialValues={{
          empresaTransporte: pedido.empresaTransporte ?? '',
          numeroGuia: pedido.numeroGuia ?? '',
          fechaEstimadaLlegada: pedido.fechaEstimadaLlegada ? dayjs(pedido.fechaEstimadaLlegada) : null,
          observaciones: pedido.observaciones ?? '',
        }}
      >
        <div className="formulario-grid">
          <Form.Item label="Empresa de transporte" name="empresaTransporte">
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item label="Número de guía" name="numeroGuia">
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item label="Llegada estimada" name="fechaEstimadaLlegada">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Observaciones" name="observaciones" className="ancho-completo">
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
        </div>
        <Space>
          <Button type="primary" htmlType="submit" loading={actualizar.isPending}>
            Guardar envío
          </Button>
          <Button onClick={onListo}>Cancelar</Button>
        </Space>
      </Form>
    </div>
  )
}

export function ModalDetallePedidoLima({ pedidoId, onCerrar }: Readonly<Props>) {
  const { tienePermiso } = useSesion()
  const puedeEditar = tienePermiso(PERMISOS.pedidosLimaEditar)
  const puedeDespachar = tienePermiso(PERMISOS.pedidosLimaDespachar)
  const puedeCancelar = tienePermiso(PERMISOS.pedidosLimaCancelar)
  const puedeCobrar = tienePermiso(PERMISOS.ventasCrear)

  const pedido = usePedidoLima(pedidoId)
  const cambiarEstado = useCambiarEstadoPedidoLima()
  const cancelar = useCancelarPedidoLima()
  const pagar = useRegistrarPagoPedidoLima()

  const [editandoEnvio, setEditandoEnvio] = useState(false)
  const [lineaEnEdicion, setLineaEnEdicion] = useState<DetallePedidoLimaResponse | null>(null)
  // null: sin modal; true: adelanto; false: pago del saldo.
  const [cobro, setCobro] = useState<boolean | null>(null)
  const [paso, setPaso] = useState<Paso | null>(null)
  const [nota, setNota] = useState('')

  const datos = pedido.data
  // Cancelar no es un paso de la secuencia: va por su propio botón, con motivo.
  const destino = datos ? siguienteEstadoPedido(datos.estado) : null
  const entregar = destino === ESTADO_PEDIDO_LIMA.entregado
  const abierto = datos ? !esPedidoFinal(datos.estado) : false
  const tituloPaso =
    paso?.tipo === 'avanzar' ? `Pasar a «${nombresEstadoPedidoLima[paso.destino]}»` : 'Cancelar pedido'
  const aprobacion = datos?.estadoAprobacionGerenciaId ?? GERENCIA.noAplica
  // Con un precio pendiente o rechazado, el backend no deja confirmar ni entregar el pedido.
  const bloqueadoPorPrecio =
    (aprobacion === GERENCIA.pendiente || aprobacion === GERENCIA.rechazado) &&
    (destino === ESTADO_PEDIDO_LIMA.confirmado || destino === ESTADO_PEDIDO_LIMA.entregado)
  const cancelado = datos?.estado === ESTADO_PEDIDO_LIMA.cancelado
  const saldoPendiente = datos ? tieneSaldo({ total: datos.total, saldo: datos.saldo ?? datos.total }) : false
  const columnasConAcciones: TableProps<DetallePedidoLimaResponse>['columns'] =
    abierto && puedeEditar
      ? [
          ...(columnas ?? []),
          {
            title: '',
            key: 'editar',
            align: 'right',
            render: (_, detalle) => (
              <Button type="link" onClick={() => setLineaEnEdicion(detalle)}>
                Editar
              </Button>
            ),
          },
        ]
      : columnas

  const cerrarPaso = () => {
    setPaso(null)
    setNota('')
    cambiarEstado.reset()
    cancelar.reset()
  }

  const cerrar = () => {
    cerrarPaso()
    setEditandoEnvio(false)
    setLineaEnEdicion(null)
    setCobro(null)
    pagar.reset()
    onCerrar()
  }

  const confirmarPaso = async () => {
    if (!datos || !paso) return
    if (paso.tipo === 'cancelar') {
      await cancelar.mutateAsync({ id: datos.id, motivo: nota.trim() })
    } else {
      await cambiarEstado.mutateAsync({ id: datos.id, nuevoEstado: paso.destino, observacion: nota.trim() || null })
    }
    cerrarPaso()
  }

  return (
    <Modal
      title={datos ? `Pedido ${datos.numeroPedido}` : 'Pedido a Lima'}
      open={pedidoId !== null}
      onCancel={cerrar}
      onOk={cerrar}
      okText="Cerrar"
      cancelButtonProps={{ style: { display: 'none' } }}
      width={820}
      destroyOnHidden
    >
      <AvisoError error={pedido.error} />
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
                <td>Registrado</td>
                <td>{fechaHora(datos.fecha)}</td>
              </tr>
              <tr>
                <td>Estado</td>
                <td>
                  <EstadoPedidoLimaTag estado={datos.estado} />
                </td>
              </tr>
              {aprobacion !== GERENCIA.noAplica && (
                <tr>
                  <td>Precio</td>
                  <td>
                    <EtiquetaEstado tono={tonoGerencia[aprobacion] ?? 'neutro'}>
                      {aprobacion === GERENCIA.pendiente
                        ? 'Esperando a Gerencia'
                        : `${nombresGerencia[aprobacion]} por Gerencia`}
                    </EtiquetaEstado>
                  </td>
                </tr>
              )}
              {datos.motivoCancelacion && (
                <tr>
                  <td>Motivo de cancelación</td>
                  <td>{datos.motivoCancelacion}</td>
                </tr>
              )}
            </tbody>
          </table>

          <Table
            rowKey="id"
            columns={columnasConAcciones}
            dataSource={datos.detalles}
            pagination={false}
            style={{ marginTop: 16 }}
          />
          <Totales pedido={datos} />
          {aprobacion === GERENCIA.rechazado && abierto && (
            <p className="texto-secundario" style={{ marginTop: 8 }}>
              Gerencia rechazó el precio: corrígelo en el repuesto y se pide una aprobación nueva.
            </p>
          )}

          {!cancelado && (
            <section style={{ marginTop: 28 }}>
              <div className="seccion-titulo">
                <h2>Cobro</h2>
                {puedeCobrar && saldoPendiente && (
                  <div className="acciones">
                    {abierto && <Button onClick={() => setCobro(true)}>Registrar adelanto</Button>}
                    <Button type="primary" onClick={() => setCobro(false)}>
                      Registrar pago
                    </Button>
                  </div>
                )}
              </div>
              <ResumenCobro
                total={datos.total}
                totalPagado={datos.totalPagado ?? 0}
                saldo={datos.saldo ?? datos.total}
                estadoPago={datos.estadoPago}
                pagos={datos.pagos ?? []}
              />
            </section>
          )}

          <section style={{ marginTop: 28 }}>
            <div className="seccion-titulo">
              <h2>Envío</h2>
              {abierto && puedeEditar && !editandoEnvio && (
                <div className="acciones">
                  <Button onClick={() => setEditandoEnvio(true)}>Editar envío</Button>
                </div>
              )}
            </div>

            {editandoEnvio ? (
              <FormularioEnvio pedido={datos} onListo={() => setEditandoEnvio(false)} />
            ) : (
              <table className="tabla-simple">
                <tbody>
                  <tr>
                    <td>Transporte</td>
                    <td>{datos.empresaTransporte || '—'}</td>
                  </tr>
                  <tr>
                    <td>Guía</td>
                    <td>{datos.numeroGuia || '—'}</td>
                  </tr>
                  <tr>
                    <td>Llegada estimada</td>
                    <td>{fechaCorta(datos.fechaEstimadaLlegada)}</td>
                  </tr>
                  <tr>
                    <td>Llegó</td>
                    <td>{fechaHora(datos.fechaLlegada)}</td>
                  </tr>
                  <tr>
                    <td>Entregado</td>
                    <td>{fechaHora(datos.fechaEntrega)}</td>
                  </tr>
                  <tr>
                    <td>Observaciones</td>
                    <td>{datos.observaciones || '—'}</td>
                  </tr>
                </tbody>
              </table>
            )}
          </section>

          <section style={{ marginTop: 28 }}>
            <div className="seccion-titulo">
              <h2>Seguimiento</h2>
              {abierto && (
                <div className="acciones">
                  {puedeCancelar && (
                    <Button onClick={() => setPaso({ tipo: 'cancelar' })}>Cancelar pedido</Button>
                  )}
                  {destino !== null && puedeEditar && (!entregar || puedeDespachar) && (
                    <Button
                      type="primary"
                      disabled={bloqueadoPorPrecio}
                      onClick={() => setPaso({ tipo: 'avanzar', destino })}
                    >
                      {entregar ? 'Entregar al cliente' : `Marcar ${nombresEstadoPedidoLima[destino].toLowerCase()}`}
                    </Button>
                  )}
                </div>
              )}
            </div>
            {bloqueadoPorPrecio && (
              <p className="texto-secundario">El pedido sigue cuando Gerencia apruebe el precio.</p>
            )}
            {entregar && !puedeDespachar && (
              <p className="texto-secundario">La entrega descuenta stock: la registra quien tiene permiso de despacho.</p>
            )}
            <HistorialEstados cambios={datos.historial} nombres={nombresEstadoPedidoLima} />
          </section>
        </>
      )}

      {datos && lineaEnEdicion && (
        <ModalEditarLineaPedido pedidoId={datos.id} detalle={lineaEnEdicion} onCerrar={() => setLineaEnEdicion(null)} />
      )}

      {datos && (
        <ModalRegistrarPago
          abierto={cobro !== null}
          titulo={cobro ? 'Registrar adelanto del pedido' : 'Registrar pago del pedido'}
          saldo={datos.saldo ?? datos.total}
          esAnticipo={cobro === true}
          guardando={pagar.isPending}
          error={pagar.error}
          onRegistrar={(pago) => pagar.mutateAsync({ id: datos.id, datos: pago })}
          onCerrar={() => {
            pagar.reset()
            setCobro(null)
          }}
        />
      )}

      <Modal
        title={tituloPaso}
        open={paso !== null}
        onCancel={cerrarPaso}
        onOk={confirmarPaso}
        okText={paso?.tipo === 'cancelar' ? 'Cancelar pedido' : 'Confirmar'}
        cancelText="Volver"
        okButtonProps={{ disabled: paso?.tipo === 'cancelar' && !nota.trim() }}
        confirmLoading={cambiarEstado.isPending || cancelar.isPending}
        destroyOnHidden
      >
        <AvisoError error={cambiarEstado.error ?? cancelar.error} />
        {paso?.tipo === 'avanzar' && paso.destino === ESTADO_PEDIDO_LIMA.entregado && (
          <p>Se descuenta del stock la cantidad de cada repuesto del pedido.</p>
        )}
        <Input.TextArea
          rows={3}
          maxLength={500}
          value={nota}
          onChange={(evento) => setNota(evento.target.value)}
          placeholder={paso?.tipo === 'cancelar' ? 'Motivo de la cancelación' : 'Observación (opcional)'}
        />
      </Modal>
    </Modal>
  )
}
