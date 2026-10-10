import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import type { Dayjs } from 'dayjs'
import { Button, DatePicker, Input, Popconfirm, Select, Table, Tabs, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { Indicadores } from '../components/Indicadores'
import { EstadoCompraTag } from '../components/EstadoCompraTag'
import { ModalDetalleCompra } from '../components/ModalDetalleCompra'
import { ModalProveedor } from '../components/ModalProveedor'
import {
  ESTADO_PAGO_COMPRA,
  nombresTipoComprobanteCompra,
  rutaCompras,
  useCompras,
  useEliminarProveedor,
  useProveedores,
  type FiltrosCompras,
} from '../api/compras'
import { ESTADO_COMPRA, MONEDA_COMPRA, type CompraResumenResponse, type ProveedorResponse } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { descargarArchivo } from '../utils/descarga'
import { diaLocal, fechaDia, soles } from '../utils/formato'
import { montoEnMoneda, numeroComprobante, redondear } from '../utils/compras'

/** La situación que se elige en la lista, traducida a los filtros de la API. */
const SITUACIONES: Record<string, { etiqueta: string; filtro: Pick<FiltrosCompras, 'estado' | 'estadoPago'> }> = {
  porPagar: { etiqueta: 'Por pagar', filtro: { estadoPago: ESTADO_PAGO_COMPRA.porPagar } },
  vencidas: { etiqueta: 'Vencidas', filtro: { estadoPago: ESTADO_PAGO_COMPRA.vencida } },
  pagadas: { etiqueta: 'Pagadas', filtro: { estadoPago: ESTADO_PAGO_COMPRA.pagada } },
  anuladas: { etiqueta: 'Anuladas', filtro: { estado: ESTADO_COMPRA.anulada } },
}

function ListaCompras({ onVer }: Readonly<{ onVer: (id: string) => void }>) {
  const [situacion, setSituacion] = useState<string>()
  const [proveedorId, setProveedorId] = useState<string>()
  const [busqueda, setBusqueda] = useState('')
  const [rango, setRango] = useState<[Dayjs | null, Dayjs | null] | null>(null)
  const [exportando, setExportando] = useState(false)
  const [errorExportar, setErrorExportar] = useState<unknown>(null)

  const filtros: FiltrosCompras = {
    ...(situacion ? SITUACIONES[situacion].filtro : {}),
    proveedorId,
    busqueda: busqueda || undefined,
    fechaDesde: rango?.[0]?.format('YYYY-MM-DD'),
    fechaHasta: rango?.[1]?.format('YYYY-MM-DD'),
  }
  const compras = useCompras(filtros)
  const porPagar = useCompras({ estadoPago: ESTADO_PAGO_COMPRA.porPagar })
  const proveedores = useProveedores()

  const deudas = porPagar.data ?? []
  const deudaSoles = redondear(deudas.filter((c) => c.moneda === MONEDA_COMPRA.pen).reduce((s, c) => s + c.saldo, 0))
  const deudaDolares = redondear(deudas.filter((c) => c.moneda === MONEDA_COMPRA.usd).reduce((s, c) => s + c.saldo, 0))
  const vencidas = deudas.filter((c) => c.vencida).length

  const exportar = async () => {
    setExportando(true)
    setErrorExportar(null)
    try {
      await descargarArchivo(rutaCompras(filtros, '/compras/exportar-excel'), `compras-${diaLocal()}.xlsx`)
    } catch (error) {
      setErrorExportar(error)
    } finally {
      setExportando(false)
    }
  }

  const columnas: TableProps<CompraResumenResponse>['columns'] = [
    {
      title: 'Compra',
      dataIndex: 'numeroCompra',
      className: 'num',
      render: (numero: string) => <strong>{numero}</strong>,
    },
    { title: 'Emisión', dataIndex: 'fechaEmision', className: 'num', render: (dia: string) => fechaDia(dia) },
    {
      title: 'Comprobante',
      key: 'comprobante',
      render: (_, compra) => (
        <>
          <div>{numeroComprobante(compra)}</div>
          <div className="texto-secundario">{nombresTipoComprobanteCompra[compra.tipoComprobante]}</div>
        </>
      ),
    },
    {
      title: 'Proveedor',
      key: 'proveedor',
      render: (_, compra) => (
        <>
          <div>{compra.proveedorNombre}</div>
          {compra.numeroPedidoLima && <div className="texto-secundario">Pedido {compra.numeroPedidoLima}</div>}
        </>
      ),
    },
    {
      title: 'Total',
      key: 'total',
      align: 'right',
      className: 'num',
      render: (_, compra) => montoEnMoneda(compra.moneda, compra.total),
    },
    {
      title: 'Saldo',
      key: 'saldo',
      align: 'right',
      className: 'num',
      render: (_, compra) =>
        compra.estado === ESTADO_COMPRA.anulada ? '—' : montoEnMoneda(compra.moneda, compra.saldo),
    },
    {
      title: 'Vence',
      dataIndex: 'fechaVencimiento',
      className: 'num',
      render: (dia: string | null) => fechaDia(dia),
    },
    { title: 'Estado', key: 'estado', render: (_, compra) => <EstadoCompraTag compra={compra} /> },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, compra) => (
        <Button type="link" onClick={() => onVer(compra.id)}>
          Ver
        </Button>
      ),
    },
  ]

  return (
    <div className="secciones">
      <Indicadores
        tamano="mediano"
        items={[
          { etiqueta: 'Por pagar en soles', valor: soles(deudaSoles), destacado: true, compacto: true },
          ...(deudaDolares > 0
            ? [{ etiqueta: 'Por pagar en dólares', valor: montoEnMoneda(MONEDA_COMPRA.usd, deudaDolares), compacto: true }]
            : []),
          { etiqueta: 'Compras vencidas', valor: vencidas },
        ]}
      />
      <section>
        <AvisoError error={compras.error ?? porPagar.error ?? errorExportar} />
        <div className="filtros">
          <Input.Search
            allowClear
            placeholder="N.° de compra, comprobante o proveedor"
            onSearch={(texto) => setBusqueda(texto.trim())}
            style={{ width: 300 }}
          />
          <Select<string>
            allowClear
            placeholder="Situación"
            value={situacion}
            onChange={setSituacion}
            options={Object.entries(SITUACIONES).map(([valor, { etiqueta }]) => ({ value: valor, label: etiqueta }))}
            style={{ width: 170 }}
          />
          <Select<string>
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder="Proveedor"
            value={proveedorId}
            onChange={setProveedorId}
            loading={proveedores.isPending}
            options={(proveedores.data ?? []).map((p) => ({ value: p.id, label: p.razonSocial }))}
            style={{ width: 240 }}
          />
          <DatePicker.RangePicker
            format="DD/MM/YYYY"
            placeholder={['Emitida desde', 'hasta']}
            value={rango}
            onChange={(valor) => setRango(valor)}
            style={{ width: 280 }}
          />
          <Button onClick={exportar} loading={exportando}>
            Exportar a Excel
          </Button>
        </div>
        <Table
          rowKey="id"
          columns={columnas}
          dataSource={compras.data ?? []}
          pagination={{ pageSize: 20, hideOnSinglePage: true }}
          loading={compras.isPending}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: 'No hay compras con estos filtros' }}
          onRow={(compra) => ({ onDoubleClick: () => onVer(compra.id) })}
        />
      </section>
    </div>
  )
}

function ListaProveedores({ puedeEditar }: Readonly<{ puedeEditar: boolean }>) {
  const [busqueda, setBusqueda] = useState('')
  const [proveedorEditado, setProveedorEditado] = useState<ProveedorResponse | null>(null)
  const [nuevo, setNuevo] = useState(false)
  const proveedores = useProveedores()
  const eliminar = useEliminarProveedor()

  const texto = busqueda.trim().toLowerCase()
  const filtrados = (proveedores.data ?? []).filter(
    (p) => !texto || p.razonSocial.toLowerCase().includes(texto) || p.numeroDocumento.includes(texto),
  )

  const columnas: TableProps<ProveedorResponse>['columns'] = [
    { title: 'Proveedor', dataIndex: 'razonSocial', render: (nombre: string) => <strong>{nombre}</strong> },
    { title: 'Documento', dataIndex: 'numeroDocumento', className: 'num' },
    { title: 'Teléfono', dataIndex: 'telefono', render: (valor: string | null) => valor ?? '—' },
    { title: 'Contacto', dataIndex: 'contacto', render: (valor: string | null) => valor ?? '—' },
    { title: 'Correo', dataIndex: 'email', render: (valor: string | null) => valor ?? '—' },
    ...(puedeEditar
      ? [
          {
            title: '',
            key: 'acciones',
            align: 'right' as const,
            render: (_: unknown, proveedor: ProveedorResponse) => (
              <span className="sin-salto">
                <Button type="link" onClick={() => setProveedorEditado(proveedor)}>
                  Editar
                </Button>
                <Popconfirm
                  title="¿Dar de baja este proveedor?"
                  description="Sus compras lo siguen mostrando."
                  okText="Dar de baja"
                  cancelText="Volver"
                  onConfirm={() => eliminar.mutateAsync(proveedor.id)}
                >
                  <Button type="link">Dar de baja</Button>
                </Popconfirm>
              </span>
            ),
          },
        ]
      : []),
  ]

  return (
    <section>
      <AvisoError error={proveedores.error ?? eliminar.error} />
      <div className="filtros">
        <Input.Search
          allowClear
          placeholder="Razón social o documento"
          onChange={(evento) => setBusqueda(evento.target.value)}
          style={{ width: 300 }}
        />
        {puedeEditar && (
          <Button type="primary" onClick={() => setNuevo(true)}>
            Nuevo proveedor
          </Button>
        )}
      </div>
      <Table
        rowKey="id"
        columns={columnas}
        dataSource={filtrados}
        pagination={{ pageSize: 20, hideOnSinglePage: true }}
        loading={proveedores.isPending}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'Todavía no hay proveedores' }}
      />
      <ModalProveedor
        abierto={nuevo || proveedorEditado !== null}
        proveedor={proveedorEditado}
        onCerrar={() => {
          setNuevo(false)
          setProveedorEditado(null)
        }}
      />
    </section>
  )
}

export function ComprasPage() {
  const navigate = useNavigate()
  const [parametros, setParametros] = useSearchParams()
  const [pestana, setPestana] = useState('compras')
  const { tienePermiso } = useSesion()
  const puedeRegistrar = tienePermiso(PERMISOS.comprasRegistrar)

  // ?ver=<id> abre el detalle, como al volver de registrar una compra.
  const compraVista = parametros.get('ver')
  const verCompra = (id: string | null) => {
    const siguientes = new URLSearchParams(parametros)
    if (id) siguientes.set('ver', id)
    else siguientes.delete('ver')
    setParametros(siguientes, { replace: true })
  }

  return (
    <>
      <BarraSuperior
        titulo="Compras"
        acciones={
          puedeRegistrar && (
            <Button type="primary" onClick={() => navigate('/compras/nueva')}>
              Nueva compra
            </Button>
          )
        }
      />
      <div className="pagina">
        <Tabs
          activeKey={pestana}
          onChange={setPestana}
          items={[
            { key: 'compras', label: 'Compras', children: <ListaCompras onVer={verCompra} /> },
            { key: 'proveedores', label: 'Proveedores', children: <ListaProveedores puedeEditar={puedeRegistrar} /> },
          ]}
        />
      </div>
      <ModalDetalleCompra compraId={compraVista} onCerrar={() => verCompra(null)} />
    </>
  )
}
