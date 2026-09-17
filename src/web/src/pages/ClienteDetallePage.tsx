import { useState } from 'react'
import { Button, Popconfirm, Space, Table, type TableProps } from 'antd'
import { Link, useParams } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { Indicadores } from '../components/Indicadores'
import { AvisoError } from '../components/AvisoError'
import { ModalCliente } from '../components/ModalCliente'
import { ModalVehiculo } from '../components/ModalVehiculo'
import { useCliente } from '../api/clientes'
import { useEliminarVehiculo, useVehiculos } from '../api/vehiculos'
import type { VehiculoResponse } from '../api/tipos'
import { entero } from '../utils/formato'

export function ClienteDetallePage() {
  const { id } = useParams()
  const cliente = useCliente(id)
  const vehiculos = useVehiculos(id)
  const eliminarVehiculo = useEliminarVehiculo()

  const [editandoCliente, setEditandoCliente] = useState(false)
  const [vehiculoEnEdicion, setVehiculoEnEdicion] = useState<VehiculoResponse | null>(null)
  const [modalVehiculo, setModalVehiculo] = useState(false)

  const columnasUnidades: TableProps<VehiculoResponse>['columns'] = [
    {
      title: 'Unidad',
      key: 'unidad',
      render: (_, vehiculo) => `${vehiculo.marca} ${vehiculo.modelo} ${vehiculo.anio ?? ''}`.trim(),
    },
    { title: 'Placa', dataIndex: 'placa', className: 'num' },
    {
      title: 'Kilometraje',
      dataIndex: 'kilometraje',
      align: 'right',
      className: 'num',
      render: (valor: number | null) => (valor === null ? '—' : `${entero(valor)} km`),
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
              setVehiculoEnEdicion(vehiculo)
              setModalVehiculo(true)
            }}
          >
            Editar
          </Button>
          <Popconfirm
            title="Dar de baja la unidad"
            okText="Dar de baja"
            cancelText="Cancelar"
            onConfirm={() => eliminarVehiculo.mutate(vehiculo.id)}
          >
            <Button type="link">Dar de baja</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  if (cliente.isPending) {
    return (
      <>
        <BarraSuperior antetitulo="Clientes" titulo="Cargando cliente…" />
        <div className="pagina" />
      </>
    )
  }

  if (cliente.isError || !cliente.data) {
    return (
      <>
        <BarraSuperior antetitulo="Clientes" titulo="Cliente no encontrado" />
        <div className="pagina">
          <AvisoError error={cliente.error} />
          <p>
            <Link to="/clientes">Volver a clientes</Link>
          </p>
        </div>
      </>
    )
  }

  const datos = cliente.data
  const unidadesCliente = vehiculos.data ?? []

  return (
    <>
      <BarraSuperior
        antetitulo="Clientes"
        titulo={datos.nombreCompleto}
        acciones={<Button onClick={() => setEditandoCliente(true)}>Editar datos</Button>}
      />
      <div className="pagina">
        <AvisoError error={vehiculos.error ?? eliminarVehiculo.error} />
        <Indicadores
          tamano="mediano"
          items={[
            { etiqueta: 'Documento', valor: datos.documentoIdentidad ?? '—' },
            { etiqueta: 'Teléfono', valor: datos.telefono ?? '—' },
            { etiqueta: 'Unidades', valor: unidadesCliente.length },
          ]}
        />
        <div className="dos-columnas">
          <div className="columna">
            <section>
              <div className="seccion-titulo">
                <h2>Unidades</h2>
                <div className="acciones">
                  <Button
                    onClick={() => {
                      setVehiculoEnEdicion(null)
                      setModalVehiculo(true)
                    }}
                  >
                    Agregar unidad
                  </Button>
                </div>
              </div>
              <Table
                rowKey="id"
                columns={columnasUnidades}
                dataSource={unidadesCliente}
                pagination={false}
                loading={vehiculos.isPending}
                locale={{ emptyText: 'Este cliente todavía no tiene unidades' }}
              />
            </section>
            <section>
              <div className="seccion-titulo">
                <h2>Historial de órdenes</h2>
              </div>
              <p className="texto-secundario">
                Se conecta cuando salga la API de órdenes de servicio, prevista para el sábado 19/09.
              </p>
            </section>
          </div>
          <aside>
            <div className="seccion-titulo">
              <h2>Contacto</h2>
            </div>
            <table className="tabla-simple">
              <tbody>
                <tr>
                  <td>Razón social</td>
                  <td>{datos.razonSocial ?? '—'}</td>
                </tr>
                <tr>
                  <td>Correo</td>
                  <td>{datos.email ?? '—'}</td>
                </tr>
                <tr>
                  <td>Dirección</td>
                  <td>{datos.direccion ?? '—'}</td>
                </tr>
                <tr>
                  <td>Observaciones</td>
                  <td>{datos.observaciones ?? '—'}</td>
                </tr>
              </tbody>
            </table>
          </aside>
        </div>
      </div>
      <ModalCliente abierto={editandoCliente} cliente={datos} onCerrar={() => setEditandoCliente(false)} />
      <ModalVehiculo
        abierto={modalVehiculo}
        vehiculo={vehiculoEnEdicion}
        clienteFijo={datos.id}
        onCerrar={() => setModalVehiculo(false)}
      />
    </>
  )
}
