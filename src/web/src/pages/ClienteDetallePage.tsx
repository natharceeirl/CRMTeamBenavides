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
import { useOrdenes } from '../api/ordenes'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import type { OrdenServicioResponse, VehiculoResponse } from '../api/tipos'
import { fechaHora, referenciaOrden } from '../utils/formato'
import { identificadorUnidad, lecturaMedidor, nombreTipoUnidad } from '../utils/unidades'
import { useSesion } from '../auth/sesion'
import { ACCESO_ORDENES, PERMISOS, cumpleAcceso } from '../auth/acceso'

export function ClienteDetallePage() {
  const { id } = useParams()
  const sesion = useSesion()
  const veUnidades = sesion.tienePermiso(PERMISOS.unidadesVer)
  const veOrdenes = cumpleAcceso(ACCESO_ORDENES, sesion)
  const puedeEditarCliente = sesion.tienePermiso(PERMISOS.clientesEditar)
  const puedeAgregarUnidad = sesion.tienePermiso(PERMISOS.unidadesCrear)
  const puedeEditarUnidad = sesion.tienePermiso(PERMISOS.unidadesEditar)
  const puedeDarDeBajaUnidad = sesion.tienePermiso(PERMISOS.unidadesEliminar)

  const cliente = useCliente(id)
  const vehiculos = useVehiculos(id, veUnidades)
  const ordenes = useOrdenes({ clienteId: id }, veOrdenes)
  const eliminarVehiculo = useEliminarVehiculo()

  const [editandoCliente, setEditandoCliente] = useState(false)
  const [vehiculoEnEdicion, setVehiculoEnEdicion] = useState<VehiculoResponse | null>(null)
  const [modalVehiculo, setModalVehiculo] = useState(false)

  const columnasUnidades: TableProps<VehiculoResponse>['columns'] = [
    {
      title: 'Unidad',
      key: 'unidad',
      render: (_, vehiculo) => (
        <div>
          <strong>{`${vehiculo.marca} ${vehiculo.modelo} ${vehiculo.anio ?? ''}`.trim()}</strong>
          <div className="texto-secundario">{nombreTipoUnidad(vehiculo)}</div>
        </div>
      ),
    },
    {
      title: 'Placa o serie',
      key: 'identificador',
      className: 'num',
      render: (_, vehiculo) => identificadorUnidad(vehiculo),
    },
    {
      title: 'Medidor',
      key: 'medidor',
      align: 'right',
      className: 'num',
      render: (_, vehiculo) => lecturaMedidor(vehiculo),
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, vehiculo) => (
        <Space size="small">
          {puedeEditarUnidad && (
            <Button
              type="link"
              onClick={() => {
                setVehiculoEnEdicion(vehiculo)
                setModalVehiculo(true)
              }}
            >
              Editar
            </Button>
          )}
          {puedeDarDeBajaUnidad && (
            <Popconfirm
              title="Dar de baja la unidad"
              okText="Dar de baja"
              cancelText="Cancelar"
              onConfirm={() => eliminarVehiculo.mutate(vehiculo.id)}
            >
              <Button type="link">Dar de baja</Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ]

  const columnasOrdenes: TableProps<OrdenServicioResponse>['columns'] = [
    {
      title: 'Orden',
      key: 'orden',
      className: 'num',
      render: (_, orden) => (
        <Link to={`/ordenes/${orden.id}`}>
          <strong>{referenciaOrden(orden)}</strong>
        </Link>
      ),
    },
    {
      title: 'Unidad',
      key: 'unidad',
      render: (_, o) => `${o.vehiculoMarca} ${o.vehiculoModelo} · ${o.vehiculoPlaca ?? 'sin placa'}`,
    },
    {
      title: 'Estado',
      key: 'estado',
      render: (_, o) => <EstadoOrdenApiTag estadoId={o.estadoId} />,
    },
    {
      title: 'Fecha',
      dataIndex: 'fechaApertura',
      className: 'num',
      render: (fecha: string) => fechaHora(fecha),
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
        acciones={
          puedeEditarCliente && <Button onClick={() => setEditandoCliente(true)}>Editar datos</Button>
        }
      />
      <div className="pagina">
        <AvisoError error={vehiculos.error ?? eliminarVehiculo.error} />
        <Indicadores
          tamano="mediano"
          items={[
            {
              etiqueta: 'Documento',
              valor: datos.numeroDocumento
                ? `${datos.tipoDocumento ?? 'Doc.'}: ${datos.numeroDocumento}`
                : (datos.documentoIdentidad ?? '—'),
            },
            { etiqueta: 'Teléfono', valor: datos.telefono ?? '—' },
            ...(veUnidades ? [{ etiqueta: 'Unidades', valor: unidadesCliente.length }] : []),
          ]}
        />
        <div className="dos-columnas">
          <div className="columna">
            {veUnidades && (
              <section>
                <div className="seccion-titulo">
                  <h2>Unidades</h2>
                  {puedeAgregarUnidad && (
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
                  )}
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
            )}
            {veOrdenes && (
              <section>
                <div className="seccion-titulo">
                  <h2>Historial de órdenes</h2>
                </div>
                <Table
                  rowKey="id"
                  columns={columnasOrdenes}
                  dataSource={ordenes.data ?? []}
                  pagination={false}
                  loading={ordenes.isPending}
                  locale={{ emptyText: 'Este cliente todavía no tiene órdenes de servicio' }}
                />
              </section>
            )}
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
