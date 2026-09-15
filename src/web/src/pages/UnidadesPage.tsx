import { useState } from 'react'
import { Button, Input, Segmented, Table, type TableProps } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { Link } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { buscarCliente, nombreUnidad, tiposUnidad, unidades, type TipoUnidad, type Unidad } from '../data/ejemplo'
import { entero } from '../utils/formato'

const TODAS = 'Todas'
const opcionesTipo = [TODAS, ...Object.values(tiposUnidad)]

const columnas: TableProps<Unidad>['columns'] = [
  { title: 'Unidad', key: 'unidad', render: (_, unidad) => <strong>{nombreUnidad(unidad)}</strong> },
  { title: 'Tipo', key: 'tipo', render: (_, unidad) => tiposUnidad[unidad.tipo] },
  { title: 'Placa', key: 'placa', className: 'num', render: (_, unidad) => unidad.placa ?? '—' },
  { title: 'VIN o serie', dataIndex: 'serie', className: 'num' },
  { title: 'N.° de motor', dataIndex: 'numeroMotor', className: 'num' },
  {
    title: 'Medidor',
    key: 'medidor',
    align: 'right',
    className: 'num',
    render: (_, unidad) => `${entero(unidad.medidor)} ${unidad.unidadMedidor}`,
  },
  {
    title: 'Propietario',
    key: 'propietario',
    render: (_, unidad) => {
      const cliente = buscarCliente(unidad.clienteId)
      return cliente ? <Link to={`/clientes/${cliente.id}`}>{cliente.nombre}</Link> : '—'
    },
  },
]

export function UnidadesPage() {
  const [texto, setTexto] = useState('')
  const [tipo, setTipo] = useState<string>(TODAS)

  const tipoElegido = (Object.keys(tiposUnidad) as TipoUnidad[]).find((clave) => tiposUnidad[clave] === tipo)
  const visibles = unidades.filter((unidad) => {
    const busqueda = [nombreUnidad(unidad), unidad.placa, unidad.serie, unidad.numeroMotor].join(' ').toLowerCase()
    return (!tipoElegido || unidad.tipo === tipoElegido) && busqueda.includes(texto.toLowerCase())
  })

  return (
    <>
      <BarraSuperior titulo="Unidades" acciones={<Button type="primary">Registrar unidad</Button>} />
      <div className="pagina">
        <section>
          <div className="filtros">
            <Input
              id="buscar-unidades"
              prefix={<SearchOutlined />}
              placeholder="Buscar por modelo, placa, serie o motor"
              allowClear
              value={texto}
              onChange={(evento) => setTexto(evento.target.value)}
              style={{ width: 340 }}
            />
            <Segmented<string> options={opcionesTipo} value={tipo} onChange={setTipo} />
          </div>
          <Table rowKey="id" columns={columnas} dataSource={visibles} pagination={false} />
        </section>
      </div>
    </>
  )
}
