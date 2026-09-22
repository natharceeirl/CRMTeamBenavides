import { useState } from 'react'
import { Button, Segmented, Table, type TableProps } from 'antd'
import { Link, useNavigate } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { Indicadores } from '../components/Indicadores'
import { GraficoBarras } from '../components/GraficoBarras'
import { esEstadoTerminal, useOrdenes } from '../api/ordenes'
import {
  barrasPorEstado,
  enTaller,
  rangoDelPeriodo,
  useResumenDashboard,
  type Periodo,
} from '../api/reportes'
import type { OrdenServicioResponse } from '../api/tipos'
import { entero, fechaHora, referenciaOrden } from '../utils/formato'

const formatoDia = new Intl.DateTimeFormat('es-PE', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
})

const hoyEnPalabras = () => {
  const texto = formatoDia.format(new Date())
  return texto.charAt(0).toUpperCase() + texto.slice(1)
}

const columnas: TableProps<OrdenServicioResponse>['columns'] = [
  {
    title: 'Orden',
    dataIndex: 'id',
    className: 'num',
    render: (id: string) => (
      <Link to={`/ordenes/${id}`} style={{ fontWeight: 600 }}>
        {referenciaOrden(id)}
      </Link>
    ),
  },
  {
    title: 'Unidad',
    key: 'unidad',
    render: (_, orden) => `${orden.vehiculoMarca} ${orden.vehiculoModelo} · ${orden.vehiculoPlaca}`,
  },
  { title: 'Cliente', dataIndex: 'clienteNombre' },
  {
    title: 'Técnico',
    dataIndex: 'tecnicoNombre',
    className: 'sin-salto',
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
]

export function TableroPage() {
  const navigate = useNavigate()
  const [periodo, setPeriodo] = useState<Periodo>('Semana')

  const rango = rangoDelPeriodo(periodo)
  const resumen = useResumenDashboard(rango)
  const ordenes = useOrdenes()

  // El tablero muestra lo que sigue en el taller: ni entregadas ni anuladas.
  const activas = (ordenes.data ?? []).filter((orden) => !esEstadoTerminal(orden.estadoId))
  const datos = resumen.data

  return (
    <>
      <BarraSuperior
        antetitulo="Tablero"
        titulo={hoyEnPalabras()}
        acciones={
          <>
            <Segmented<Periodo>
              options={['Hoy', 'Semana', 'Mes']}
              value={periodo}
              onChange={setPeriodo}
            />
            <Button type="primary" onClick={() => navigate('/ordenes/nueva')}>
              Crear orden
            </Button>
          </>
        }
      />
      <div className="pagina">
        <AvisoError error={resumen.error ?? ordenes.error} />
        <Indicadores
          items={[
            { etiqueta: 'En taller', valor: datos ? enTaller(datos) : '—' },
            { etiqueta: 'Listas para entrega', valor: datos?.ordenesServicio.lista ?? '—' },
            {
              etiqueta: `Vendido · ${periodo.toLowerCase()}`,
              valor: datos ? `S/ ${entero(Math.round(datos.ventas.montoConfirmadas))}` : '—',
              compacto: true,
            },
            {
              etiqueta: 'Productos con stock bajo',
              valor: datos?.inventario.productosConStockBajo ?? '—',
              destacado: (datos?.inventario.productosConStockBajo ?? 0) > 0,
            },
          ]}
        />
        <section>
          <div className="seccion-titulo">
            <h2>Órdenes por estado</h2>
          </div>
          <GraficoBarras
            datos={datos ? barrasPorEstado(datos) : []}
            unidad="órdenes"
            leyenda={[{ texto: 'En taller' }, { texto: 'Cerradas', atenuada: true }]}
            cargando={resumen.isPending}
            vacio="Todavía no hay órdenes registradas"
          />
        </section>

        <section>
          <div className="seccion-titulo">
            <h2>Órdenes en taller</h2>
          </div>
          <Table
            rowKey="id"
            columns={columnas}
            dataSource={activas}
            pagination={false}
            loading={ordenes.isPending}
            locale={{ emptyText: 'No hay órdenes abiertas' }}
          />
        </section>
      </div>
    </>
  )
}
