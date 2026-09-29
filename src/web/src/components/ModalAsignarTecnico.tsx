import { useState } from 'react'
import { Input, Modal, Select } from 'antd'
import { useAsignarTecnico } from '../api/ordenes'
import { useTecnicos } from '../api/usuarios'
import { AvisoError } from './AvisoError'

type Props = {
  abierto: boolean
  ordenId: string
  tecnicoActualId: string | null
  onCerrar: () => void
}

/** PUT /api/ordenes-servicio/{id}/asignar-tecnico. Solo con `ordenes.asignar_tecnico`. */
export function ModalAsignarTecnico({ abierto, ordenId, tecnicoActualId, onCerrar }: Readonly<Props>) {
  const tecnicos = useTecnicos(abierto)
  const asignar = useAsignarTecnico()
  const [tecnicoId, setTecnicoId] = useState<string | null>(null)
  const [observacion, setObservacion] = useState('')

  const elegido = tecnicoId ?? tecnicoActualId

  const cerrar = () => {
    setTecnicoId(null)
    setObservacion('')
    asignar.reset()
    onCerrar()
  }

  const confirmar = async () => {
    if (!elegido) return
    await asignar.mutateAsync({
      id: ordenId,
      datos: { tecnicoId: elegido, observaciones: observacion.trim() || null },
    })
    cerrar()
  }

  return (
    <Modal
      title={tecnicoActualId ? 'Cambiar técnico' : 'Asignar técnico'}
      open={abierto}
      onCancel={cerrar}
      onOk={confirmar}
      okText="Asignar"
      cancelText="Cancelar"
      okButtonProps={{ disabled: !elegido || elegido === tecnicoActualId }}
      confirmLoading={asignar.isPending}
      destroyOnHidden
    >
      <AvisoError error={asignar.error ?? tecnicos.error} />
      <Select
        showSearch
        optionFilterProp="label"
        style={{ width: '100%' }}
        placeholder="Elige al técnico"
        value={elegido ?? undefined}
        onChange={setTecnicoId}
        loading={tecnicos.isPending}
        options={(tecnicos.data ?? []).map((tecnico) => ({ value: tecnico.id, label: tecnico.nombreCompleto }))}
      />
      <Input.TextArea
        rows={2}
        style={{ marginTop: 12 }}
        value={observacion}
        onChange={(evento) => setObservacion(evento.target.value)}
        placeholder="Motivo del cambio (opcional)"
      />
      <p className="texto-secundario" style={{ marginTop: 8 }}>
        La orden aparece en la app del técnico asignado.
      </p>
    </Modal>
  )
}
