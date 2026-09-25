import { useState } from 'react'
import { Button, Input, Popconfirm, Space, Table, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalVehiculo } from '../components/ModalVehiculo'
import { useEliminarVehiculo, useVehiculos } from '../api/vehiculos'
import type { VehiculoResponse } from '../api/tipos'
import { entero } from '../utils/formato'

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
      title: 'Unidad',
      key: 'unidad',
      render: (_, vehiculo) => (
        <strong>
          {vehiculo.marca} {vehiculo.modelo} {vehiculo.anio ?? ''}
        </strong>
      ),
    },
    { title: 'Placa', dataIndex: 'placa', className: 'num' },
    {
      title: 'Kilometraje',
      dataIndex: 'kilometraje',
      align: 'right',
      className: 'num',
      render: (valor: number | null) => (valor === null ? '—' : `${entero(valor)} km`),
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
    [vehiculo.marca, vehiculo.modelo, vehiculo.placa, vehiculo.clienteNombre]
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
          {/* El tipo de unidad, la serie, el número de motor y el medidor en horas todavía no existen en la API. */}
          <div className="filtros">
            <Input
              id="buscar-unidades"
              prefix={<SearchOutlined />}
              placeholder="Buscar por modelo, placa o propietario"
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
