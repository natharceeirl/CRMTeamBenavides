import { Button, Table, Tabs, Tag, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { matrizPermisos, roles, usuarios, type NivelPermiso, type Usuario } from '../data/ejemplo'
import { colores } from '../theme/tokens'

const estiloNivel: Record<NivelPermiso, { background: string; color: string; borderColor: string }> = {
  Todo: { background: colores.texto, color: colores.fondo, borderColor: colores.texto },
  Ver: { background: colores.neutro100, color: colores.neutro800, borderColor: colores.neutro300 },
  '—': { background: 'transparent', color: colores.textoSecundario, borderColor: 'transparent' },
}

const columnasUsuarios: TableProps<Usuario>['columns'] = [
  { title: 'Nombre', dataIndex: 'nombre', render: (nombre: string) => <strong>{nombre}</strong> },
  { title: 'Correo', dataIndex: 'correo' },
  { title: 'Rol', dataIndex: 'rol' },
  {
    title: 'Estado',
    dataIndex: 'activo',
    render: (activo: boolean) => (
      <Tag
        style={{
          marginInlineEnd: 0,
          background: activo ? colores.neutro100 : 'transparent',
          color: activo ? colores.neutro800 : colores.textoSecundario,
          borderColor: colores.neutro300,
        }}
      >
        {activo ? 'Activo' : 'Inactivo'}
      </Tag>
    ),
  },
  { title: '', key: 'acciones', align: 'right', render: () => <Button type="link">Editar</Button> },
]

type FilaPermiso = (typeof matrizPermisos)[number]

const columnasPermisos: TableProps<FilaPermiso>['columns'] = [
  { title: 'Módulo', dataIndex: 'modulo', render: (modulo: string) => <strong>{modulo}</strong> },
  ...roles.map((rol, indice) => ({
    title: rol,
    key: rol,
    render: (_: unknown, fila: FilaPermiso) => {
      const nivel = fila.permisos[indice]
      return <Tag style={{ ...estiloNivel[nivel], marginInlineEnd: 0 }}>{nivel}</Tag>
    },
  })),
]

export function UsuariosPage() {
  return (
    <>
      <BarraSuperior titulo="Usuarios y roles" acciones={<Button type="primary">Crear usuario</Button>} />
      <div className="pagina">
        <Tabs
          items={[
            {
              key: 'usuarios',
              label: 'Usuarios',
              children: <Table rowKey="correo" columns={columnasUsuarios} dataSource={usuarios} pagination={false} />,
            },
            {
              key: 'permisos',
              label: 'Roles y permisos',
              children: (
                <>
                  <p className="texto-secundario" style={{ marginBottom: 16 }}>
                    Propuesta de matriz; la definitiva la entrega el cliente.
                  </p>
                  <Table
                    rowKey="modulo"
                    columns={columnasPermisos}
                    dataSource={matrizPermisos}
                    pagination={false}
                    scroll={{ x: 'max-content' }}
                  />
                </>
              ),
            },
          ]}
        />
      </div>
    </>
  )
}
