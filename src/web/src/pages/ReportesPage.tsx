import { useState, type ReactNode } from 'react'
import { Segmented, Select, Table, Tabs, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { Indicadores } from '../components/Indicadores'
import { GraficoArea } from '../components/GraficoArea'
import { GraficoBarras } from '../components/GraficoBarras'
import {
  rangoDelPeriodo,
  useRentabilidad,
  useReporteOrdenes,
  useReporteVentas,
  useResumenDashboard,
  useStockBajo,
  type Periodo,
} from '../api/reportes'
import { ESTADO_VENTA } from '../api/ventas'
import { useProductos } from '../api/inventario'
import type {
  OrdenServicioReporteResponse,
  RentabilidadReporteResponse,
  StockBajoResponse,
  VentaReporteResponse,
} from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { colores } from '../theme/tokens'
import { entero, fechaHora, nombreDeEnum, porcentaje, referenciaOrden, soles } from '../utils/formato'
import { serieDiaria } from '../utils/series'

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

type FilaPorTipo = RentabilidadReporteResponse['desglosePorTipo'][number]
type FilaOperacion = RentabilidadReporteResponse['detalleOperaciones'][number]
type FilaRepuesto = RentabilidadReporteResponse['rankingRepuestos'][number]

// Lo vendido bajo el costo se lee en rojo.
const enRojoSiNegativo = (valor: number, texto: string): ReactNode =>
  valor < 0 ? <span style={{ color: colores.acento700 }}>{texto}</span> : texto

// Las cuatro columnas de montos son las mismas en las tres tablas de rentabilidad.
function columnasMontos<T extends { utilidadBruta: number; margenPorcentual: number }>(
  ingreso: keyof T & string,
  costo: keyof T & string,
): NonNullable<TableProps<T>['columns']> {
  return [
    {
      title: 'Ingreso sin IGV',
      dataIndex: ingreso,
      align: 'right',
      className: 'num',
      render: (monto: number) => soles(monto),
    },
    {
      title: 'Costo',
      dataIndex: costo,
      align: 'right',
      className: 'num',
      render: (monto: number) => soles(monto),
    },
    {
      title: 'Utilidad',
      dataIndex: 'utilidadBruta',
      align: 'right',
      className: 'num',
      render: (monto: number) => enRojoSiNegativo(monto, soles(monto)),
    },
    {
      title: 'Margen',
      dataIndex: 'margenPorcentual',
      align: 'right',
      className: 'num',
      render: (valor: number) => enRojoSiNegativo(valor, porcentaje(valor)),
    },
  ]
}

const columnasPorTipo: TableProps<FilaPorTipo>['columns'] = [
  { title: 'Tipo', dataIndex: 'tipoItem', render: (tipo: string) => nombreDeEnum(tipo) },
  {
    title: 'Cantidad',
    dataIndex: 'cantidadItems',
    align: 'right',
    className: 'num',
    render: (cantidad: number) => entero(cantidad),
  },
  ...columnasMontos<FilaPorTipo>('ingresoNeto', 'costoHistoricoRegistrado'),
]

const columnasRepuestos: TableProps<FilaRepuesto>['columns'] = [
  { title: 'Código', dataIndex: 'codigo', className: 'num' },
  { title: 'Repuesto', dataIndex: 'descripcion' },
  {
    title: 'Unidades',
    dataIndex: 'unidadesVendidas',
    align: 'right',
    className: 'num',
    render: (cantidad: number) => entero(cantidad),
  },
  ...columnasMontos<FilaRepuesto>('ingresoNeto', 'costoHistorico'),
]

const columnasOperaciones: TableProps<FilaOperacion>['columns'] = [
  { title: 'Documento', dataIndex: 'numeroDocumento', className: 'num' },
  {
    title: 'Origen',
    dataIndex: 'documentoTipo',
    render: (tipo: string) => (tipo === 'OrdenServicio' ? 'Orden de servicio' : 'Venta'),
  },
  {
    title: 'Fecha',
    dataIndex: 'fecha',
    className: 'num',
    render: (fecha: string) => fechaHora(fecha),
  },
  { title: 'Cliente', dataIndex: 'clienteNombre' },
  ...columnasMontos<FilaOperacion>('ingresoNeto', 'costoHistoricoRegistrado'),
]

type PropsRentabilidad = {
  datos?: RentabilidadReporteResponse
  cargando: boolean
  productoId?: string
  onProducto: (productoId: string | undefined) => void
}

function Rentabilidad({ datos, cargando, productoId, onProducto }: Readonly<PropsRentabilidad>) {
  const resumen = datos?.resumen
  const productos = useProductos()
  return (
    <div className="secciones">
      <div>
        <div className="filtros">
          <Select<string>
            id="filtro-repuesto-rentabilidad"
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder="Todos los repuestos y servicios"
            value={productoId}
            onChange={onProducto}
            loading={productos.isPending}
            options={(productos.data ?? []).map((producto) => ({
              value: producto.id,
              label: `${producto.codigo} · ${producto.nombre}`,
            }))}
            style={{ width: 380 }}
          />
        </div>
        <Indicadores
          items={[
            {
              etiqueta: 'Ingresos sin IGV',
              valor: resumen ? soles(resumen.ingresosTotalesSinIgv) : '—',
              compacto: true,
            },
            { etiqueta: 'Costo', valor: resumen ? soles(resumen.costoTotalHistorico) : '—', compacto: true },
            {
              etiqueta: 'Utilidad bruta',
              valor: resumen ? soles(resumen.utilidadBrutaTotal) : '—',
              compacto: true,
              destacado: true,
            },
            { etiqueta: 'Margen', valor: resumen ? porcentaje(resumen.margenPorcentualGlobal) : '—' },
          ]}
        />
        <p className="texto-secundario" style={{ marginTop: 16 }}>
          Solo ventas confirmadas y sin IGV. El costo de cada repuesto es el que tenía el día de la venta.
        </p>
      </div>

      <section>
        <div className="seccion-titulo">
          <h2>Por tipo de ítem</h2>
        </div>
        <Table
          rowKey="tipoItem"
          columns={columnasPorTipo}
          dataSource={datos?.desglosePorTipo ?? []}
          pagination={false}
          loading={cargando}
        />
      </section>

      <section>
        <div className="seccion-titulo">
          <h2>Repuestos por utilidad</h2>
        </div>
        <Table
          rowKey="productoId"
          columns={columnasRepuestos}
          dataSource={datos?.rankingRepuestos ?? []}
          pagination={{ pageSize: 10 }}
          loading={cargando}
          locale={{ emptyText: 'Sin repuestos vendidos en el periodo' }}
        />
      </section>

      <section>
        <div className="seccion-titulo">
          <h2>Operaciones del periodo</h2>
        </div>
        <Table
          rowKey="operacionId"
          columns={columnasOperaciones}
          dataSource={datos?.detalleOperaciones ?? []}
          pagination={{ pageSize: 10 }}
          loading={cargando}
          locale={{ emptyText: 'Sin ventas confirmadas en el periodo' }}
        />
      </section>
    </div>
  )
}

export function ReportesPage() {
  const [periodo, setPeriodo] = useState<Periodo>('Semana')
  const rango = rangoDelPeriodo(periodo)
  // La rentabilidad muestra costos y utilidad: solo Gerencia tiene este permiso.
  const veFinancieros = useSesion().tienePermiso(PERMISOS.reportesVerFinancieros)

  const resumen = useResumenDashboard(rango)
  const ordenes = useReporteOrdenes(rango)
  const ventas = useReporteVentas(rango)
  const stockBajo = useStockBajo()
  const [productoRentabilidad, setProductoRentabilidad] = useState<string>()
  const rentabilidad = useRentabilidad(rango, veFinancieros, productoRentabilidad)

  const datos = resumen.data

  // Las tablas de abajo son la vista completa; los gráficos son la lectura
  // rápida del mismo periodo.
  const ordenesPorDia = serieDiaria(
    ordenes.data ?? [],
    (orden) => orden.fechaApertura,
    () => 1,
    rango,
  )
  const ventasPorDia = serieDiaria(
    (ventas.data ?? []).filter((venta) => venta.estadoId !== ESTADO_VENTA.anulada),
    (venta) => venta.fecha,
    (venta) => venta.total,
    rango,
  )
  // Los ocho más cortos de stock: la lista completa sigue en la tabla.
  const faltantes = [...(stockBajo.data ?? [])]
    .sort((uno, otro) => otro.diferencia - uno.diferencia)
    .slice(0, 8)
    .map((producto) => ({ etiqueta: producto.nombre, valor: producto.diferencia }))

  // Dentro de las pestañas no llega el espaciado de .pagina: lo pone .secciones.
  const operacion = (
    <div className="secciones">
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
          <h2>Órdenes abiertas por día</h2>
        </div>
        <GraficoArea
          puntos={ordenesPorDia}
          nombreSerie="Órdenes abiertas"
          entero
          cargando={ordenes.isPending}
        />
      </section>

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
          <h2>Vendido por día</h2>
        </div>
        <p className="texto-secundario" style={{ marginBottom: 16 }}>
          Cotizaciones y ventas confirmadas. Las anuladas no suman.
        </p>
        <GraficoArea
          puntos={ventasPorDia}
          nombreSerie="Vendido"
          formatearValor={soles}
          formatearEje={(monto) => entero(Math.round(monto))}
          cargando={ventas.isPending}
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
        <div style={{ marginBottom: 24 }}>
          <GraficoBarras
            datos={faltantes}
            unidad="unidades por debajo del mínimo"
            cargando={stockBajo.isPending}
            vacio="Ningún producto por debajo del mínimo"
          />
        </div>
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
  )

  return (
    <>
      <BarraSuperior
        titulo="Reportes"
        acciones={
          <Segmented<Periodo> options={['Hoy', 'Semana', 'Mes']} value={periodo} onChange={setPeriodo} />
        }
      />
      <div className="pagina">
        <AvisoError
          error={resumen.error ?? ordenes.error ?? ventas.error ?? stockBajo.error ?? rentabilidad.error}
        />
        {veFinancieros ? (
          <Tabs
            items={[
              { key: 'operacion', label: 'Operación', children: operacion },
              {
                key: 'rentabilidad',
                label: 'Rentabilidad',
                children: (
                  <Rentabilidad
                    datos={rentabilidad.data}
                    cargando={rentabilidad.isPending}
                    productoId={productoRentabilidad}
                    onProducto={setProductoRentabilidad}
                  />
                ),
              },
            ]}
          />
        ) : (
          operacion
        )}
      </div>
    </>
  )
}
