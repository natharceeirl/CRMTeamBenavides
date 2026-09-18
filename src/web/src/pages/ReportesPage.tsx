import { useState } from 'react'
import { Segmented, Table, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { Indicadores } from '../components/Indicadores'
import {
  rangoDelPeriodo,
  useReporteOrdenes,
  useReporteVentas,
  useResumenDashboard,
  useStockBajo,
  type Periodo,
} from '../api/reportes'
import type {
  OrdenServicioReporteResponse,
  StockBajoResponse,
  VentaReporteResponse,
} from '../api/tipos'
import { entero, fechaHora, referenciaOrden, soles } from '../utils/formato'

const columnasOrdenes: TableProps<OrdenServicioReporteResponse>['columns'] = [
  {
    title: 'Orden',
    dataIndex: 'id',
    className: 'num',
    render: (id: string) => referenciaOrden(id),
  },
  { title: 'Cliente', dataIndex: 'clienteNombre' },
  {
    title: 'Unidad',
    key: 'unidad',
    render: (_, orden) => `${orden.vehiculoMarca} ${orden.vehiculoModelo} · ${orden.vehiculoPlaca}`,
  },
  {
    title: 'Técnico',
    dataIndex: 'tecnicoNombre',
    render: (nombre: string | null) => nombre ?? 'Sin asignar',
  },
  {
    title: 'Estado',
    key: 'estado',
    render: (_, orden) => <EstadoOrdenApiTag estadoId={orden.estadoId} />,
  },
  {
    title: 'Ingreso',
    dataIndex: 'fechaApertura',
    className: 'num',
    render: (fecha: string) => fechaHora(fecha),
  },
  {
    title: 'Atención',
    dataIndex: 'tiempoAtencionHoras',
    align: 'right',
    className: 'num',
    render: (horas: number | null) => (horas === null ? 'En curso' : `${entero(Math.round(horas))} h`),
  },
]

const columnasVentas: TableProps<VentaReporteResponse>['columns'] = [
  {
    title: 'Documento',
    dataIndex: 'id',
    className: 'num',
    render: (id: string) => referenciaOrden(id),
  },
  { title: 'Cliente', dataIndex: 'clienteNombre' },
  { title: 'Estado', dataIndex: 'estado' },
  {
    title: 'Fecha',
    dataIndex: 'fecha',
    className: 'num',
    render: (fecha: string) => fechaHora(fecha),
  },
  {
    title: 'Total',
    dataIndex: 'total',
    align: 'right',
    className: 'num',
    render: (total: number) => soles(total),
  },
]

const columnasStock: TableProps<StockBajoResponse>['columns'] = [
  { title: 'Código', dataIndex: 'codigo', className: 'num' },
  { title: 'Producto', dataIndex: 'nombre' },
  { title: 'Categoría', dataIndex: 'categoriaNombre' },
  { title: 'Stock', dataIndex: 'stockActual', align: 'right', className: 'num' },
  { title: 'Mínimo', dataIndex: 'stockMinimo', align: 'right', className: 'num' },
  {
    title: 'Faltan',
    dataIndex: 'diferencia',
    align: 'right',
    className: 'num',
    render: (diferencia: number) => entero(Math.abs(diferencia)),
  },
]

export function ReportesPage() {
  const [periodo, setPeriodo] = useState<Periodo>('Semana')
  const rango = rangoDelPeriodo(periodo)

  const resumen = useResumenDashboard(rango)
  const ordenes = useReporteOrdenes(rango)
  const ventas = useReporteVentas(rango)
  const stockBajo = useStockBajo()

  const datos = resumen.data

  return (
    <>
      <BarraSuperior
        titulo="Reportes"
        acciones={
          <Segmented<Periodo> options={['Hoy', 'Semana', 'Mes']} value={periodo} onChange={setPeriodo} />
        }
      />
      <div className="pagina">
        <AvisoError error={resumen.error ?? ordenes.error ?? ventas.error ?? stockBajo.error} />
        <Indicadores
          items={[
            {
              etiqueta: 'Ventas confirmadas',
              valor: datos ? `S/ ${entero(Math.round(datos.ventas.montoConfirmadas))}` : '—',
              compacto: true,
            },
            {
              etiqueta: 'Ticket promedio',
              valor: datos ? `S/ ${entero(Math.round(datos.ventas.ticketPromedio))}` : '—',
              compacto: true,
            },
            { etiqueta: 'Órdenes entregadas', valor: datos?.ordenesServicio.entregada ?? '—' },
            { etiqueta: 'Clientes activos', valor: datos?.crmActivos.clientesActivos ?? '—' },
          ]}
        />

        <section>
          <div className="seccion-titulo">
            <h2>Órdenes de servicio del periodo</h2>
          </div>
          <Table
            rowKey="id"
            columns={columnasOrdenes}
            dataSource={ordenes.data ?? []}
            pagination={{ pageSize: 10 }}
            loading={ordenes.isPending}
            locale={{ emptyText: 'Sin órdenes en el periodo' }}
          />
        </section>

        <section>
          <div className="seccion-titulo">
            <h2>Ventas del periodo</h2>
          </div>
          <Table
            rowKey="id"
            columns={columnasVentas}
            dataSource={ventas.data ?? []}
            pagination={{ pageSize: 10 }}
            loading={ventas.isPending}
            locale={{ emptyText: 'Sin ventas en el periodo' }}
          />
        </section>

        <section>
          <div className="seccion-titulo">
            <h2>Productos con stock bajo</h2>
          </div>
          <p className="texto-secundario" style={{ marginBottom: 16 }}>
            Este listado no depende del periodo: es la foto del inventario ahora mismo.
          </p>
          <Table
            rowKey="productoId"
            columns={columnasStock}
            dataSource={stockBajo.data ?? []}
            pagination={false}
            loading={stockBajo.isPending}
            locale={{ emptyText: 'Ningún producto por debajo del mínimo' }}
          />
        </section>
      </div>
    </>
  )
}
