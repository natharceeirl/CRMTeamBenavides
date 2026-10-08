import { useState } from 'react'
import { Button, Input, Modal, Select, Table, Tabs, type TableProps } from 'antd'
import { Link } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EtiquetaEstado, type TonoEstado } from '../components/EtiquetaEstado'
import {
  ENTIDAD_APROBACION,
  nombresEntidadAprobacion,
  useAprobacionesPendientes,
  useHistorialAprobaciones,
  useResolverAprobacion,
  type SolicitudAprobacionResponse,
} from '../api/aprobaciones'
import { GERENCIA, nombresGerencia } from '../api/ordenes'
import { fechaHora, soles } from '../utils/formato'

const tonoEstado: Record<number, TonoEstado> = {
  [GERENCIA.noAplica]: 'apagado',
  [GERENCIA.pendiente]: 'alerta',
  [GERENCIA.aprobado]: 'hecho',
  [GERENCIA.rechazado]: 'suave',
}

const opcionesEntidad = Object.values(ENTIDAD_APROBACION).map((entidad) => ({
  value: entidad,
  label: nombresEntidadAprobacion[entidad],
}))

const opcionesEstado = [GERENCIA.pendiente, GERENCIA.aprobado, GERENCIA.rechazado].map((estado) => ({
  value: estado,
  label: nombresGerencia[estado],
}))

// Solo la orden tiene página propia; la venta y el pedido se abren desde su lista.
function Origen({ solicitud }: Readonly<{ solicitud: SolicitudAprobacionResponse }>) {
  const nombre = nombresEntidadAprobacion[solicitud.entidad] ?? solicitud.entidad
  if (solicitud.entidad === ENTIDAD_APROBACION.ordenServicio) {
    return <Link to={`/ordenes/${solicitud.entidadId}`}>{nombre}</Link>
  }
  if (solicitud.entidad === ENTIDAD_APROBACION.venta) {
    return <Link to="/ventas">{nombre}</Link>
  }
  if (solicitud.entidad === ENTIDAD_APROBACION.pedidoLima) {
    return <Link to="/pedidos-lima">{nombre}</Link>
  }
  return <>{nombre}</>
}

const cambioDePrecio = (solicitud: SolicitudAprobacionResponse) =>
  solicitud.valorAnterior != null && solicitud.valorSolicitado != null
    ? `${soles(solicitud.valorAnterior)} → ${soles(solicitud.valorSolicitado)}`
    : '—'

const columnasBase: TableProps<SolicitudAprobacionResponse>['columns'] = [
  {
    title: 'Fecha',
    dataIndex: 'fechaSolicitud',
    className: 'num',
    render: (fecha: string) => fechaHora(fecha),
  },
  { title: 'Origen', key: 'origen', render: (_, solicitud) => <Origen solicitud={solicitud} /> },
  {
    title: 'Cambio',
    key: 'cambio',
    render: (_, solicitud) => (
      <>
        <div>{solicitud.detalleCambio}</div>
        {solicitud.motivo && <div className="texto-secundario">{solicitud.motivo}</div>}
      </>
    ),
  },
  { title: 'Precio', key: 'precio', className: 'num', render: (_, solicitud) => cambioDePrecio(solicitud) },
  {
    title: 'Solicitó',
    dataIndex: 'usuarioSolicitanteNombre',
    render: (nombre: string | null) => nombre ?? '—',
  },
]

type Decision = { solicitud: SolicitudAprobacionResponse; estado: number }

/**
 * Bandeja de Gerencia: cada precio distinto al de lista en una orden, venta o
 * pedido a Lima llega aquí y deja bloqueada la operación hasta que se decida.
 */
export function AprobacionesPage() {
  const [pestana, setPestana] = useState('pendientes')
  const [entidad, setEntidad] = useState<string | undefined>()
  const [estado, setEstado] = useState<number | undefined>()
  const pendientes = useAprobacionesPendientes()
  const historial = useHistorialAprobaciones({ entidad, estado }, pestana === 'historial')
  const resolver = useResolverAprobacion()
  const [decision, setDecision] = useState<Decision | null>(null)
  const [observaciones, setObservaciones] = useState('')

  const rechazando = decision?.estado === GERENCIA.rechazado

  const cerrar = () => {
    setDecision(null)
    setObservaciones('')
    resolver.reset()
  }

  const confirmar = async () => {
    if (!decision) return
    await resolver.mutateAsync({
      id: decision.solicitud.id,
      estado: decision.estado,
      observaciones: observaciones.trim() || null,
    })
    cerrar()
  }

  const columnasPendientes: TableProps<SolicitudAprobacionResponse>['columns'] = [
    ...(columnasBase ?? []),
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, solicitud) => (
        <span style={{ whiteSpace: 'nowrap' }}>
          <Button type="link" onClick={() => setDecision({ solicitud, estado: GERENCIA.aprobado })}>
            Aprobar
          </Button>
          <Button type="link" danger onClick={() => setDecision({ solicitud, estado: GERENCIA.rechazado })}>
            Rechazar
          </Button>
        </span>
      ),
    },
  ]

  const columnasHistorial: TableProps<SolicitudAprobacionResponse>['columns'] = [
    ...(columnasBase ?? []),
    {
      title: 'Decisión',
      key: 'decision',
      render: (_, solicitud) => (
        <>
          <EtiquetaEstado tono={tonoEstado[solicitud.estadoId] ?? 'neutro'}>
            {nombresGerencia[solicitud.estadoId] ?? solicitud.estado}
          </EtiquetaEstado>
          {solicitud.usuarioAprobadorNombre && (
            <div className="texto-secundario">
              {solicitud.usuarioAprobadorNombre} · {fechaHora(solicitud.fechaRespuesta)}
            </div>
          )}
          {solicitud.observacionesRespuesta && <div className="texto-secundario">{solicitud.observacionesRespuesta}</div>}
        </>
      ),
    },
  ]

  return (
    <>
      <BarraSuperior titulo="Aprobaciones" />
      <div className="pagina">
        <AvisoError error={pendientes.error ?? historial.error} />
        <Tabs
          activeKey={pestana}
          onChange={setPestana}
          items={[
            {
              key: 'pendientes',
              label: `Pendientes${pendientes.data?.length ? ` (${pendientes.data.length})` : ''}`,
              children: (
                <Table
                  rowKey="id"
                  columns={columnasPendientes}
                  dataSource={pendientes.data ?? []}
                  loading={pendientes.isPending}
                  pagination={false}
                  locale={{ emptyText: 'No hay cambios de precio esperando aprobación' }}
                />
              ),
            },
            {
              key: 'historial',
              label: 'Historial',
              children: (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                  <div className="filtros">
                    <Select
                      allowClear
                      placeholder="Origen"
                      value={entidad}
                      onChange={setEntidad}
                      options={opcionesEntidad}
                      style={{ minWidth: 200 }}
                    />
                    <Select
                      allowClear
                      placeholder="Decisión"
                      value={estado}
                      onChange={setEstado}
                      options={opcionesEstado}
                      style={{ minWidth: 160 }}
                    />
                  </div>
                  <Table
                    rowKey="id"
                    columns={columnasHistorial}
                    dataSource={historial.data ?? []}
                    loading={historial.isPending}
                    pagination={(historial.data?.length ?? 0) > 20 ? { pageSize: 20 } : false}
                    locale={{ emptyText: 'No hay solicitudes con esos filtros' }}
                  />
                </div>
              ),
            },
          ]}
        />
      </div>

      <Modal
        title={rechazando ? 'Rechazar el cambio de precio' : 'Aprobar el cambio de precio'}
        open={decision !== null}
        onCancel={cerrar}
        onOk={confirmar}
        okText={rechazando ? 'Rechazar' : 'Aprobar'}
        cancelText="Cancelar"
        okButtonProps={{ danger: rechazando, disabled: rechazando && !observaciones.trim() }}
        confirmLoading={resolver.isPending}
        destroyOnHidden
      >
        <AvisoError error={resolver.error} />
        {decision && (
          <p>
            {decision.solicitud.detalleCambio}
            {rechazando
              ? '. La operación sigue bloqueada hasta que se corrija el precio; un precio nuevo abre otra solicitud.'
              : '. La operación queda libre para continuar.'}
          </p>
        )}
        <Input.TextArea
          rows={3}
          value={observaciones}
          onChange={(evento) => setObservaciones(evento.target.value)}
          placeholder={rechazando ? 'Motivo del rechazo (obligatorio)' : 'Observaciones (opcional)'}
        />
      </Modal>
    </>
  )
}
