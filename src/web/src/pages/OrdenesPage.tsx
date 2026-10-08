import { useEffect, useState, type ComponentProps } from 'react'
import dayjs from 'dayjs'
import { Button, DatePicker, Input, Select, Table, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link, useNavigate } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { fechaIngresoOrden, nombresEstado, useOrdenes } from '../api/ordenes'
import { useTecnicos } from '../api/usuarios'
import type { OrdenServicioResponse } from '../api/tipos'
import { fechaHora, referenciaOrden } from '../utils/formato'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { descargarArchivo } from '../utils/descarga'

const opcionesEstado = Object.entries(nombresEstado).map(([valor, etiqueta]) => ({
  value: Number(valor),
  label: etiqueta,
}))

type RangoFechas = Parameters<NonNullable<ComponentProps<typeof DatePicker.RangePicker>['onChange']>>[0]

const columnas: TableProps<OrdenServicioResponse>['columns'] = [
  {
    title: 'Orden',
    key: 'orden',
    className: 'num',
    render: (_, orden) => (
      <Link to={`/ordenes/${orden.id}`} style={{ fontWeight: 600 }}>
        {referenciaOrden(orden)}
      </Link>
    ),
  },
  { title: 'Cliente', dataIndex: 'clienteNombre' },
  {
    title: 'Unidad',
    key: 'unidad',
    render: (_, orden) =>
      `${orden.vehiculoMarca} ${orden.vehiculoModelo} · ${orden.vehiculoPlaca ?? 'sin placa'}`,
  },
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
    key: 'ingreso',
    className: 'num',
    render: (_, orden) => fechaHora(fechaIngresoOrden(orden)),
  },
  {
    title: 'Entrega estimada',
    dataIndex: 'fechaEstimadaEntrega',
    className: 'num',
    render: (fecha: string | null | undefined) => fechaHora(fecha),
  },
]

export function OrdenesPage() {
  const navigate = useNavigate()
  const { tienePermiso } = useSesion()
  // Quien solo ve sus órdenes asignadas no necesita filtrar por técnico.
  const veTodas = tienePermiso(PERMISOS.ordenesVerTodas)
  const puedeCrear = tienePermiso(PERMISOS.ordenesCrear)

  const [texto, setTexto] = useState('')
  const [busqueda, setBusqueda] = useState('')
  const [estado, setEstado] = useState<number>()
  const [tecnicoId, setTecnicoId] = useState<string>()
  const [rango, setRango] = useState<RangoFechas>(null)
  const [exportando, setExportando] = useState(false)
  const [errorExportar, setErrorExportar] = useState<unknown>(null)

  // La búsqueda la resuelve la API: se espera a que el usuario deje de escribir.
  useEffect(() => {
    const espera = setTimeout(() => setBusqueda(texto), 350)
    return () => clearTimeout(espera)
  }, [texto])

  const tecnicos = useTecnicos(veTodas && tienePermiso(PERMISOS.usuariosVer))
  const ordenes = useOrdenes({
    estado,
    tecnicoId,
    busqueda,
    fechaDesde: rango?.[0]?.format('YYYY-MM-DD'),
    fechaHasta: rango?.[1]?.format('YYYY-MM-DD'),
  })

  // El Excel sale con los mismos filtros de la lista; al técnico la API le exporta solo las suyas.
  const exportar = async () => {
    const parametros = new URLSearchParams()
    if (estado !== undefined) parametros.set('estado', String(estado))
    if (tecnicoId) parametros.set('tecnicoId', tecnicoId)
    if (rango?.[0]) parametros.set('fechaDesde', rango[0].format('YYYY-MM-DD'))
    if (rango?.[1]) parametros.set('fechaHasta', rango[1].format('YYYY-MM-DD'))
    const consulta = parametros.toString()
    setErrorExportar(null)
    setExportando(true)
    try {
      await descargarArchivo(
        `/ordenes-servicio/exportar-excel${consulta ? `?${consulta}` : ''}`,
        `ordenes-${dayjs().format('YYYY-MM-DD')}.xlsx`,
      )
    } catch (fallo) {
      setErrorExportar(fallo)
    } finally {
      setExportando(false)
    }
  }

  return (
    <>
      <BarraSuperior
        titulo={veTodas ? 'Órdenes de servicio' : 'Mis órdenes'}
        acciones={
          <>
            <Button onClick={exportar} loading={exportando}>
              Exportar a Excel
            </Button>
            {puedeCrear && (
              <Button type="primary" onClick={() => navigate('/ordenes/nueva')}>
                Crear orden
              </Button>
            )}
          </>
        }
      />
      <div className="pagina">
        <section>
          <AvisoError error={ordenes.error ?? errorExportar} />
          <div className="filtros">
            <Input
              id="buscar-ordenes"
              prefix={<SearchOutlined />}
              placeholder="Buscar por OT, cliente, placa, serie o modelo"
              allowClear
              value={texto}
              onChange={(evento) => setTexto(evento.target.value)}
              style={{ width: 340 }}
            />
            <Select<number>
              id="filtro-estado"
              allowClear
              placeholder="Estado"
              value={estado}
              onChange={setEstado}
              options={opcionesEstado}
              style={{ width: 180 }}
            />
            {veTodas && (
              <Select<string>
                id="filtro-tecnico"
                allowClear
                showSearch
                optionFilterProp="label"
                placeholder="Técnico"
                value={tecnicoId}
                onChange={setTecnicoId}
                loading={tecnicos.isPending}
                options={(tecnicos.data ?? []).map((tecnico) => ({
                  value: tecnico.id,
                  label: tecnico.nombreCompleto,
                }))}
                style={{ width: 220 }}
              />
            )}
            <DatePicker.RangePicker
              id="filtro-fechas"
              format="DD/MM/YYYY"
              placeholder={['Ingreso desde', 'hasta']}
              value={rango}
              onChange={setRango}
            />
          </div>
          <Table
            rowKey="id"
            columns={columnas}
            dataSource={ordenes.data ?? []}
            pagination={false}
            loading={ordenes.isFetching}
            locale={{ emptyText: 'No hay órdenes que coincidan' }}
          />
        </section>
      </div>
    </>
  )
}
