import { useState } from 'react'
import { Button, Input, Modal, Popconfirm, Space, Table, Tag, Tabs, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalFaq } from '../components/ModalFaq'
import {
  ATENCION,
  nombresAtencion,
  puedeAsignarse,
  puedeCerrarse,
  useAsignarAgente,
  useBandeja,
  useEliminarFaq,
  useFaqsAdmin,
  useResolverConsulta,
} from '../api/chatbot'
import { useSesion } from '../auth/sesion'
import type { ConsultaBandejaResponse, FaqResponse } from '../api/tipos'
import { colores } from '../theme/tokens'
import { fechaHora } from '../utils/formato'

function EstadoAtencionTag({ estadoId }: Readonly<{ estadoId: number }>) {
  const estilos: Record<number, { background: string; color: string; borderColor: string }> = {
    [ATENCION.pendiente]: {
      background: colores.acento100,
      color: colores.acento800,
      borderColor: colores.acento200,
    },
    [ATENCION.enAtencion]: {
      background: colores.texto,
      color: colores.fondo,
      borderColor: colores.texto,
    },
    [ATENCION.resuelto]: {
      background: colores.neutro100,
      color: colores.neutro800,
      borderColor: colores.neutro300,
    },
    [ATENCION.descartado]: {
      background: 'transparent',
      color: colores.textoSecundario,
      borderColor: colores.neutro300,
    },
  }

  return (
    <Tag style={{ ...estilos[estadoId], marginInlineEnd: 0, fontWeight: 600 }}>
      {nombresAtencion[estadoId] ?? 'Desconocido'}
    </Tag>
  )
}

export function ChatbotPage() {
  const { usuario } = useSesion()

  const bandeja = useBandeja()
  const faqs = useFaqsAdmin()
  const asignar = useAsignarAgente()
  const resolver = useResolverConsulta()
  const eliminarFaq = useEliminarFaq()

  const [faqEnEdicion, setFaqEnEdicion] = useState<FaqResponse | null>(null)
  const [modalFaq, setModalFaq] = useState(false)
  const [cierre, setCierre] = useState<{ id: string; estado: number } | null>(null)
  const [notas, setNotas] = useState('')

  const confirmarCierre = async () => {
    if (!cierre) {
      return
    }

    await resolver.mutateAsync({
      id: cierre.id,
      datos: { estado: cierre.estado, notasAgente: notas.trim() ? notas.trim() : null },
    })

    setCierre(null)
    setNotas('')
  }

  const columnasBandeja: TableProps<ConsultaBandejaResponse>['columns'] = [
    {
      title: 'Fecha',
      dataIndex: 'fecha',
      className: 'num',
      render: (fecha: string) => fechaHora(fecha),
    },
    { title: 'Canal', dataIndex: 'canal' },
    {
      title: 'Contacto',
      key: 'contacto',
      render: (_, consulta) =>
        [consulta.clienteNombre ?? consulta.nombreContacto, consulta.telefonoContacto]
          .filter(Boolean)
          .join(' · ') || 'Anónimo',
    },
    { title: 'Consulta', dataIndex: 'mensajeConsulta' },
    {
      title: 'Respondió',
      dataIndex: 'faqPregunta',
      render: (pregunta: string | null) => pregunta ?? 'Sin respuesta automática',
    },
    {
      title: 'Estado',
      key: 'estado',
      render: (_, consulta) => <EstadoAtencionTag estadoId={consulta.estadoAtencionId} />,
    },
    {
      title: 'Agente',
      dataIndex: 'agenteNombre',
      render: (nombre: string | null) => nombre ?? '—',
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, consulta) => (
        <Space size="small">
          {puedeAsignarse(consulta.estadoAtencionId) && usuario && (
            <Button
              type="link"
              onClick={() => asignar.mutate({ id: consulta.id, agenteId: usuario.id })}
            >
              Tomar
            </Button>
          )}
          {puedeCerrarse(consulta.estadoAtencionId) && (
            <>
              <Button
                type="link"
                onClick={() => setCierre({ id: consulta.id, estado: ATENCION.resuelto })}
              >
                Resolver
              </Button>
              <Popconfirm
                title="Descartar la consulta"
                description="Queda registrada, pero sin atención."
                okText="Descartar"
                cancelText="Cancelar"
                onConfirm={() =>
                  resolver.mutate({
                    id: consulta.id,
                    datos: { estado: ATENCION.descartado, notasAgente: null },
                  })
                }
              >
                <Button type="link">Descartar</Button>
              </Popconfirm>
            </>
          )}
        </Space>
      ),
    },
  ]

  const columnasFaqs: TableProps<FaqResponse>['columns'] = [
    { title: 'Categoría', dataIndex: 'categoria' },
    { title: 'Pregunta', dataIndex: 'pregunta', render: (texto: string) => <strong>{texto}</strong> },
    { title: 'Orden', dataIndex: 'orden', align: 'right', className: 'num' },
    {
      title: 'Consultada',
      dataIndex: 'vecesConsultada',
      align: 'right',
      className: 'num',
    },
    {
      title: 'Estado',
      dataIndex: 'activo',
      render: (activo: boolean) => (activo ? 'Activa' : 'Inactiva'),
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, faq) => (
        <Space size="small">
          <Button
            type="link"
            onClick={() => {
              setFaqEnEdicion(faq)
              setModalFaq(true)
            }}
          >
            Editar
          </Button>
          <Popconfirm
            title="Eliminar la pregunta"
            okText="Eliminar"
            cancelText="Cancelar"
            onConfirm={() => eliminarFaq.mutate(faq.id)}
          >
            <Button type="link">Eliminar</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <>
      <BarraSuperior titulo="Chatbot" />
      <div className="pagina">
        <AvisoError
          error={bandeja.error ?? faqs.error ?? asignar.error ?? resolver.error ?? eliminarFaq.error}
        />
        <Tabs
          items={[
            {
              key: 'bandeja',
              label: 'Consultas',
              children: (
                <>
                  <p className="texto-secundario" style={{ marginBottom: 16 }}>
                    Todo lo que pregunta la gente al asistente queda acá, resuelto o no. Las que piden asesor llegan
                    como pendientes.
                  </p>
                  <Table
                    rowKey="id"
                    columns={columnasBandeja}
                    dataSource={bandeja.data ?? []}
                    pagination={{ pageSize: 15 }}
                    loading={bandeja.isPending}
                    locale={{ emptyText: 'Todavía no hay consultas' }}
                  />
                </>
              ),
            },
            {
              key: 'faqs',
              label: 'Preguntas frecuentes',
              children: (
                <>
                  <div className="filtros">
                    <Button
                      type="primary"
                      onClick={() => {
                        setFaqEnEdicion(null)
                        setModalFaq(true)
                      }}
                    >
                      Nueva pregunta
                    </Button>
                  </div>
                  <Table
                    rowKey="id"
                    columns={columnasFaqs}
                    dataSource={faqs.data ?? []}
                    pagination={false}
                    loading={faqs.isPending}
                    locale={{ emptyText: 'Todavía no hay preguntas cargadas' }}
                  />
                  <p className="texto-secundario" style={{ marginTop: 16 }}>
                    El chatbot responde solo con estas preguntas: no inventa respuestas. La integración con WhatsApp
                    queda pendiente de que el cliente defina el proveedor.
                  </p>
                </>
              ),
            },
          ]}
        />
      </div>

      <ModalFaq abierto={modalFaq} faq={faqEnEdicion} onCerrar={() => setModalFaq(false)} />

      <Modal
        title="Resolver la consulta"
        open={cierre !== null}
        onCancel={() => {
          setCierre(null)
          setNotas('')
        }}
        onOk={confirmarCierre}
        okText="Resolver"
        cancelText="Cancelar"
        confirmLoading={resolver.isPending}
        destroyOnHidden
      >
        <Input.TextArea
          rows={3}
          value={notas}
          onChange={(evento) => setNotas(evento.target.value)}
          placeholder="Qué se hizo o qué se le respondió (opcional)"
        />
      </Modal>
    </>
  )
}
