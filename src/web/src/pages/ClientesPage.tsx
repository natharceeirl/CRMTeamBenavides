import { useState } from 'react'
import { Button, Input, Table, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { clientes, ordenes, unidades, type Cliente } from '../data/ejemplo'

const columnas: TableProps<Cliente>['columns'] = [
  {
    title: 'Cliente',
    dataIndex: 'nombre',
    render: (nombre: string, cliente) => (
      <Link to={`/clientes/${cliente.id}`} style={{ fontWeight: 600 }}>
        {nombre}
      </Link>
    ),
  },
  { title: 'Documento', key: 'documento', className: 'num', render: (_, cliente) => `${cliente.tipoDocumento} ${cliente.documento}` },
  { title: 'Teléfono', dataIndex: 'telefono', className: 'num' },
  {
    title: 'Unidades',
    key: 'unidades',
    align: 'right',
    className: 'num',
    render: (_, cliente) => unidades.filter((unidad) => unidad.clienteId === cliente.id).length,
  },
  {
    title: 'Órdenes',
    key: 'ordenes',
    align: 'right',
    className: 'num',
    render: (_, cliente) => ordenes.filter((orden) => orden.clienteId === cliente.id).length,
  },
]

export function ClientesPage() {
  const [texto, setTexto] = useState('')

  const visibles = clientes.filter((cliente) => {
    const placas = unidades
      .filter((unidad) => unidad.clienteId === cliente.id)
      .map((unidad) => unidad.placa ?? '')
      .join(' ')
    return [cliente.nombre, cliente.documento, cliente.telefono, placas]
      .join(' ')
      .toLowerCase()
      .includes(texto.toLowerCase())
  })

  return (
    <>
      <BarraSuperior titulo="Clientes" acciones={<Button type="primary">Registrar cliente</Button>} />
      <div className="pagina">
        <section>
          <div className="filtros">
            <Input
              id="buscar-clientes"
              prefix={<SearchOutlined />}
              placeholder="Buscar por nombre, DNI/RUC, teléfono o placa"
              allowClear
              value={texto}
              onChange={(evento) => setTexto(evento.target.value)}
              style={{ width: 380 }}
            />
          </div>
          <Table rowKey="id" columns={columnas} dataSource={visibles} pagination={false} />
        </section>
      </div>
    </>
  )
}
