import { useState } from 'react'
import { Button, Input, Popconfirm, Space, Table, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalCliente } from '../components/ModalCliente'
import { useClientes, useEliminarCliente } from '../api/clientes'
import { useVehiculos } from '../api/vehiculos'
import type { ClienteResponse } from '../api/tipos'

export function ClientesPage() {
  const [texto, setTexto] = useState('')
  const [editando, setEditando] = useState<ClienteResponse | null>(null)
  const [modalAbierto, setModalAbierto] = useState(false)

  const clientes = useClientes()
  const vehiculos = useVehiculos()
  const eliminar = useEliminarCliente()

  const abrirNuevo = () => {
    setEditando(null)
    setModalAbierto(true)
  }

  const abrirEdicion = (cliente: ClienteResponse) => {
    setEditando(cliente)
    setModalAbierto(true)
  }

  const columnas: TableProps<ClienteResponse>['columns'] = [
    {
      title: 'Cliente',
      dataIndex: 'nombreCompleto',
      render: (nombre: string, cliente) => (
        <Link to={`/clientes/${cliente.id}`} style={{ fontWeight: 600 }}>
          {nombre}
        </Link>
      ),
    },
    { title: 'Documento', dataIndex: 'documentoIdentidad', className: 'num', render: (valor: string | null) => valor ?? '—' },
    { title: 'Teléfono', dataIndex: 'telefono', className: 'num', render: (valor: string | null) => valor ?? '—' },
    { title: 'Correo', dataIndex: 'email', render: (valor: string | null) => valor ?? '—' },
    {
      title: 'Unidades',
      key: 'unidades',
      align: 'right',
      className: 'num',
      render: (_, cliente) => (vehiculos.data ?? []).filter((vehiculo) => vehiculo.clienteId === cliente.id).length,
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, cliente) => (
        <Space size="small">
          <Button type="link" onClick={() => abrirEdicion(cliente)}>
            Editar
          </Button>
          <Popconfirm
            title="Dar de baja al cliente"
            description="Deja de estar activo, no se borra su historial."
            okText="Dar de baja"
            cancelText="Cancelar"
            onConfirm={() => eliminar.mutate(cliente.id)}
          >
            <Button type="link">Dar de baja</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  const busqueda = texto.toLowerCase()
  const visibles = (clientes.data ?? []).filter((cliente) =>
    [cliente.nombreCompleto, cliente.razonSocial, cliente.documentoIdentidad, cliente.telefono, cliente.email]
      .filter(Boolean)
      .join(' ')
      .toLowerCase()
      .includes(busqueda),
  )

  return (
    <>
      <BarraSuperior
        titulo="Clientes"
        acciones={
          <Button type="primary" onClick={abrirNuevo}>
            Registrar cliente
          </Button>
        }
      />
      <div className="pagina">
        <section>
          <AvisoError error={clientes.error ?? eliminar.error} />
          <div className="filtros">
            <Input
              id="buscar-clientes"
              prefix={<SearchOutlined />}
              placeholder="Buscar por nombre, documento, teléfono o correo"
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
            loading={clientes.isPending}
            locale={{ emptyText: 'Todavía no hay clientes registrados' }}
          />
        </section>
      </div>
      <ModalCliente abierto={modalAbierto} cliente={editando} onCerrar={() => setModalAbierto(false)} />
    </>
  )
}
