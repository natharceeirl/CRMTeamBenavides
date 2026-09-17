import { Checkbox, Modal, Space, Spin } from 'antd'
import { usePermisos } from '../api/permisos'
import { useAsignarPermiso, usePermisosDeRol, useQuitarPermiso } from '../api/roles'
import type { RolResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  rol?: RolResponse | null
  onCerrar: () => void
}

export function ModalPermisosRol({ abierto, rol, onCerrar }: Readonly<Props>) {
  const catalogo = usePermisos()
  const permisosDelRol = usePermisosDeRol(abierto ? rol?.id : undefined)
  const asignar = useAsignarPermiso()
  const quitar = useQuitarPermiso()

  const asignados = new Set((permisosDelRol.data ?? []).map((permiso) => permiso.id))

  const alternar = (permisoId: string, marcado: boolean) => {
    if (!rol) {
      return
    }

    if (marcado) {
      asignar.mutate({ rolId: rol.id, permisoId })
    } else {
      quitar.mutate({ rolId: rol.id, permisoId })
    }
  }

  const cargando = catalogo.isPending || permisosDelRol.isPending

  return (
    <Modal
      title={`Permisos de ${rol?.nombre ?? ''}`}
      open={abierto}
      onCancel={onCerrar}
      onOk={onCerrar}
      okText="Listo"
      cancelButtonProps={{ style: { display: 'none' } }}
      destroyOnHidden
    >
      <AvisoError error={catalogo.error ?? permisosDelRol.error ?? asignar.error ?? quitar.error} />
      {cargando && <Spin />}
      {!cargando && (catalogo.data ?? []).length === 0 && (
        <p className="texto-secundario">
          El catálogo de permisos está vacío. Los siembra el backend: pídeselos a Paolo.
        </p>
      )}
      <Space direction="vertical" size="small">
        {(catalogo.data ?? []).map((permiso) => (
          <Checkbox
            key={permiso.id}
            checked={asignados.has(permiso.id)}
            onChange={(evento) => alternar(permiso.id, evento.target.checked)}
          >
            <strong>{permiso.codigo}</strong>
            {permiso.descripcion ? ` · ${permiso.descripcion}` : ''}
          </Checkbox>
        ))}
      </Space>
    </Modal>
  )
}
