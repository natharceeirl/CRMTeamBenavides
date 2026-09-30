import { useState } from 'react'
import {
  Button,
  Card,
  Collapse,
  Descriptions,
  Empty,
  Modal,
  Spin,
  Table,
  Tabs,
  Tag,
  Timeline,
  type TableProps,
} from 'antd'
import { EyeOutlined, HistoryOutlined } from '@ant-design/icons'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { Indicadores } from '../components/Indicadores'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { PanelAprobaciones } from '../components/PanelAprobaciones'
import { ModalFotosOrden } from '../components/ModalFotosOrden'
import { ModalFormatoAtencion } from '../components/ModalFormatoAtencion'
import { useVehiculos } from '../api/vehiculos'
import { fechaIngresoOrden, nombresEstado, useOrden, useOrdenes, PRESUPUESTO, nombresPresupuesto } from '../api/ordenes'
import {
  useHistorialServicioUnidad,
  usePortalComprobantes,
  usePortalResumen,
  type AtencionServicioUnidad,
  type PortalComprobante,
} from '../api/portal'
import type { DetalleServicioResponse, OrdenServicioResponse, VehiculoResponse } from '../api/tipos'
import { entero, fechaHora, referenciaOrden, soles } from '../utils/formato'
import { identificadorUnidad, lecturaIngresoOrden, lecturaMedidor, nombreTipoUnidad } from '../utils/unidades'

const colorPresupuesto: Record<number, string> = {
  [PRESUPUESTO.pendiente]: 'gold',
  [PRESUPUESTO.aprobado]: 'green',
  [PRESUPUESTO.rechazado]: 'red',
}

export function PortalClientePage() {
  const [tabActiva, setTabActiva] = useState('unidades')
  const [vehiculoHistorial, setVehiculoHistorial] = useState<VehiculoResponse | null>(null)
  const [ordenSeleccionadaId, setOrdenSeleccionadaId] = useState<string | null>(null)
  const [modalFotosId, setModalFotosId] = useState<string | null>(null)
  const [modalFormatoId, setModalFormatoId] = useState<string | null>(null)

  const resumenQuery = usePortalResumen()
  const vehiculosQuery = useVehiculos()
  const ordenesQuery = useOrdenes()
  const comprobantesQuery = usePortalComprobantes()
  const ordenDetalleQuery = useOrden(ordenSeleccionadaId ?? undefined)
  const historialQuery = useHistorialServicioUnidad(vehiculoHistorial?.id ?? null)

  const resumen = resumenQuery.data

  const columnasVehiculos: TableProps<VehiculoResponse>['columns'] = [
    {
      title: 'Tipo',
      key: 'tipo',
      render: (_, v) => <Tag>{nombreTipoUnidad(v)}</Tag>,
    },
    {
      title: 'Unidad',
      key: 'unidad',
      render: (_, v) => (
        <div>
          <strong>
            {v.marca} {v.modelo}
          </strong>{' '}
          {v.anio ? <span className="texto-secundario">({v.anio})</span> : null}
          {v.color ? <div className="texto-secundario">Color: {v.color}</div> : null}
        </div>
      ),
    },
    {
      title: 'Placa / Serie',
      key: 'identificador',
      render: (_, v) => (
        <div>
          <strong>{identificadorUnidad(v)}</strong>
          {v.placa && v.numeroSerieVIN ? (
            <div className="texto-secundario">VIN: {v.numeroSerieVIN}</div>
          ) : null}
        </div>
      ),
    },
    {
      title: 'Lectura actual',
      key: 'lectura',
      render: (_, v) => lecturaMedidor(v),
    },
    {
      title: 'Acciones',
      key: 'acciones',
      align: 'right',
      render: (_, v) => (
        <Button
          icon={<HistoryOutlined />}
          onClick={() => setVehiculoHistorial(v)}
        >
          Historial de servicio
        </Button>
      ),
    },
  ]

  const columnasOrdenes: TableProps<OrdenServicioResponse>['columns'] = [
    {
      title: 'N° Orden',
      key: 'orden',
      render: (_, o) => <strong>{referenciaOrden(o)}</strong>,
    },
    {
      title: 'Unidad',
      key: 'unidad',
      render: (_, o) => `${o.vehiculoMarca} ${o.vehiculoModelo} · ${o.vehiculoPlaca ?? 'sin placa'}`,
    },
    {
      title: 'Ingreso',
      key: 'ingreso',
      render: (_, o) => fechaHora(fechaIngresoOrden(o)),
    },
    {
      title: 'Entrega estimada',
      key: 'entrega',
      render: (_, o) => fechaHora(o.fechaEstimadaEntrega),
    },
    {
      title: 'Estado taller',
      key: 'estado',
      render: (_, o) => <EstadoOrdenApiTag estadoId={o.estadoId} />,
    },
    {
      title: 'Presupuesto',
      key: 'presupuesto',
      render: (_, o) => {
        const est = o.estadoPresupuestoClienteId ?? PRESUPUESTO.pendiente
        return <Tag color={colorPresupuesto[est]}>{nombresPresupuesto[est]}</Tag>
      },
    },
    {
      title: 'Total',
      key: 'total',
      className: 'num',
      render: (_, o) => <strong>{soles(o.total ?? 0)}</strong>,
    },
    {
      title: 'Acciones',
      key: 'acciones',
      align: 'right',
      render: (_, o) => (
        <Button
          type="primary"
          icon={<EyeOutlined />}
          onClick={() => setOrdenSeleccionadaId(o.id)}
        >
          Ver detalle
        </Button>
      ),
    },
  ]

  const columnasComprobantes: TableProps<PortalComprobante>['columns'] = [
    {
      title: 'Comprobante',
      key: 'comprobante',
      render: (_, c) => (
        <div>
          <Tag color={c.tipo === 'Factura' ? 'blue' : 'green'}>{c.tipo}</Tag>
          <strong>
            {c.serie ?? ''}-{c.numero ?? ''}
          </strong>
        </div>
      ),
    },
    {
      title: 'Fecha emisión',
      key: 'fecha',
      render: (_, c) => fechaHora(c.fecha),
    },
    {
      title: 'N° Orden',
      key: 'orden',
      render: (_, c) => c.numeroOrden ?? '—',
    },
    {
      title: 'Método de pago',
      key: 'metodo',
      render: (_, c) => c.metodoPagoPrincipal ?? '—',
    },
    {
      title: 'Gravado',
      key: 'gravado',
      className: 'num',
      render: (_, c) => soles(c.subtotalGravado ?? 0),
    },
    {
      title: 'IGV',
      key: 'igv',
      className: 'num',
      render: (_, c) => soles(c.montoIgv ?? 0),
    },
    {
      title: 'Total',
      key: 'total',
      className: 'num',
      render: (_, c) => <strong>{soles(c.total ?? 0)}</strong>,
    },
    {
      title: 'Estado',
      key: 'estado',
      render: (_, c) => <Tag color={c.estado === 'Emitido' ? 'success' : 'default'}>{c.estado}</Tag>,
    },
  ]

  const ordenDetalle = ordenDetalleQuery.data

  return (
    <>
      <BarraSuperior
        antetitulo="Portal del Cliente"
        titulo={resumen?.clienteNombre ? `Bienvenido, ${resumen.clienteNombre}` : 'Mi Portal'}
      />

      <div className="pagina">
        {resumenQuery.isError && <AvisoError error={resumenQuery.error} />}

        {resumen && (
          <Indicadores
            items={[
              {
                etiqueta: 'Mis Unidades',
                valor: entero(resumen.cantidadUnidades),
              },
              {
                etiqueta: 'Órdenes Activas',
                valor: entero(resumen.cantidadOrdenesActivas),
              },
              {
                etiqueta: 'Presupuestos Pendientes',
                valor: entero(resumen.cantidadPresupuestosPendientes),
                destacado: resumen.cantidadPresupuestosPendientes > 0,
              },
              {
                etiqueta: 'Saldo Pendiente Total',
                valor: soles(resumen.saldoPendienteTotal),
                compacto: true,
                destacado: resumen.saldoPendienteTotal > 0,
              },
            ]}
          />
        )}

        <Tabs
          activeKey={tabActiva}
          onChange={setTabActiva}
          items={[
            {
              key: 'unidades',
              label: `Mis Unidades (${vehiculosQuery.data?.length ?? 0})`,
              children: (
                <Card>
                  {vehiculosQuery.isError && <AvisoError error={vehiculosQuery.error} />}
                  <Table
                    rowKey="id"
                    loading={vehiculosQuery.isLoading}
                    dataSource={vehiculosQuery.data ?? []}
                    columns={columnasVehiculos}
                    locale={{
                      emptyText: <Empty description="No tienes unidades registradas a tu nombre." />,
                    }}
                  />
                </Card>
              ),
            },
            {
              key: 'ordenes',
              label: `Mis Órdenes de Servicio (${ordenesQuery.data?.length ?? 0})`,
              children: (
                <Card>
                  {ordenesQuery.isError && <AvisoError error={ordenesQuery.error} />}
                  <Table
                    rowKey="id"
                    loading={ordenesQuery.isLoading}
                    dataSource={ordenesQuery.data ?? []}
                    columns={columnasOrdenes}
                    locale={{
                      emptyText: <Empty description="No tienes órdenes de servicio registradas." />,
                    }}
                  />
                </Card>
              ),
            },
            {
              key: 'comprobantes',
              label: `Comprobantes de Pago (${comprobantesQuery.data?.length ?? 0})`,
              children: (
                <Card>
                  {comprobantesQuery.isError && <AvisoError error={comprobantesQuery.error} />}
                  <Table
                    rowKey="id"
                    loading={comprobantesQuery.isLoading}
                    dataSource={comprobantesQuery.data ?? []}
                    columns={columnasComprobantes}
                    locale={{
                      emptyText: <Empty description="Aún no tienes comprobantes de pago emitidos." />,
                    }}
                  />
                </Card>
              ),
            },
          ]}
        />
      </div>

      {/* Modal Historial de Servicio de Unidad */}
      <Modal
        title={
          vehiculoHistorial
            ? `Historial de servicio · ${vehiculoHistorial.marca} ${vehiculoHistorial.modelo} (${identificadorUnidad(vehiculoHistorial)})`
            : 'Historial de servicio'
        }
        open={vehiculoHistorial !== null}
        onCancel={() => setVehiculoHistorial(null)}
        footer={[
          <Button key="cerrar" onClick={() => setVehiculoHistorial(null)}>
            Cerrar
          </Button>,
        ]}
        width={850}
        destroyOnHidden
      >
        {historialQuery.isLoading && (
          <div style={{ textAlign: 'center', padding: '32px 0' }}>
            <Spin size="large" />
          </div>
        )}
        {historialQuery.isError && <AvisoError error={historialQuery.error} />}
        {historialQuery.data && (
          <>
            {historialQuery.data.length === 0 ? (
              <Empty description="Esta unidad no registra atenciones o servicios anteriores en el taller." />
            ) : (
              <Collapse
                defaultActiveKey={[historialQuery.data[0]?.ordenServicioId]}
                items={historialQuery.data.map((atencion: AtencionServicioUnidad) => ({
                  key: atencion.ordenServicioId,
                  label: (
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%', paddingRight: 16 }}>
                      <span>
                        <strong>{atencion.numeroOrden ? `Orden ${atencion.numeroOrden}` : 'Atención'}</strong>
                        {' · '}
                        <span className="texto-secundario">{fechaHora(atencion.fechaIngreso)}</span>
                      </span>
                      <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                        <Tag color="blue">{atencion.estado}</Tag>
                        <strong>{soles(atencion.total)}</strong>
                      </span>
                    </div>
                  ),
                  children: (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                      <Descriptions size="small" column={{ xs: 1, sm: 2 }}>
                        <Descriptions.Item label="Fecha de ingreso">
                          {fechaHora(atencion.fechaIngreso)}
                        </Descriptions.Item>
                        <Descriptions.Item label="Fecha de salida">
                          {fechaHora(atencion.fechaSalida)}
                        </Descriptions.Item>
                        <Descriptions.Item label="Lectura medidor al ingreso">
                          {atencion.lecturaMedidorIngreso != null
                            ? `${entero(atencion.lecturaMedidorIngreso)} ${atencion.tipoMedidor === 'Horas' ? 'h' : 'km'}`
                            : '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label="Falla / Trabajo solicitado">
                          {atencion.motivoFalla ?? '—'}
                        </Descriptions.Item>
                        {atencion.solucion && (
                          <Descriptions.Item label="Solución / Diagnóstico" span={2}>
                            {atencion.solucion}
                          </Descriptions.Item>
                        )}
                      </Descriptions>

                      {atencion.items.length > 0 && (
                        <div>
                          <div style={{ fontWeight: 600, marginBottom: 8 }}>Ítems y servicios realizados:</div>
                          <Table
                            rowKey="id"
                            size="small"
                            pagination={false}
                            dataSource={atencion.items}
                            columns={[
                              {
                                title: 'Tipo',
                                dataIndex: 'tipoItemNombre',
                                key: 'tipo',
                                render: (tipo) => <Tag>{tipo}</Tag>,
                              },
                              {
                                title: 'Descripción',
                                dataIndex: 'descripcion',
                                key: 'desc',
                              },
                              {
                                title: 'Cant.',
                                dataIndex: 'cantidad',
                                key: 'cant',
                                className: 'num',
                              },
                              {
                                title: 'P. Unitario',
                                dataIndex: 'precioUnitario',
                                key: 'precio',
                                className: 'num',
                                render: (p) => soles(p),
                              },
                              {
                                title: 'Total',
                                dataIndex: 'total',
                                key: 'total',
                                className: 'num',
                                render: (t) => <strong>{soles(t)}</strong>,
                              },
                            ]}
                          />
                        </div>
                      )}
                    </div>
                  ),
                }))}
              />
            )}
          </>
        )}
      </Modal>

      {/* Modal Detalle de Orden y Aprobación de Presupuesto */}
      <Modal
        title={
          ordenDetalle
            ? `Detalle de Orden ${referenciaOrden(ordenDetalle)} · ${ordenDetalle.vehiculoMarca} ${ordenDetalle.vehiculoModelo}`
            : 'Detalle de Orden'
        }
        open={ordenSeleccionadaId !== null}
        onCancel={() => setOrdenSeleccionadaId(null)}
        footer={[
          <Button key="fotos" onClick={() => setModalFotosId(ordenSeleccionadaId)}>
            📷 Ver Fotografías
          </Button>,
          <Button key="formato" type="primary" onClick={() => setModalFormatoId(ordenSeleccionadaId)}>
            🖨️ Formato de Atención
          </Button>,
          <Button key="cerrar" onClick={() => setOrdenSeleccionadaId(null)}>
            Cerrar
          </Button>,
        ]}
        width={950}
        destroyOnHidden
      >
        {ordenDetalleQuery.isLoading && (
          <div style={{ textAlign: 'center', padding: '32px 0' }}>
            <Spin size="large" />
          </div>
        )}
        {ordenDetalleQuery.isError && <AvisoError error={ordenDetalleQuery.error} />}
        {ordenDetalle && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
            {/* Panel de Aprobaciones para que el cliente decida */}
            <PanelAprobaciones orden={ordenDetalle} />

            <Descriptions
              title="Información General"
              bordered
              size="small"
              column={{ xs: 1, sm: 2, md: 3 }}
            >
              <Descriptions.Item label="Unidad">
                {ordenDetalle.vehiculoMarca} {ordenDetalle.vehiculoModelo} ({ordenDetalle.vehiculoPlaca ?? 'sin placa'})
              </Descriptions.Item>
              <Descriptions.Item label="Estado de la Orden">
                <EstadoOrdenApiTag estadoId={ordenDetalle.estadoId} />
              </Descriptions.Item>
              <Descriptions.Item label="Técnico a cargo">
                {ordenDetalle.tecnicoNombre ?? 'Por asignar'}
              </Descriptions.Item>
              <Descriptions.Item label="Fecha de Ingreso">
                {fechaHora(fechaIngresoOrden(ordenDetalle))}
              </Descriptions.Item>
              <Descriptions.Item label="Entrega estimada">
                {fechaHora(ordenDetalle.fechaEstimadaEntrega)}
              </Descriptions.Item>
              <Descriptions.Item label="Medidor de ingreso">
                {lecturaIngresoOrden(ordenDetalle)}
              </Descriptions.Item>
              <Descriptions.Item label="Motivo de Falla" span={3}>
                {ordenDetalle.motivoFalla ?? '—'}
              </Descriptions.Item>
              {ordenDetalle.diagnostico && (
                <Descriptions.Item label="Diagnóstico del Taller" span={3}>
                  {ordenDetalle.diagnostico}
                </Descriptions.Item>
              )}
              {ordenDetalle.solucion && (
                <Descriptions.Item label="Solución Propuesta" span={3}>
                  {ordenDetalle.solucion}
                </Descriptions.Item>
              )}
            </Descriptions>

            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                <h3 style={{ margin: 0 }}>Ítems del Presupuesto</h3>
                <div style={{ fontSize: 18, fontWeight: 700 }}>
                  Total Presupuestado: {soles(ordenDetalle.total)}
                </div>
              </div>

              <Table<DetalleServicioResponse>
                rowKey="id"
                size="middle"
                pagination={false}
                dataSource={ordenDetalle.detalles ?? []}
                columns={[
                  {
                    title: 'Concepto / Ítem',
                    key: 'concepto',
                    render: (_, d) => (
                      <div>
                        <strong>{d.descripcion}</strong>
                        <div className="texto-secundario">
                          {d.tipoItemNombre ?? (d.esRepuesto ? 'Repuesto' : 'Servicio')}
                        </div>
                      </div>
                    ),
                  },
                  {
                    title: 'Cantidad',
                    dataIndex: 'cantidad',
                    key: 'cantidad',
                    className: 'num',
                    render: (c) => entero(c),
                  },
                  {
                    title: 'Precio Unitario',
                    dataIndex: 'precioUnitario',
                    key: 'precio',
                    className: 'num',
                    render: (p) => soles(p),
                  },
                  {
                    title: 'Subtotal',
                    dataIndex: 'subtotal',
                    key: 'subtotal',
                    className: 'num',
                    render: (s) => <strong>{soles(s)}</strong>,
                  },
                ]}
                locale={{
                  emptyText: <Empty description="Esta orden aún no tiene ítems presupuestados." />,
                }}
              />
            </div>

            {ordenDetalle.historial && ordenDetalle.historial.length > 0 && (
              <div>
                <h3 style={{ marginBottom: 12 }}>Historial de la Orden</h3>
                <Timeline
                  items={ordenDetalle.historial.map((h) => ({
                    key: h.id,
                    color: h.estadoAnteriorId !== h.estadoNuevoId ? 'blue' : 'gray',
                    children: (
                      <div>
                        <strong>
                          {h.estadoAnteriorId !== h.estadoNuevoId
                            ? (nombresEstado[h.estadoNuevoId] ?? h.estadoNuevo)
                            : (h.observaciones ?? 'Actualización')}
                        </strong>
                        {h.observaciones && h.estadoAnteriorId !== h.estadoNuevoId && (
                          <div style={{ fontSize: 13, marginTop: 4 }}>{h.observaciones}</div>
                        )}
                        <div className="texto-secundario" style={{ fontSize: 12 }}>
                          {fechaHora(h.fechaCambio)} · {h.usuarioNombre ?? 'Sistema'}
                        </div>
                      </div>
                    ),
                  }))}
                />
              </div>
            )}
          </div>
        )}
      </Modal>

      <ModalFotosOrden
        abierto={modalFotosId !== null}
        ordenServicioId={modalFotosId ?? ''}
        soloLectura={true}
        onCerrar={() => setModalFotosId(null)}
      />

      <ModalFormatoAtencion
        abierto={modalFormatoId !== null}
        ordenServicioId={modalFormatoId ?? ''}
        onCerrar={() => setModalFormatoId(null)}
      />
    </>
  )
}
