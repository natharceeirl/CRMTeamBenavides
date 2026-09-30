import { useState } from 'react'
import {
  Button,
  Card,
  Empty,
  Form,
  Image,
  Input,
  Modal,
  Popconfirm,
  Radio,
  Select,
  Space,
  Spin,
  Tag,
  Typography,
} from 'antd'
import { useFotosOrden, useSubirFotoOrden, useEliminarFotoOrden } from '../api/fotos'
import { fechaHora } from '../utils/formato'
import { AvisoError } from './AvisoError'

const { Text } = Typography

type Props = {
  abierto: boolean
  ordenServicioId: string
  numeroOrden?: string | null
  soloLectura?: boolean
  onCerrar: () => void
}

const ETAPAS = [
  { value: 0, label: 'Ingreso', color: 'blue' },
  { value: 1, label: 'Diagnóstico', color: 'orange' },
  { value: 2, label: 'Reparación', color: 'purple' },
  { value: 3, label: 'Entrega', color: 'green' },
]

export function ModalFotosOrden({
  abierto,
  ordenServicioId,
  numeroOrden,
  soloLectura = false,
  onCerrar,
}: Readonly<Props>) {
  const fotosQuery = useFotosOrden(abierto ? ordenServicioId : null)
  const subirFoto = useSubirFotoOrden()
  const eliminarFoto = useEliminarFotoOrden()

  const [filtroEtapa, setFiltroEtapa] = useState<number | 'todas'>('todas')
  const [archivoSeleccionado, setArchivoSeleccionado] = useState<File | null>(null)
  const [etapaSeleccionada, setEtapaSeleccionada] = useState<number>(0)
  const [observacion, setObservacion] = useState('')
  const [formularioAbierto, setFormularioAbierto] = useState(false)

  const fotos = fotosQuery.data ?? []
  const fotosFiltradas =
    filtroEtapa === 'todas' ? fotos : fotos.filter((f) => f.etapa === filtroEtapa)

  const handleSubir = async () => {
    if (!archivoSeleccionado) return

    await subirFoto.mutateAsync({
      ordenServicioId,
      archivo: archivoSeleccionado,
      etapa: etapaSeleccionada,
      observacion: observacion.trim() || undefined,
    })

    setArchivoSeleccionado(null)
    setObservacion('')
    setFormularioAbierto(false)
  }

  const handleEliminar = async (fotoId: string) => {
    await eliminarFoto.mutateAsync({
      ordenServicioId,
      fotoId,
    })
  }

  const etiquetaEtapa = (etapaNum: number) => {
    const config = ETAPAS.find((e) => e.value === etapaNum)
    return <Tag color={config?.color ?? 'default'}>{config?.label ?? 'Etapa'}</Tag>
  }

  return (
    <Modal
      title={`Fotografías de Orden ${numeroOrden ?? ordenServicioId.slice(0, 8)}`}
      open={abierto}
      onCancel={onCerrar}
      footer={[
        <Button key="cerrar" onClick={onCerrar}>
          Cerrar
        </Button>,
      ]}
      width={860}
      destroyOnHidden
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {/* Barra superior: filtros y botón de carga */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
          <Radio.Group
            value={filtroEtapa}
            onChange={(e) => setFiltroEtapa(e.target.value)}
            size="small"
            buttonStyle="solid"
          >
            <Radio.Button value="todas">Todas ({fotos.length})</Radio.Button>
            {ETAPAS.map((e) => {
              const cant = fotos.filter((f) => f.etapa === e.value).length
              return (
                <Radio.Button key={e.value} value={e.value}>
                  {e.label} ({cant})
                </Radio.Button>
              )
            })}
          </Radio.Group>

          {!soloLectura && (
            <Button
              type={formularioAbierto ? 'default' : 'primary'}
              onClick={() => setFormularioAbierto((prev) => !prev)}
            >
              {formularioAbierto ? 'Cancelar carga' : '📷 Agregar fotografía'}
            </Button>
          )}
        </div>

        {/* Formulario de subida de foto */}
        {formularioAbierto && !soloLectura && (
          <Card
            size="small"
            title="Nueva fotografía de evidencia"
            style={{ background: '#f9fafb', borderColor: '#d1d5db' }}
          >
            <Form layout="vertical">
              <Form.Item label="Archivo de imagen (JPG, PNG, WEBP, máx 10 MB)" required>
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={(e) => {
                    const archivo = e.target.files?.[0] ?? null
                    setArchivoSeleccionado(archivo)
                  }}
                />
              </Form.Item>

              <Form.Item label="Etapa / Momento de la fotografía" required>
                <Select
                  value={etapaSeleccionada}
                  onChange={(val) => setEtapaSeleccionada(val)}
                  options={ETAPAS.map((e) => ({ value: e.value, label: e.label }))}
                />
              </Form.Item>

              <Form.Item label="Observación o nota (opcional)">
                <Input.TextArea
                  rows={2}
                  value={observacion}
                  onChange={(e) => setObservacion(e.target.value)}
                  placeholder="Detalle sobre el estado de la pieza o parte fotografiada..."
                  maxLength={500}
                />
              </Form.Item>

              {subirFoto.isError && <AvisoError error={subirFoto.error} />}

              <Space>
                <Button
                  type="primary"
                  onClick={handleSubir}
                  disabled={!archivoSeleccionado}
                  loading={subirFoto.isPending}
                >
                  Guardar fotografía
                </Button>
                <Button onClick={() => setFormularioAbierto(false)}>Cancelar</Button>
              </Space>
            </Form>
          </Card>
        )}

        {/* Galería de imágenes */}
        {fotosQuery.isPending && (
          <div style={{ textAlign: 'center', padding: 40 }}>
            <Spin description="Cargando fotografías..." />
          </div>
        )}

        {fotosQuery.isError && <AvisoError error={fotosQuery.error} />}

        {!fotosQuery.isPending && !fotosQuery.isError && fotosFiltradas.length === 0 && (
          <Empty
            description={
              filtroEtapa === 'todas'
                ? 'No hay fotografías registradas en esta orden de servicio.'
                : 'No hay fotografías en la etapa seleccionada.'
            }
          />
        )}

        {!fotosQuery.isPending && fotosFiltradas.length > 0 && (
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))',
              gap: 16,
              maxHeight: 520,
              overflowY: 'auto',
              paddingRight: 4,
            }}
          >
            {fotosFiltradas.map((foto) => (
              <Card
                key={foto.id}
                size="small"
                hoverable
                cover={
                  <div style={{ height: 180, display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#000000' }}>
                    <Image
                      alt={foto.nombreArchivoOriginal}
                      src={foto.urlRelativa}
                      style={{ maxHeight: 180, objectFit: 'contain' }}
                    />
                  </div>
                }
                actions={
                  !soloLectura
                    ? [
                        <Popconfirm
                          key="eliminar"
                          title="¿Eliminar esta fotografía?"
                          description="Esta acción no se puede deshacer."
                          onConfirm={() => handleEliminar(foto.id)}
                          okText="Sí, eliminar"
                          cancelText="Cancelar"
                          okButtonProps={{ danger: true }}
                        >
                          <Button type="link" danger size="small" loading={eliminarFoto.isPending}>
                            Eliminar
                          </Button>
                        </Popconfirm>,
                      ]
                    : undefined
                }
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
                  {etiquetaEtapa(foto.etapa)}
                  <Text type="secondary" style={{ fontSize: 11 }}>
                    {fechaHora(foto.fechaCreacion)}
                  </Text>
                </div>

                {foto.usuarioNombre && (
                  <Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 4 }}>
                    Registrada por: {foto.usuarioNombre}
                  </Text>
                )}

                {foto.observacion && (
                  <Text style={{ fontSize: 12, display: 'block', fontStyle: 'italic', marginTop: 4 }}>
                    «{foto.observacion}»
                  </Text>
                )}
              </Card>
            ))}
          </div>
        )}
      </div>
    </Modal>
  )
}
