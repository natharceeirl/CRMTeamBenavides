import { useState } from 'react'
import { Button, Input, Popconfirm, Space, Table, Tag, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalVehiculo } from '../components/ModalVehiculo'
import { useEliminarVehiculo, useVehiculos } from '../api/vehiculos'
import type { VehiculoResponse } from '../api/tipos'
import { entero } from '../utils/formato'

const tiposUnidadNombres: Record<number, string> = {
  0: 'Motocicleta',
  1: 'Cuatrimoto',
  2: 'Moto acuática',
  3: 'Generador',
  4: 'Otro',
}

export function UnidadesPage() {
  const [texto, setTexto] = useState('')
  const [editando, setEditando] = useState<VehiculoResponse | null>(null)
  const [modalAbierto, setModalAbierto] = useState(false)

  const vehiculos = useVehiculos()
  const eliminar = useEliminarVehiculo()

  const abrirNueva = () => {
    setEditando(null)
    setModalAbierto(true)
  }

  const columnas: TableProps<VehiculoResponse>['columns'] = [
    {
      title: 'Tipo',
      key: 'tipoUnidad',
      render: (_, vehiculo) => (
        <Tag style={{ marginInlineEnd: 0 }}>
          {vehiculo.tipoUnidad || (vehiculo.tipoUnidadId != null ? tiposUnidadNombres[vehiculo.tipoUnidadId] : 'Unidad')}
        </Tag>
      ),
    },
    {
      title: 'Unidad',
      key: 'unidad',
      render: (_, vehiculo) => (
        <strong>
          {vehiculo.marca} {vehiculo.modelo} {vehiculo.anio ?? ''}
        </strong>
      ),
    },
    {
      title: 'Placa / VIN',
      key: 'identificador',
      className: 'num',
      render: (_, vehiculo) => (
        <div>
          {vehiculo.placa && <div><strong>{vehiculo.placa}</strong></div>}
          {vehiculo.numeroSerieVIN && (
            <div style={{ fontSize: '0.85em', opacity: 0.75 }}>VIN: {vehiculo.numeroSerieVIN}</div>
          )}
          {!vehiculo.placa && !vehiculo.numeroSerieVIN && '—'}
        </div>
      ),
    },
    {
      title: 'Medidor',
      key: 'medidor',
      align: 'right',
      className: 'num',
      render: (_, vehiculo) => {
        const lectura = vehiculo.lecturaMedidorActual ?? vehiculo.kilometraje
        if (lectura == null) return '—'
        const unidad = vehiculo.tipoMedidor === 'HorasUso' || vehiculo.tipoMedidorId === 1 ? 'hrs' : 'km'
        return `${entero(lectura)} ${unidad}`
      },
    },
    { title: 'Color', dataIndex: 'color', render: (valor: string | null) => valor ?? '—' },
    {
      title: 'Propietario',
      key: 'propietario',
      render: (_, vehiculo) => <Link to={`/clientes/${vehiculo.clienteId}`}>{vehiculo.clienteNombre}</Link>,
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, vehiculo) => (
        <Space size="small">
          <Button
            type="link"
            onClick={() => {
              setEditando(vehiculo)
              setModalAbierto(true)
            }}
          >
            Editar
          </Button>
          <Popconfirm
            title="Dar de baja la unidad"
            okText="Dar de baja"
            cancelText="Cancelar"
            onConfirm={() => eliminar.mutate(vehiculo.id)}
          >
            <Button type="link">Dar de baja</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  const busqueda = texto.toLowerCase()
  const visibles = (vehiculos.data ?? []).filter((vehiculo) =>
    [
      vehiculo.marca,
      vehiculo.modelo,
      vehiculo.placa,
      vehiculo.numeroSerieVIN,
      vehiculo.numeroMotor,
      vehiculo.clienteNombre,
    ]
      .filter(Boolean)
      .join(' ')
      .toLowerCase()
      .includes(busqueda),
  )

  return (
    <>
      <BarraSuperior
        titulo="Unidades"
        acciones={
          <Button type="primary" onClick={abrirNueva}>
            Registrar unidad
          </Button>
        }
      />
      <div className="pagina">
        <section>
          <AvisoError error={vehiculos.error ?? eliminar.error} />
          <div className="filtros">
            <Input
              id="buscar-unidades"
              prefix={<SearchOutlined />}
              placeholder="Buscar por modelo, placa, serie/VIN o propietario"
              allowClear
              value={texto}
              onChange={(evento) => setTexto(evento.target.value)}
              style={{ width: 340 }}
            />
          </div>
          <Table
            rowKey="id"
            columns={columnas}
            dataSource={visibles}
            pagination={false}
            loading={vehiculos.isPending}
            locale={{ emptyText: 'Todavía no hay unidades registradas' }}
          />
        </section>
      </div>
      <ModalVehiculo abierto={modalAbierto} vehiculo={editando} onCerrar={() => setModalAbierto(false)} />
    </>
  )
}
