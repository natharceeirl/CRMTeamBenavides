import { useState } from 'react'
import { Button, Input, Popconfirm, Space, Table, Tag, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalVehiculo } from '../components/ModalVehiculo'
import { ModalYamahaMock } from '../components/ModalYamahaMock'
import { useEliminarVehiculo, useVehiculos } from '../api/vehiculos'
import type { VehiculoResponse } from '../api/tipos'
import { lecturaMedidor, nombreTipoUnidad } from '../utils/unidades'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'

export function UnidadesPage() {
  const [texto, setTexto] = useState('')
  const [editando, setEditando] = useState<VehiculoResponse | null>(null)
  const [modalAbierto, setModalAbierto] = useState(false)
  const [modalYamaha, setModalYamaha] = useState(false)

  const { tienePermiso } = useSesion()
  const puedeCrear = tienePermiso(PERMISOS.unidadesCrear)
  const puedeEditar = tienePermiso(PERMISOS.unidadesEditar)
  const puedeDarDeBaja = tienePermiso(PERMISOS.unidadesEliminar)
  const veClientes = tienePermiso(PERMISOS.clientesVer)

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
      render: (_, vehiculo) => <Tag style={{ marginInlineEnd: 0 }}>{nombreTipoUnidad(vehiculo)}</Tag>,
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
      title: 'Placa / serie',
      key: 'identificador',
      className: 'num',
      render: (_, vehiculo) => (
        <div>
          {vehiculo.placa && (
            <div>
              <strong>{vehiculo.placa}</strong>
            </div>
          )}
          {vehiculo.numeroSerieVIN && (
            <div className="texto-secundario">Serie: {vehiculo.numeroSerieVIN}</div>
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
      render: (_, vehiculo) => lecturaMedidor(vehiculo),
    },
    { title: 'Color', dataIndex: 'color', render: (valor: string | null) => valor ?? '—' },
    {
      title: 'Propietario',
      key: 'propietario',
      render: (_, vehiculo) =>
        veClientes ? <Link to={`/clientes/${vehiculo.clienteId}`}>{vehiculo.clienteNombre}</Link> : vehiculo.clienteNombre,
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, vehiculo) => (
        <Space size="small">
          {puedeEditar && (
            <Button
              type="link"
              onClick={() => {
                setEditando(vehiculo)
                setModalAbierto(true)
              }}
            >
              Editar
            </Button>
          )}
          {puedeDarDeBaja && (
            <Popconfirm
              title="Dar de baja la unidad"
              okText="Dar de baja"
              cancelText="Cancelar"
              onConfirm={() => eliminar.mutate(vehiculo.id)}
            >
              <Button type="link">Dar de baja</Button>
            </Popconfirm>
          )}
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
      nombreTipoUnidad(vehiculo),
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
          <Space>
            <Button onClick={() => setModalYamaha(true)}>
              🏍️ Catálogo Yamaha (Mock)
            </Button>
            {puedeCrear && (
              <Button type="primary" onClick={abrirNueva}>
                Registrar unidad
              </Button>
            )}
          </Space>
        }
      />
      <div className="pagina">
        <section>
          <AvisoError error={vehiculos.error ?? eliminar.error} />
          <div className="filtros">
            <Input
              id="buscar-unidades"
              prefix={<SearchOutlined />}
              placeholder="Buscar por tipo, modelo, placa, serie, motor o propietario"
              allowClear
              value={texto}
              onChange={(evento) => setTexto(evento.target.value)}
              style={{ width: 380 }}
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
      <ModalYamahaMock abierto={modalYamaha} onCerrar={() => setModalYamaha(false)} />
    </>
  )
}
