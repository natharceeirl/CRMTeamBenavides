import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Button, Form, Image, Input, Modal, Popconfirm, Segmented, Select, Space, Upload } from 'antd'
import { useEliminarFotoOrden, useFotosOrden, useSubirFotoOrden } from '../api/fotos'
import { solicitarBlob } from '../api/http'
import type { FotoOrdenServicioResponse } from '../api/tipos'
import { fechaHora } from '../utils/formato'
import { AvisoError } from './AvisoError'
import { EtiquetaEstado } from './EtiquetaEstado'

type Props = {
  abierto: boolean
  ordenServicioId: string
  numeroOrden?: string | null
  soloLectura?: boolean
  onCerrar: () => void
}

/** Enum EtapaFotoOrdenServicio del backend, en el mismo orden. */
const ETAPAS = [
  { value: 0, label: 'Ingreso' },
  { value: 1, label: 'Diagnóstico' },
  { value: 2, label: 'Reparación' },
  { value: 3, label: 'Entrega' },
]

const TIPOS_PERMITIDOS = ['image/jpeg', 'image/png', 'image/webp']
const TAMANO_MAXIMO = 10 * 1024 * 1024

const nombreEtapa = (etapa: number) => ETAPAS.find((opcion) => opcion.value === etapa)?.label ?? 'Etapa'

/**
 * La foto la sirve la API con el token: se descarga y se muestra desde una URL
 * local, porque una etiqueta <img> apuntando a la API respondería 401.
 */
function FotoProtegida({ foto }: Readonly<{ foto: FotoOrdenServicioResponse }>) {
  const archivo = useQuery({
    queryKey: ['foto-orden', foto.id],
    queryFn: () => solicitarBlob(foto.urlRelativa.replace(/^\/api/, '')),
    staleTime: Infinity,
  })
  const [url, setUrl] = useState<string | null>(null)

  useEffect(() => {
    if (!archivo.data) return
    const local = URL.createObjectURL(archivo.data)
    setUrl(local)
    return () => URL.revokeObjectURL(local)
  }, [archivo.data])

  if (archivo.isError) return <div className="galeria-imagen">No se pudo cargar</div>
  return (
    <div className="galeria-imagen">
      {url ? <Image src={url} alt={foto.observacion ?? foto.nombreArchivoOriginal} /> : 'Cargando…'}
    </div>
  )
}

export function ModalFotosOrden({ abierto, ordenServicioId, numeroOrden, soloLectura = false, onCerrar }: Readonly<Props>) {
  const fotosQuery = useFotosOrden(abierto ? ordenServicioId : null)
  const subirFoto = useSubirFotoOrden()
  const eliminarFoto = useEliminarFotoOrden()

  const [filtroEtapa, setFiltroEtapa] = useState<number | 'todas'>('todas')
  const [archivo, setArchivo] = useState<File | null>(null)
  const [errorArchivo, setErrorArchivo] = useState<string | null>(null)
  const [etapa, setEtapa] = useState(0)
  const [observacion, setObservacion] = useState('')
  const [formularioAbierto, setFormularioAbierto] = useState(false)

  const fotos = fotosQuery.data ?? []
  const visibles = filtroEtapa === 'todas' ? fotos : fotos.filter((foto) => foto.etapa === filtroEtapa)

  const limpiarFormulario = () => {
    setArchivo(null)
    setErrorArchivo(null)
    setObservacion('')
    setFormularioAbierto(false)
    subirFoto.reset()
  }

  const elegirArchivo = (elegido: File) => {
    if (!TIPOS_PERMITIDOS.includes(elegido.type)) {
      setErrorArchivo('Solo se aceptan fotos JPG, PNG o WEBP.')
      setArchivo(null)
    } else if (elegido.size > TAMANO_MAXIMO) {
      setErrorArchivo('La foto pesa más de 10 MB.')
      setArchivo(null)
    } else {
      setErrorArchivo(null)
      setArchivo(elegido)
    }
    // No se sube sola: espera a «Guardar foto».
    return false
  }

  const subir = async () => {
    if (!archivo) return
    await subirFoto.mutateAsync({ ordenServicioId, archivo, etapa, observacion: observacion.trim() || undefined })
    limpiarFormulario()
  }

  const opcionesFiltro = [
    { value: 'todas' as const, label: `Todas (${fotos.length})` },
    ...ETAPAS.map((opcion) => ({
      value: opcion.value,
      label: `${opcion.label} (${fotos.filter((foto) => foto.etapa === opcion.value).length})`,
    })),
  ]

  return (
    <Modal
      title={`Fotos de la orden ${numeroOrden ?? ordenServicioId.slice(0, 8).toUpperCase()}`}
      open={abierto}
      onCancel={onCerrar}
      footer={<Button onClick={onCerrar}>Cerrar</Button>}
      width={900}
      destroyOnHidden
    >
      <AvisoError error={fotosQuery.error ?? eliminarFoto.error} />
      <div className="seccion-titulo">
        <Segmented<number | 'todas'> options={opcionesFiltro} value={filtroEtapa} onChange={setFiltroEtapa} />
        {!soloLectura && !formularioAbierto && (
          <div className="acciones">
            <Button type="primary" onClick={() => setFormularioAbierto(true)}>
              Agregar foto
            </Button>
          </div>
        )}
      </div>

      {formularioAbierto && !soloLectura && (
        <div className="panel-formulario" style={{ marginBottom: 20 }}>
          <AvisoError error={subirFoto.error} />
          <Form layout="vertical" requiredMark={false}>
            <div className="formulario-grid">
              <Form.Item
                label="Foto"
                validateStatus={errorArchivo ? 'error' : undefined}
                help={errorArchivo ?? 'JPG, PNG o WEBP, hasta 10 MB.'}
              >
                <Space wrap>
                  <Upload accept={TIPOS_PERMITIDOS.join(',')} showUploadList={false} beforeUpload={elegirArchivo}>
                    <Button>{archivo ? 'Cambiar foto' : 'Elegir foto'}</Button>
                  </Upload>
                  {archivo && <span className="texto-secundario">{archivo.name}</span>}
                </Space>
              </Form.Item>
              <Form.Item label="Etapa">
                <Select value={etapa} onChange={setEtapa} options={ETAPAS} />
              </Form.Item>
              <Form.Item label="Observación" className="ancho-completo">
                <Input.TextArea
                  rows={2}
                  maxLength={500}
                  value={observacion}
                  onChange={(evento) => setObservacion(evento.target.value)}
                  placeholder="Golpe en el tanque, desgaste de la cadena…"
                />
              </Form.Item>
            </div>
            <Space>
              <Button type="primary" onClick={subir} disabled={!archivo} loading={subirFoto.isPending}>
                Guardar foto
              </Button>
              <Button onClick={limpiarFormulario}>Cancelar</Button>
            </Space>
          </Form>
        </div>
      )}

      {!fotosQuery.isPending && visibles.length === 0 && (
        <p className="texto-secundario">
          {filtroEtapa === 'todas' ? 'Todavía no hay fotos de esta orden.' : 'No hay fotos en esta etapa.'}
        </p>
      )}

      {visibles.length > 0 && (
        <Image.PreviewGroup>
          <div className="galeria">
            {visibles.map((foto) => (
              <figure key={foto.id} className="galeria-foto">
                <FotoProtegida foto={foto} />
                <figcaption>
                  <Space size={8} wrap>
                    <EtiquetaEstado tono="neutro">{nombreEtapa(foto.etapa)}</EtiquetaEstado>
                    <span className="texto-secundario">{fechaHora(foto.fechaCreacion)}</span>
                  </Space>
                  {foto.observacion && <div>{foto.observacion}</div>}
                  <div className="texto-secundario">{foto.usuarioNombre ?? ''}</div>
                  {!soloLectura && (
                    <Popconfirm
                      title="Quitar la foto"
                      okText="Quitar"
                      cancelText="Cancelar"
                      okButtonProps={{ danger: true }}
                      onConfirm={() => eliminarFoto.mutateAsync({ ordenServicioId, fotoId: foto.id })}
                    >
                      <Button type="link" style={{ paddingInline: 0 }}>
                        Quitar
                      </Button>
                    </Popconfirm>
                  )}
                </figcaption>
              </figure>
            ))}
          </div>
        </Image.PreviewGroup>
      )}
    </Modal>
  )
}
