import { useState } from 'react'
import { Button, Select, Table, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoPedidoLimaTag } from '../components/EstadoPedidoLimaTag'
import { ModalPedidoLima } from '../components/ModalPedidoLima'
import { ModalDetallePedidoLima } from '../components/ModalDetallePedidoLima'
import { nombresEstadoPedidoLima, rutaPedidosLima, usePedidosLima } from '../api/pedidosLima'
import { ESTADO_PEDIDO_LIMA, type EstadoPedidoLima, type PedidoLimaResponse } from '../api/tipos'
import { GERENCIA } from '../api/ordenes'
import { useSesion } from '../auth/sesion'
import { useClientes } from '../api/clientes'
import { PERMISOS } from '../auth/acceso'
import { descargarArchivo } from '../utils/descarga'
import { diaLocal, entero, fechaCorta, fechaHora, soles } from '../utils/formato'

const opcionesEstado = Object.entries(nombresEstadoPedidoLima).map(([valor, etiqueta]) => ({
  value: valor as EstadoPedidoLima,
  label: etiqueta,
}))

export function PedidosLimaPage() {
  const [estado, setEstado] = useState<EstadoPedidoLima>()
  const [clienteId, setClienteId] = useState<string>()
  const [modalNuevo, setModalNuevo] = useState(false)
  const [pedidoVisto, setPedidoVisto] = useState<string | null>(null)
  const [exportando, setExportando] = useState(false)
  const [errorExportar, setErrorExportar] = useState<unknown>(null)

  const { tienePermiso } = useSesion()
  const puedeCrear = tienePermiso(PERMISOS.pedidosLimaCrear)
  const veClientes = tienePermiso(PERMISOS.clientesVer)
  const clientes = useClientes(veClientes)
  const pedidos = usePedidosLima({ estado, clienteId })

  const exportar = async () => {
    setExportando(true)
    setErrorExportar(null)
    try {
      await descargarArchivo(
        rutaPedidosLima({ estado, clienteId }, '/pedidos-lima/exportar-excel'),
        `pedidos-lima-${diaLocal()}.xlsx`,
      )
    } catch (error) {
      setErrorExportar(error)
    } finally {
      setExportando(false)
    }
  }

  const columnas: TableProps<PedidoLimaResponse>['columns'] = [
    {
      title: 'Pedido',
      dataIndex: 'numeroPedido',
      className: 'num',
      render: (numero: string) => <strong>{numero}</strong>,
    },
    { title: 'Cliente', dataIndex: 'clienteNombre' },
    {
      title: 'Registrado',
      dataIndex: 'fecha',
      className: 'num',
      render: (fecha: string) => fechaHora(fecha),
    },
    {
      title: 'Estado',
      key: 'estado',
      render: (_, pedido) => (
        <>
          <EstadoPedidoLimaTag estado={pedido.estado} />
          {pedido.estadoAprobacionGerenciaId === GERENCIA.pendiente && (
            <div className="texto-secundario">Precio esperando a Gerencia</div>
          )}
          {pedido.estadoAprobacionGerenciaId === GERENCIA.rechazado && (
            <div className="texto-secundario">Precio rechazado por Gerencia</div>
          )}
        </>
      ),
    },
    {
      title: 'Llegada estimada',
      dataIndex: 'fechaEstimadaLlegada',
      className: 'num',
      render: (fecha: string | null) => fechaCorta(fecha),
    },
    {
      title: 'Repuestos',
      key: 'repuestos',
      align: 'right',
      className: 'num',
      render: (_, pedido) => entero(pedido.detalles.reduce((suma, detalle) => suma + detalle.cantidad, 0)),
    },
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
      className: 'num',
      render: (_, pedido) =>
        pedido.estado === ESTADO_PEDIDO_LIMA.cancelado ? '—' : soles(pedido.saldo ?? pedido.total),
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, pedido) => (
        <Button type="link" onClick={() => setPedidoVisto(pedido.id)}>
          Ver
        </Button>
      ),
    },
  ]

  return (
    <>
      <BarraSuperior
        titulo="Pedidos a Lima"
        acciones={
          <>
            <Button onClick={exportar} loading={exportando}>
              Exportar a Excel
            </Button>
            {puedeCrear && (
              <Button type="primary" onClick={() => setModalNuevo(true)}>
                Nuevo pedido
              </Button>
            )}
          </>
        }
      />
      <div className="pagina">
        <section>
          <AvisoError error={pedidos.error ?? errorExportar} />
          <div className="filtros">
            <Select<EstadoPedidoLima>
              id="filtro-estado-pedido"
              allowClear
              placeholder="Estado"
              value={estado}
              onChange={setEstado}
              options={opcionesEstado}
              style={{ width: 220 }}
            />
            {veClientes && (
              <Select<string>
                id="filtro-cliente-pedido"
                allowClear
                showSearch
                optionFilterProp="label"
                placeholder="Cliente"
                value={clienteId}
                onChange={setClienteId}
                loading={clientes.isPending}
                options={(clientes.data ?? []).map((cliente) => ({ value: cliente.id, label: cliente.nombreCompleto }))}
                style={{ width: 260 }}
              />
            )}
          </div>
          <Table
            rowKey="id"
            columns={columnas}
            dataSource={pedidos.data ?? []}
            pagination={{ pageSize: 20, hideOnSinglePage: true }}
            loading={pedidos.isPending}
            locale={{ emptyText: 'Todavía no hay pedidos a Lima' }}
            onRow={(pedido) => ({ onDoubleClick: () => setPedidoVisto(pedido.id) })}
          />
        </section>
      </div>
      <ModalPedidoLima
        abierto={modalNuevo}
        onCerrar={() => setModalNuevo(false)}
        onCreado={(pedido) => setPedidoVisto(pedido.id)}
      />
      <ModalDetallePedidoLima pedidoId={pedidoVisto} onCerrar={() => setPedidoVisto(null)} />
    </>
  )
}
