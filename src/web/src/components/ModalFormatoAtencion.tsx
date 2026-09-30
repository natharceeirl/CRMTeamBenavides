import { Button, Card, Descriptions, Divider, Modal, Spin, Table, Tag, Typography } from 'antd'
import { useFormatoAtencionOrden } from '../api/ordenes'
import { AvisoError } from './AvisoError'
import { fechaHora, importe, soles } from '../utils/formato'

const { Title, Text } = Typography

type Props = {
  abierto: boolean
  ordenServicioId: string
  onCerrar: () => void
}

export function ModalFormatoAtencion({ abierto, ordenServicioId, onCerrar }: Readonly<Props>) {
  const query = useFormatoAtencionOrden(abierto ? ordenServicioId : null)
  const datos = query.data

  const handleImprimir = () => {
    window.open(`/api/ordenes-servicio/${ordenServicioId}/formato-atencion/imprimir`, '_blank')
  }

  return (
    <Modal
      title={
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginRight: 32 }}>
          <span>Formato Oficial de Atención</span>
          {datos && <Tag color="blue">{datos.orden.numeroOrden}</Tag>}
        </div>
      }
      open={abierto}
      onCancel={onCerrar}
      footer={[
        <Button key="imprimir" type="primary" onClick={handleImprimir} disabled={!datos}>
          🖨️ Imprimir / Guardar PDF
        </Button>,
        <Button key="cerrar" onClick={onCerrar}>
          Cerrar
        </Button>,
      ]}
      width={840}
      destroyOnHidden
    >
      {query.isPending && (
        <div style={{ textAlign: 'center', padding: 40 }}>
          <Spin tip="Cargando formato de atención..." />
        </div>
      )}

      {query.isError && <AvisoError error={query.error} />}

      {datos && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Encabezado Taller */}
          <div style={{ background: '#f8fafc', padding: 12, borderRadius: 6, border: '1px solid #e2e8f0' }}>
            <Title level={5} style={{ margin: 0 }}>
              {datos.empresa.nombreTaller}
            </Title>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {datos.empresa.razonSocial} {datos.empresa.ruc ? `· RUC: ${datos.empresa.ruc}` : ''}
              {datos.empresa.direccion ? ` · ${datos.empresa.direccion}` : ''}
            </Text>
          </div>

          {/* Datos Cliente y Unidad */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Card size="small" title="Cliente">
              <Descriptions size="small" column={1}>
                <Descriptions.Item label="Nombre">{datos.cliente.nombreCompleto}</Descriptions.Item>
                {datos.cliente.numeroDocumento && (
                  <Descriptions.Item label={datos.cliente.tipoDocumento ?? 'Documento'}>
                    {datos.cliente.numeroDocumento}
                  </Descriptions.Item>
                )}
                {datos.cliente.telefono && (
                  <Descriptions.Item label="Teléfono">{datos.cliente.telefono}</Descriptions.Item>
                )}
                {datos.cliente.direccion && (
                  <Descriptions.Item label="Dirección">{datos.cliente.direccion}</Descriptions.Item>
                )}
              </Descriptions>
            </Card>

            <Card size="small" title="Unidad">
              <Descriptions size="small" column={1}>
                <Descriptions.Item label="Tipo / Marca">
                  {datos.unidad.tipoUnidad} · {datos.unidad.marca} {datos.unidad.modelo}
                </Descriptions.Item>
                {datos.unidad.placa && (
                  <Descriptions.Item label="Placa">{datos.unidad.placa}</Descriptions.Item>
                )}
                {datos.unidad.numeroSerieVIN && (
                  <Descriptions.Item label="VIN / Serie">{datos.unidad.numeroSerieVIN}</Descriptions.Item>
                )}
                {datos.unidad.lecturaIngreso !== null && (
                  <Descriptions.Item label="Lectura Ingreso">
                    {datos.unidad.lecturaIngreso} {datos.unidad.tipoMedidor === 'Horas' ? 'hrs' : 'km'}
                  </Descriptions.Item>
                )}
              </Descriptions>
            </Card>
          </div>

          {/* Fechas y Trabajo */}
          <Card size="small" title="Datos de Atención">
            <Descriptions size="small" column={2}>
              <Descriptions.Item label="Fecha Ingreso">
                {fechaHora(datos.orden.fechaIngreso)}
              </Descriptions.Item>
              <Descriptions.Item label="Estado">
                <Tag color="cyan">{datos.orden.estadoNombre}</Tag>
              </Descriptions.Item>
              <Descriptions.Item label="Tipo de Atención">
                {datos.orden.tipoAtencion} ({datos.orden.modalidadAtencion})
              </Descriptions.Item>
              <Descriptions.Item label="Técnico Responsable">
                {datos.trabajo.tecnicoResponsable ?? 'Sin asignar'}
              </Descriptions.Item>
            </Descriptions>

            {(datos.trabajo.diagnostico || datos.trabajo.motivoFalla) && (
              <div style={{ marginTop: 12, paddingTop: 8, borderTop: '1px solid #f1f5f9' }}>
                {datos.trabajo.motivoFalla && (
                  <div style={{ fontSize: 12, marginBottom: 4 }}>
                    <strong>Motivo de Ingreso:</strong> {datos.trabajo.motivoFalla}
                  </div>
                )}
                {datos.trabajo.diagnostico && (
                  <div style={{ fontSize: 12 }}>
                    <strong>Diagnóstico:</strong> {datos.trabajo.diagnostico}
                  </div>
                )}
              </div>
            )}
          </Card>

          {/* Tabla de Trabajos y Repuestos */}
          <Table
            size="small"
            pagination={false}
            rowKey="id"
            dataSource={datos.items}
            columns={[
              { title: 'Tipo', dataIndex: 'tipoItemNombre', width: 110 },
              { title: 'Descripción', dataIndex: 'descripcion' },
              { title: 'Cant.', dataIndex: 'cantidad', align: 'center', width: 70 },
              {
                title: 'P. Unit.',
                dataIndex: 'precioUnitario',
                align: 'right',
                width: 100,
                render: (val: number) => importe(val),
              },
              {
                title: 'Total',
                dataIndex: 'total',
                align: 'right',
                width: 110,
                render: (val: number) => importe(val),
              },
            ]}
          />

          {/* Totales Financieros */}
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <div style={{ width: 280, background: '#f8fafc', padding: 12, borderRadius: 6, border: '1px solid #e2e8f0' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                <Text type="secondary">Subtotal Gravado:</Text>
                <Text>{soles(datos.financiero.subtotalGravado)}</Text>
              </div>
              {datos.financiero.subtotalExonerado > 0 && (
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                  <Text type="secondary">Exonerado:</Text>
                  <Text>{soles(datos.financiero.subtotalExonerado)}</Text>
                </div>
              )}
              {datos.financiero.subtotalInafecto > 0 && (
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                  <Text type="secondary">Inafecto:</Text>
                  <Text>{soles(datos.financiero.subtotalInafecto)}</Text>
                </div>
              )}
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
                <Text type="secondary">IGV ({datos.financiero.porcentajeIgv}%):</Text>
                <Text>{soles(datos.financiero.montoIgv)}</Text>
              </div>
              <Divider style={{ margin: '6px 0' }} />
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4, fontWeight: 700, fontSize: 14 }}>
                <span>TOTAL:</span>
                <span>{soles(datos.financiero.total)}</span>
              </div>
              {datos.financiero.totalPagado > 0 && (
                <div style={{ display: 'flex', justifyContent: 'space-between', color: '#059669', marginBottom: 4 }}>
                  <span>Pagado / Anticipos:</span>
                  <span>{soles(datos.financiero.totalPagado)}</span>
                </div>
              )}
              <div style={{ display: 'flex', justifyContent: 'space-between', color: '#dc2626', fontWeight: 600 }}>
                <span>Saldo Pendiente:</span>
                <span>{soles(datos.financiero.saldoPendiente)}</span>
              </div>
            </div>
          </div>
        </div>
      )}
    </Modal>
  )
}
