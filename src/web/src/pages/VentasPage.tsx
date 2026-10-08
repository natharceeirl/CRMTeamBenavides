import { useState } from 'react'
import dayjs from 'dayjs'
import { Button, Popconfirm, Select, Space, Table, Tabs, Tag, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalVenta } from '../components/ModalVenta'
import { ModalDetalleVenta } from '../components/ModalDetalleVenta'
import { EstadoPagoTag } from '../components/EstadoPagoTag'
import {
  ESTADO_VENTA,
  nombresEstadoVenta,
  puedeAnular,
  puedeConfirmar,
  rutaExportarVentas,
  useAnularVenta,
  useConfirmarVenta,
  useVentas,
  useVentasPendientesComprobante,
} from '../api/ventas'
import { GERENCIA } from '../api/ordenes'
import { descargarArchivo } from '../utils/descarga'
import type { VentaResponse } from '../api/tipos'
import { colores } from '../theme/tokens'
import { fechaHora, referenciaOrden, soles } from '../utils/formato'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'

const opcionesEstado = Object.entries(nombresEstadoVenta).map(([valor, etiqueta]) => ({
  value: Number(valor),
  label: etiqueta,
}))

function EstadoVentaTag({ estadoId }: Readonly<{ estadoId: number }>) {
  const estilos: Record<number, { background: string; color: string; borderColor: string }> = {
    [ESTADO_VENTA.cotizacion]: {
      background: colores.neutro100,
      color: colores.neutro800,
      borderColor: colores.neutro300,
    },
    [ESTADO_VENTA.confirmada]: {
      background: colores.acento600,
      color: colores.blanco,
      borderColor: colores.acento600,
    },
    [ESTADO_VENTA.anulada]: {
      background: 'transparent',
      color: colores.textoSecundario,
      borderColor: colores.neutro300,
    },
  }

  return (
    <Tag style={{ ...estilos[estadoId], marginInlineEnd: 0, fontWeight: 600 }}>
      {nombresEstadoVenta[estadoId] ?? 'Desconocido'}
    </Tag>
  )
}

/** Un precio pendiente o rechazado bloquea confirmar la cotización y registrar el comprobante. */
const precioBloqueado = (venta: VentaResponse) =>
  venta.estadoAprobacionGerenciaId === GERENCIA.pendiente || venta.estadoAprobacionGerenciaId === GERENCIA.rechazado

export function VentasPage() {
  const [estado, setEstado] = useState<number>()
  const [vista, setVista] = useState('todas')
  const [errorDescarga, setErrorDescarga] = useState<unknown>(null)
  const [modalNueva, setModalNueva] = useState(false)
  const [ventaVista, setVentaVista] = useState<string | null>(null)

  const { tienePermiso } = useSesion()
  const puedeVender = tienePermiso(PERMISOS.ventasCrear)
  const puedeAnularVentas = tienePermiso(PERMISOS.ventasAnular)

  const ventas = useVentas({ estado })
  const sinComprobante = useVentasPendientesComprobante()

  const exportar = () => {
    setErrorDescarga(null)
    descargarArchivo(rutaExportarVentas({ estado }), `ventas-${dayjs().format('YYYY-MM-DD')}.xlsx`).catch(
      setErrorDescarga,
    )
  }
  const confirmar = useConfirmarVenta()
  const anular = useAnularVenta()

  const columnas: TableProps<VentaResponse>['columns'] = [
    {
      title: 'Documento',
      dataIndex: 'id',
      className: 'num',
      render: (id: string) => <strong>{referenciaOrden(id)}</strong>,
    },
    { title: 'Cliente', dataIndex: 'clienteNombre' },
    {
      title: 'Fecha',
      dataIndex: 'fecha',
      className: 'num',
      render: (fecha: string) => fechaHora(fecha),
    },
    {
      title: 'Estado',
      key: 'estado',
      render: (_, venta) => (
        <>
          <EstadoVentaTag estadoId={venta.estadoId} />
          {venta.estadoAprobacionGerenciaId === GERENCIA.pendiente && (
            <div className="texto-secundario">Precio esperando a Gerencia</div>
          )}
          {venta.estadoAprobacionGerenciaId === GERENCIA.rechazado && (
            <div className="texto-secundario">Precio rechazado por Gerencia</div>
          )}
        </>
      ),
    },
    { title: 'Ítems', dataIndex: 'cantidadItems', align: 'right', className: 'num' },
    {
      title: 'Total',
      dataIndex: 'total',
      align: 'right',
      className: 'num',
      render: (total: number) => soles(total),
    },
    {
      title: 'Saldo',
      key: 'saldo',
      align: 'right',
      // Solo una venta confirmada se cobra: la cotización y la anulada no deben nada.
      render: (_, venta) =>
        venta.estadoId === ESTADO_VENTA.confirmada ? (
          <Space size={8}>
            <span className="num">{soles(venta.saldo ?? 0)}</span>
            <EstadoPagoTag estado={venta.estadoPago} />
          </Space>
        ) : (
          <span className="texto-secundario">—</span>
        ),
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, venta) => (
        <Space size="small">
          <Button type="link" onClick={() => setVentaVista(venta.id)}>
            Ver
          </Button>
          {puedeVender && puedeConfirmar(venta.estadoId) && !precioBloqueado(venta) && (
            <Popconfirm
              title="Confirmar la venta"
              description="Descuenta el stock de los productos."
              okText="Confirmar"
              cancelText="Cancelar"
              onConfirm={() => confirmar.mutate(venta.id)}
            >
              <Button type="link">Confirmar</Button>
            </Popconfirm>
          )}
          {puedeAnularVentas && puedeAnular(venta.estadoId) && (
            <Popconfirm
              title="Anular"
              description={
                venta.estadoId === ESTADO_VENTA.confirmada
                  ? 'Los productos vuelven al stock.'
                  : 'La cotización queda anulada.'
              }
              okText="Anular"
              cancelText="Cancelar"
              onConfirm={() => anular.mutate(venta.id)}
            >
              <Button type="link">Anular</Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ]

  return (
    <>
      <BarraSuperior
        titulo="Ventas y cotizaciones"
        acciones={
          <>
            <Button onClick={exportar}>Exportar a Excel</Button>
            {puedeVender && (
              <Button type="primary" onClick={() => setModalNueva(true)}>
                Nueva venta
              </Button>
            )}
          </>
        }
      />
      <div className="pagina">
        <section>
          <AvisoError error={ventas.error ?? sinComprobante.error ?? confirmar.error ?? anular.error ?? errorDescarga} />
          <Tabs
            activeKey={vista}
            onChange={setVista}
            items={[
              {
                key: 'todas',
                label: 'Todas',
                children: (
                  <>
                    <div className="filtros">
                      <Select<number>
                        id="filtro-estado-venta"
                        allowClear
                        placeholder="Estado"
                        value={estado}
                        onChange={setEstado}
                        options={opcionesEstado}
                        style={{ width: 220 }}
                      />
                    </div>
                    <Table
                      rowKey="id"
                      columns={columnas}
                      dataSource={ventas.data ?? []}
                      pagination={false}
                      loading={ventas.isPending}
                      locale={{ emptyText: 'Todavía no hay ventas ni cotizaciones' }}
                    />
                  </>
                ),
              },
              {
                key: 'sin-comprobante',
                label: `Sin comprobante${sinComprobante.data?.length ? ` (${sinComprobante.data.length})` : ''}`,
                children: (
                  <Table
                    rowKey="id"
                    columns={columnas}
                    dataSource={sinComprobante.data ?? []}
                    pagination={false}
                    loading={sinComprobante.isPending}
                    locale={{ emptyText: 'Todas las ventas confirmadas tienen comprobante' }}
                  />
                ),
              },
            ]}
          />
          <p className="texto-secundario" style={{ marginTop: 16 }}>
            Los pagos y el comprobante se registran desde el detalle de cada venta confirmada. El comprobante se puede
            emitir después: no duplica la venta, la caja ni el stock.
          </p>
        </section>
      </div>
      <ModalVenta
        abierto={modalNueva}
        onCerrar={() => setModalNueva(false)}
        // Una venta directa nace confirmada: se abre para cobrarla y registrar el comprobante.
        onCreada={(venta) => {
          if (venta.estadoId === ESTADO_VENTA.confirmada) setVentaVista(venta.id)
        }}
      />
      <ModalDetalleVenta
        abierto={ventaVista !== null}
        ventaId={ventaVista}
        onCerrar={() => setVentaVista(null)}
      />
    </>
  )
}
