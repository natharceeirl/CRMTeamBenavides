import { useState } from 'react'
import { Button, Input, Modal, Popconfirm, Space, Table, Tabs, Tag, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalUsuario } from '../components/ModalUsuario'
import { ModalRol } from '../components/ModalRol'
import { ModalRolesUsuario } from '../components/ModalRolesUsuario'
import { ModalPermisosRol } from '../components/ModalPermisosRol'
import { PermisoGuard } from '../components/PermisoGuard'
import { useEliminarUsuario, useResetPasswordUsuario, useUsuarios } from '../api/usuarios'
import { useEliminarRol, useRoles } from '../api/roles'
import type { RolResponse, UsuarioResponse } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { avisos } from '../utils/avisos'

export function UsuariosPage() {
  const { usuario: yo } = useSesion()
  const usuarios = useUsuarios()
  const roles = useRoles()
  const eliminarUsuario = useEliminarUsuario()
  const eliminarRol = useEliminarRol()
  const resetPassword = useResetPasswordUsuario()

  const [usuarioEnEdicion, setUsuarioEnEdicion] = useState<UsuarioResponse | null>(null)
  const [modalUsuario, setModalUsuario] = useState(false)
  const [usuarioEnRoles, setUsuarioEnRoles] = useState<UsuarioResponse | null>(null)
  const [modalRolesUsuario, setModalRolesUsuario] = useState(false)

  const [usuarioEnResetPassword, setUsuarioEnResetPassword] = useState<UsuarioResponse | null>(null)
  const [modalResetPassword, setModalResetPassword] = useState(false)
  const [nuevoPasswordReset, setNuevoPasswordReset] = useState('')

  const [rolEnEdicion, setRolEnEdicion] = useState<RolResponse | null>(null)
  const [modalRol, setModalRol] = useState(false)
  const [rolEnPermisos, setRolEnPermisos] = useState<RolResponse | null>(null)
  const [modalPermisos, setModalPermisos] = useState(false)

  const columnasUsuarios: TableProps<UsuarioResponse>['columns'] = [
    {
      title: 'Nombre',
      dataIndex: 'nombreCompleto',
      render: (nombre: string, usuario) => (
        <>
          <strong>{nombre}</strong>
          {usuario.id === yo?.id && <Tag style={{ marginInlineStart: 8 }}>Tú</Tag>}
        </>
      ),
    },
    { title: 'Correo', dataIndex: 'email' },
    { title: 'Teléfono', dataIndex: 'phoneNumber', className: 'num', render: (valor: string | null) => valor ?? '—' },
    {
      title: 'Roles',
      key: 'roles',
      render: (_, usuario) =>
        usuario.roles.length === 0 ? (
          <span className="texto-secundario">Sin rol</span>
        ) : (
          usuario.roles.map((rol) => <Tag key={rol}>{rol}</Tag>)
        ),
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, usuario) => (
        <Space size="small">
          <PermisoGuard permiso="usuarios.editar">
            <Button
              type="link"
              onClick={() => {
                setUsuarioEnEdicion(usuario)
                setModalUsuario(true)
              }}
            >
              Editar
            </Button>
          </PermisoGuard>
          <PermisoGuard permiso="roles.gestionar">
            <Button
              type="link"
              onClick={() => {
                setUsuarioEnRoles(usuario)
                setModalRolesUsuario(true)
              }}
            >
              Roles
            </Button>
          </PermisoGuard>
          <PermisoGuard permiso="usuarios.reset_password">
            <Button
              type="link"
              onClick={() => {
                setUsuarioEnResetPassword(usuario)
                setNuevoPasswordReset('')
                setModalResetPassword(true)
              }}
            >
              Contraseña
            </Button>
          </PermisoGuard>
          {/* Nadie se da de baja a sí mismo: perdería el acceso en el acto. */}
          {usuario.id !== yo?.id && (
            <PermisoGuard permiso="usuarios.eliminar">
              <Popconfirm
                title="Dar de baja al usuario"
                description="Deja de poder iniciar sesión."
                okText="Dar de baja"
                cancelText="Cancelar"
                onConfirm={() => eliminarUsuario.mutate(usuario.id)}
              >
                <Button type="link">Dar de baja</Button>
              </Popconfirm>
            </PermisoGuard>
          )}
        </Space>
      ),
    },
  ]

  const columnasRoles: TableProps<RolResponse>['columns'] = [
    { title: 'Rol', dataIndex: 'nombre', render: (nombre: string) => <strong>{nombre}</strong> },
    { title: 'Descripción', dataIndex: 'descripcion', render: (valor: string | null) => valor ?? '—' },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, rol) => (
        <Space size="small">
          <PermisoGuard permiso="roles.gestionar">
            <Button
              type="link"
              onClick={() => {
                setRolEnEdicion(rol)
                setModalRol(true)
              }}
            >
              Editar
            </Button>
          </PermisoGuard>
          <PermisoGuard permiso="roles.gestionar">
            <Button
              type="link"
              onClick={() => {
                setRolEnPermisos(rol)
                setModalPermisos(true)
              }}
            >
              Permisos
            </Button>
          </PermisoGuard>
          <PermisoGuard permiso="roles.gestionar">
            <Popconfirm
              title="Eliminar el rol"
              okText="Eliminar"
              cancelText="Cancelar"
              onConfirm={() => eliminarRol.mutate(rol.id)}
            >
              <Button type="link">Eliminar</Button>
            </Popconfirm>
          </PermisoGuard>
        </Space>
      ),
    },
  ]

  return (
    <>
      <BarraSuperior titulo="Usuarios y roles" />
      <div className="pagina">
        <AvisoError error={usuarios.error ?? roles.error ?? eliminarUsuario.error ?? eliminarRol.error} />
        <Tabs
          items={[
            {
              key: 'usuarios',
              label: 'Usuarios',
              children: (
                <>
                  <div className="filtros">
                    <PermisoGuard permiso="usuarios.crear">
                      <Button
                        type="primary"
                        onClick={() => {
                          setUsuarioEnEdicion(null)
                          setModalUsuario(true)
                        }}
                      >
                        Crear usuario
                      </Button>
                    </PermisoGuard>
                  </div>
                  <Table
                    rowKey="id"
                    columns={columnasUsuarios}
                    dataSource={usuarios.data ?? []}
                    pagination={false}
                    loading={usuarios.isPending}
                    locale={{ emptyText: 'Todavía no hay usuarios' }}
                  />
                </>
              ),
            },
            {
              key: 'roles',
              label: 'Roles y permisos',
              children: (
                <>
                  <p className="texto-secundario" style={{ marginBottom: 16 }}>
                    Matriz de roles definitivos y permisos gobernados por el backend.
                  </p>
                  <div className="filtros">
                    <PermisoGuard permiso="roles.gestionar">
                      <Button
                        type="primary"
                        onClick={() => {
                          setRolEnEdicion(null)
                          setModalRol(true)
                        }}
                      >
                        Crear rol
                      </Button>
                    </PermisoGuard>
                  </div>
                  <Table
                    rowKey="id"
                    columns={columnasRoles}
                    dataSource={roles.data ?? []}
                    pagination={false}
                    loading={roles.isPending}
                    locale={{ emptyText: 'Todavía no hay roles' }}
                  />
                </>
              ),
            },
          ]}
        />
      </div>
      <ModalUsuario abierto={modalUsuario} usuario={usuarioEnEdicion} onCerrar={() => setModalUsuario(false)} />
      <ModalRolesUsuario
        abierto={modalRolesUsuario}
        usuario={usuarioEnRoles}
        onCerrar={() => setModalRolesUsuario(false)}
      />
      <ModalRol abierto={modalRol} rol={rolEnEdicion} onCerrar={() => setModalRol(false)} />
      <ModalPermisosRol abierto={modalPermisos} rol={rolEnPermisos} onCerrar={() => setModalPermisos(false)} />

      <Modal
        title={`Restablecer contraseña: ${usuarioEnResetPassword?.nombreCompleto ?? ''}`}
        open={modalResetPassword}
        onCancel={() => setModalResetPassword(false)}
        onOk={async () => {
          if (!usuarioEnResetPassword || !nuevoPasswordReset.trim()) return
          try {
            await resetPassword.mutateAsync({
              usuarioId: usuarioEnResetPassword.id,
              nuevoPassword: nuevoPasswordReset.trim(),
            })
            setModalResetPassword(false)
          } catch (fallo) {
            avisos.error(fallo instanceof Error ? fallo.message : 'No se pudo restablecer la contraseña')
          }
        }}
        confirmLoading={resetPassword.isPending}
        okText="Restablecer"
        cancelText="Cancelar"
      >
        <p style={{ marginBottom: 12 }}>
          Ingrese la nueva contraseña para este usuario (mínimo 6 caracteres):
        </p>
        <Input.Password
          placeholder="Nueva contraseña"
          value={nuevoPasswordReset}
          onChange={(e) => setNuevoPasswordReset(e.target.value)}
        />
      </Modal>
    </>
  )
}
