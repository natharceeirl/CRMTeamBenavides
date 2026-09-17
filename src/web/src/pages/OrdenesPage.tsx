import { useState } from 'react'
import { Button, Input, Select, Table, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link, useNavigate } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { nombresEstado, useOrdenes } from '../api/ordenes'
import type { OrdenServicioResponse } from '../api/tipos'
import { fechaHora, referenciaOrden } from '../utils/formato'

const opcionesEstado = Object.entries(nombresEstado).map(([valor, etiqueta]) => ({
  value: Number(valor),
  label: etiqueta,
}))

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
  { title: 'Cliente', dataIndex: 'clienteNombre' },
  {
    title: 'Unidad',
    key: 'unidad',
    render: (_, orden) => `${orden.vehiculoMarca} ${orden.vehiculoModelo} · ${orden.vehiculoPlaca}`,
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
    dataIndex: 'fechaApertura',
    className: 'num',
    render: (fecha: string) => fechaHora(fecha),
  },
]

export function OrdenesPage() {
  const navigate = useNavigate()
  const [texto, setTexto] = useState('')
  const [estado, setEstado] = useState<number>()

  // El estado se filtra en la API; el texto, en la tabla ya cargada.
  const ordenes = useOrdenes({ estado })

  const busqueda = texto.toLowerCase()
  const visibles = (ordenes.data ?? []).filter((orden) =>
    [
      referenciaOrden(orden.id),
      orden.clienteNombre,
      orden.vehiculoPlaca,
      orden.vehiculoMarca,
      orden.vehiculoModelo,
      orden.tecnicoNombre ?? '',
    ]
      .join(' ')
      .toLowerCase()
      .includes(busqueda),
  )

  return (
    <>
      <BarraSuperior
        titulo="Órdenes de servicio"
        acciones={
          <Button type="primary" onClick={() => navigate('/ordenes/nueva')}>
            Crear orden
          </Button>
        }
      />
      <div className="pagina">
        <section>
          <AvisoError error={ordenes.error} />
          <div className="filtros">
            <Input
              id="buscar-ordenes"
              prefix={<SearchOutlined />}
              placeholder="Buscar por cliente, placa, técnico o referencia"
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
              style={{ width: 220 }}
            />
          </div>
          <Table
            rowKey="id"
            columns={columnas}
            dataSource={visibles}
            pagination={false}
            loading={ordenes.isPending}
            locale={{ emptyText: 'No hay órdenes que coincidan' }}
          />
        </section>
      </div>
    </>
  )
}
