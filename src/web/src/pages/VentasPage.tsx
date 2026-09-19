import { useState } from 'react'
import { Button, Popconfirm, Select, Space, Table, Tag, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalVenta } from '../components/ModalVenta'
import { ModalDetalleVenta } from '../components/ModalDetalleVenta'
import {
  ESTADO_VENTA,
  nombresEstadoVenta,
  puedeAnular,
  puedeConfirmar,
  useAnularVenta,
  useConfirmarVenta,
  useVentas,
} from '../api/ventas'
import type { VentaResponse } from '../api/tipos'
import { colores } from '../theme/tokens'
import { fechaHora, referenciaOrden, soles } from '../utils/formato'

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

export function VentasPage() {
  const [estado, setEstado] = useState<number>()
  const [modalNueva, setModalNueva] = useState(false)
  const [ventaVista, setVentaVista] = useState<string | null>(null)

  const ventas = useVentas({ estado })
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
      render: (_, venta) => <EstadoVentaTag estadoId={venta.estadoId} />,
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
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, venta) => (
        <Space size="small">
          <Button type="link" onClick={() => setVentaVista(venta.id)}>
            Ver
          </Button>
          {puedeConfirmar(venta.estadoId) && (
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
          {puedeAnular(venta.estadoId) && (
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
          <Button type="primary" onClick={() => setModalNueva(true)}>
            Nueva venta
          </Button>
        }
      />
      <div className="pagina">
        <section>
          <AvisoError error={ventas.error ?? confirmar.error ?? anular.error} />
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
          <p className="texto-secundario" style={{ marginTop: 16 }}>
            El registro administrativo de comprobantes está disponible desde el detalle de cada venta confirmada.
          </p>
        </section>
      </div>
      <ModalVenta abierto={modalNueva} onCerrar={() => setModalNueva(false)} />
      <ModalDetalleVenta
        abierto={ventaVista !== null}
        ventaId={ventaVista}
        onCerrar={() => setVentaVista(null)}
      />
    </>
  )
}
