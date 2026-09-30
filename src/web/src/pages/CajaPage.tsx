import { useState } from 'react'
import { Button, Modal, Table, Tabs, Tag, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { Indicadores } from '../components/Indicadores'
import { ModalAperturaCaja, ModalCierreCaja, ModalMovimientoCaja } from '../components/ModalesCaja'
import { useCaja, useCajaActual, useHistorialCajas } from '../api/caja'
import {
  ESTADO_CAJA,
  TIPO_MOVIMIENTO_CAJA,
  type CajaChicaResponse,
  type MovimientoCajaResponse,
} from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { colores } from '../theme/tokens'
import { fechaHora, importe, soles } from '../utils/formato'

const recientesPrimero = (a: MovimientoCajaResponse, b: MovimientoCajaResponse) =>
  new Date(b.fecha).getTime() - new Date(a.fecha).getTime()

const columnasMovimientos: TableProps<MovimientoCajaResponse>['columns'] = [
  { title: 'Fecha', dataIndex: 'fecha', className: 'num', render: (fecha: string) => fechaHora(fecha) },
  {
    title: 'Tipo',
    key: 'tipo',
    render: (_, movimiento) =>
      movimiento.tipo === TIPO_MOVIMIENTO_CAJA.egreso ? (
        <Tag style={{ marginInlineEnd: 0, color: colores.acento700, borderColor: colores.acento600, background: 'transparent' }}>
          Egreso
        </Tag>
      ) : (
        <Tag style={{ marginInlineEnd: 0 }}>Ingreso</Tag>
      ),
  },
  {
    title: 'Concepto',
    key: 'concepto',
    render: (_, movimiento) => (
      <>
        <div>{movimiento.concepto}</div>
        {movimiento.referencia && <div className="texto-secundario">{movimiento.referencia}</div>}
      </>
    ),
  },
  { title: 'Responsable', dataIndex: 'usuarioNombre', render: (nombre: string | null) => nombre ?? '—' },
  {
    title: 'Monto',
    key: 'monto',
    align: 'right',
    className: 'num',
    render: (_, movimiento) =>
      movimiento.tipo === TIPO_MOVIMIENTO_CAJA.egreso ? `− ${importe(movimiento.monto)}` : `+ ${importe(movimiento.monto)}`,
  },
]

function TablaMovimientos({ movimientos, cargando }: Readonly<{ movimientos: MovimientoCajaResponse[]; cargando?: boolean }>) {
  return (
    <Table
      rowKey="id"
      columns={columnasMovimientos}
      dataSource={[...movimientos].sort(recientesPrimero)}
      pagination={movimientos.length > 20 ? { pageSize: 20 } : false}
      loading={cargando}
      locale={{ emptyText: 'Todavía no hay movimientos en esta caja' }}
    />
  )
}

export function CajaPage() {
  const { tienePermiso } = useSesion()
  const puedeAbrir = tienePermiso(PERMISOS.cajaAperturar)
  const puedeCerrar = tienePermiso(PERMISOS.cajaCerrar)
  const puedeIngresar = tienePermiso(PERMISOS.cajaRegistrarIngreso)
  const puedeEgresar = tienePermiso(PERMISOS.cajaRegistrarEgreso)

  const actual = useCajaActual()
  const historial = useHistorialCajas()
  const [pestana, setPestana] = useState('actual')
  const [modalApertura, setModalApertura] = useState(false)
  const [modalCierre, setModalCierre] = useState(false)
  const [tipoMovimiento, setTipoMovimiento] = useState<number | null>(null)
  const [cajaVista, setCajaVista] = useState<string | null>(null)
  const detalleVisto = useCaja(cajaVista)

  const caja = actual.data?.caja ?? null
  const abierta = Boolean(actual.data?.tieneCajaAbierta && caja)

  const columnasHistorial: TableProps<CajaChicaResponse>['columns'] = [
    { title: 'Apertura', dataIndex: 'fechaApertura', className: 'num', render: (fecha: string) => fechaHora(fecha) },
    { title: 'Cierre', dataIndex: 'fechaCierre', className: 'num', render: (fecha: string | null) => fechaHora(fecha) },
    { title: 'Inicial', dataIndex: 'montoApertura', align: 'right', className: 'num', render: (monto: number) => importe(monto) },
    { title: 'Ingresos', dataIndex: 'totalIngresos', align: 'right', className: 'num', render: (monto: number) => importe(monto) },
    { title: 'Egresos', dataIndex: 'totalEgresos', align: 'right', className: 'num', render: (monto: number) => importe(monto) },
    {
      title: 'Saldo final',
      key: 'saldo',
      align: 'right',
      className: 'num',
      render: (_, registro) => importe(registro.montoCierre ?? registro.saldoCalculado),
    },
    {
      title: 'Responsables',
      key: 'responsables',
      render: (_, registro) =>
        [registro.usuarioAperturaNombre, registro.usuarioCierreNombre]
          .filter((nombre, indice, lista) => nombre && lista.indexOf(nombre) === indice)
          .join(' · ') || '—',
    },
    {
      title: '',
      key: 'estado',
      render: (_, registro) =>
        registro.estado === ESTADO_CAJA.abierta ? <Tag style={{ marginInlineEnd: 0 }}>Abierta</Tag> : null,
    },
    {
      title: '',
      key: 'ver',
      align: 'right',
      render: (_, registro) => (
        <Button type="link" onClick={() => setCajaVista(registro.id)}>
          Movimientos
        </Button>
      ),
    },
  ]

  return (
    <>
      <BarraSuperior
        titulo="Caja chica"
        acciones={
          <>
            {abierta && puedeIngresar && (
              <Button onClick={() => setTipoMovimiento(TIPO_MOVIMIENTO_CAJA.ingreso)}>Registrar ingreso</Button>
            )}
            {abierta && puedeEgresar && (
              <Button type="primary" onClick={() => setTipoMovimiento(TIPO_MOVIMIENTO_CAJA.egreso)}>
                Registrar egreso
              </Button>
            )}
            {abierta && puedeCerrar && (
              <Button danger onClick={() => setModalCierre(true)}>
                Cerrar caja
              </Button>
            )}
            {!abierta && actual.isSuccess && puedeAbrir && (
              <Button type="primary" onClick={() => setModalApertura(true)}>
                Abrir caja
              </Button>
            )}
          </>
        }
      />
      <div className="pagina">
        <AvisoError error={actual.error ?? historial.error} />
        <Tabs
          activeKey={pestana}
          onChange={setPestana}
          items={[
            {
              key: 'actual',
              label: 'Caja actual',
              children:
                abierta && caja ? (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
                    <Indicadores
                      items={[
                        { etiqueta: 'Saldo', valor: soles(caja.saldoCalculado), destacado: true, compacto: true },
                        { etiqueta: 'Monto inicial', valor: soles(caja.montoApertura), compacto: true },
                        { etiqueta: 'Ingresos', valor: soles(caja.totalIngresos), compacto: true },
                        { etiqueta: 'Egresos', valor: soles(caja.totalEgresos), compacto: true },
                      ]}
                    />
                    <p className="texto-secundario">
                      Abierta el {fechaHora(caja.fechaApertura)}
                      {caja.usuarioAperturaNombre ? ` por ${caja.usuarioAperturaNombre}` : ''}.
                      {caja.observacionesApertura ? ` ${caja.observacionesApertura}` : ''}
                    </p>
                    <TablaMovimientos movimientos={caja.movimientos} />
                  </div>
                ) : (
                  !actual.isPending && (
                    <p className="texto-secundario">
                      No hay una caja abierta.
                      {puedeAbrir ? ' Ábrela con el monto inicial para registrar ingresos y egresos.' : ''}
                    </p>
                  )
                ),
            },
            {
              key: 'historial',
              label: 'Historial',
              children: (
                <Table
                  rowKey="id"
                  columns={columnasHistorial}
                  dataSource={historial.data ?? []}
                  pagination={false}
                  loading={historial.isPending}
                  locale={{ emptyText: 'Todavía no hay cajas registradas' }}
                />
              ),
            },
          ]}
        />
      </div>

      <ModalAperturaCaja abierto={modalApertura} onCerrar={() => setModalApertura(false)} />
      {caja && <ModalCierreCaja abierto={modalCierre} caja={caja} onCerrar={() => setModalCierre(false)} />}
      <ModalMovimientoCaja
        abierto={tipoMovimiento !== null}
        tipo={tipoMovimiento ?? TIPO_MOVIMIENTO_CAJA.ingreso}
        saldo={caja?.saldoCalculado ?? 0}
        onCerrar={() => setTipoMovimiento(null)}
      />
      <Modal
        title={detalleVisto.data ? `Caja del ${fechaHora(detalleVisto.data.fechaApertura)}` : 'Caja'}
        open={cajaVista !== null}
        onCancel={() => setCajaVista(null)}
        footer={null}
        width={860}
        destroyOnHidden
      >
        <AvisoError error={detalleVisto.error} />
        <TablaMovimientos movimientos={detalleVisto.data?.movimientos ?? []} cargando={detalleVisto.isPending} />
      </Modal>
    </>
  )
}
