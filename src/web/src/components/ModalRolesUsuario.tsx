import { useState } from 'react'
import { Button, Modal, Select, Space, Tag } from 'antd'
import { useRoles } from '../api/roles'
import { useAsignarRolAUsuario, useQuitarRolAUsuario, useRolesDeUsuario } from '../api/usuarios'
import type { UsuarioResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  usuario?: UsuarioResponse | null
  onCerrar: () => void
}

export function ModalRolesUsuario({ abierto, usuario, onCerrar }: Readonly<Props>) {
  const [rolElegido, setRolElegido] = useState<string | undefined>(undefined)

  const roles = useRoles()
  const rolesDelUsuario = useRolesDeUsuario(abierto ? usuario?.id : undefined)
  const asignar = useAsignarRolAUsuario()
  const quitar = useQuitarRolAUsuario()

  const asignados = rolesDelUsuario.data ?? []
  const disponibles = (roles.data ?? []).filter((rol) => !asignados.some((propio) => propio.id === rol.id))

  const agregar = async () => {
    if (!usuario || !rolElegido) {
      return
    }

    await asignar.mutateAsync({ usuarioId: usuario.id, rolId: rolElegido })
    setRolElegido(undefined)
  }

  return (
    <Modal
      title={`Roles de ${usuario?.nombreCompleto ?? ''}`}
      open={abierto}
      onCancel={onCerrar}
      onOk={onCerrar}
      okText="Listo"
      cancelButtonProps={{ style: { display: 'none' } }}
      destroyOnHidden
    >
      <AvisoError error={roles.error ?? rolesDelUsuario.error ?? asignar.error ?? quitar.error} />
      <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <Space.Compact style={{ width: '100%' }}>
          <Select
            style={{ width: '100%' }}
            placeholder="Elegir rol"
            value={rolElegido}
            onChange={setRolElegido}
            loading={roles.isPending}
            options={disponibles.map((rol) => ({ value: rol.id, label: rol.nombre }))}
            notFoundContent="No quedan roles por asignar"
          />
          <Button type="primary" onClick={agregar} disabled={!rolElegido} loading={asignar.isPending}>
            Asignar
          </Button>
        </Space.Compact>
        <div>
          {asignados.length === 0 && <span className="texto-secundario">Este usuario todavía no tiene roles.</span>}
          {asignados.map((rol) => (
            <Tag
              key={rol.id}
              closable
              onClose={(evento) => {
                evento.preventDefault()
                if (usuario) {
                  quitar.mutate({ usuarioId: usuario.id, rolId: rol.id })
                }
              }}
            >
              {rol.nombre}
            </Tag>
          ))}
        </div>
      </Space>
    </Modal>
  )
}
