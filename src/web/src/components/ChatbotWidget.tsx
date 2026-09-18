import { useState } from 'react'
import { Button, Form, Input } from 'antd'
import { CloseOutlined, MessageOutlined, SendOutlined } from '@ant-design/icons'
import { useConsultarChatbot, useFaqs, useSolicitarAgente } from '../api/chatbot'
import type { FaqResponse } from '../api/tipos'

type Mensaje = {
  de: 'bot' | 'usuario'
  texto: string
}

type CamposAgente = {
  nombreContacto?: string
  telefonoContacto: string
  motivo: string
}

const SALUDO: Mensaje = {
  de: 'bot',
  texto: 'Hola, soy el asistente de Team Benavides. ¿En qué te ayudo?',
}

export function ChatbotWidget() {
  const [abierto, setAbierto] = useState(false)
  const [mensajes, setMensajes] = useState<Mensaje[]>([SALUDO])
  const [texto, setTexto] = useState('')
  const [sugerencias, setSugerencias] = useState<FaqResponse[]>([])
  const [consultaId, setConsultaId] = useState<string | null>(null)
  const [ofreceAgente, setOfreceAgente] = useState(false)
  const [formularioAgente, setFormularioAgente] = useState(false)

  const faqs = useFaqs()
  const consultar = useConsultarChatbot()
  const solicitarAgente = useSolicitarAgente()

  const agregar = (mensaje: Mensaje) => setMensajes((previos) => [...previos, mensaje])

  const preguntar = async (mensaje: string) => {
    const limpio = mensaje.trim()
    if (!limpio) {
      return
    }

    agregar({ de: 'usuario', texto: limpio })
    setTexto('')
    setSugerencias([])

    try {
      const respuesta = await consultar.mutateAsync({
        mensaje: limpio,
        nombreContacto: null,
        telefonoContacto: null,
        canal: 'web',
      })

      agregar({ de: 'bot', texto: respuesta.mensajeRespuesta })
      setSugerencias(respuesta.sugerencias)
      setConsultaId(respuesta.consultaId)
      setOfreceAgente(true)
    } catch (fallo) {
      agregar({
        de: 'bot',
        texto: fallo instanceof Error ? fallo.message : 'No se pudo consultar.',
      })
      setOfreceAgente(true)
    }
  }

  const pedirAgente = async (campos: CamposAgente) => {
    try {
      const respuesta = await solicitarAgente.mutateAsync({
        consultaId,
        nombreContacto: campos.nombreContacto?.trim() ? campos.nombreContacto.trim() : null,
        telefonoContacto: campos.telefonoContacto.trim(),
        motivo: campos.motivo.trim(),
        canal: 'web',
      })

      agregar({ de: 'bot', texto: respuesta.mensaje })
      setFormularioAgente(false)
      setOfreceAgente(false)
    } catch (fallo) {
      agregar({
        de: 'bot',
        texto: fallo instanceof Error ? fallo.message : 'No se pudo registrar la solicitud.',
      })
    }
  }

  // Las preguntas frecuentes sirven de atajo mientras la conversación empieza.
  const atajos = (faqs.data ?? []).slice(0, 4)

  return (
    <>
      {abierto && (
        <section className="chatbot-panel" aria-label="Asistente virtual">
          <header className="chatbot-cabecera">
            <h2>Asistente Team Benavides</h2>
            <Button
              type="text"
              aria-label="Cerrar asistente"
              icon={<CloseOutlined />}
              onClick={() => setAbierto(false)}
            />
          </header>
          <div className="chatbot-mensajes">
            {mensajes.map((mensaje, indice) => (
              <div key={`${indice}-${mensaje.texto}`} className={`mensaje ${mensaje.de}`}>
                {mensaje.texto}
              </div>
            ))}

            {consultar.isPending && <div className="mensaje bot">Escribiendo…</div>}

            {mensajes.length === 1 && atajos.length > 0 && (
              <div className="chatbot-opciones">
                {atajos.map((faq) => (
                  <Button key={faq.id} block onClick={() => preguntar(faq.pregunta)}>
                    {faq.pregunta}
                  </Button>
                ))}
              </div>
            )}

            {sugerencias.length > 0 && (
              <div className="chatbot-opciones">
                {sugerencias.map((faq) => (
                  <Button key={faq.id} block onClick={() => preguntar(faq.pregunta)}>
                    {faq.pregunta}
                  </Button>
                ))}
              </div>
            )}

            {ofreceAgente && !formularioAgente && (
              <div className="chatbot-opciones">
                {/* Siempre disponible: la búsqueda por palabras clave a veces
                    responde algo que no era lo que el cliente preguntaba. */}
                <Button block onClick={() => setFormularioAgente(true)}>
                  ¿No era eso? Hablar con un asesor
                </Button>
              </div>
            )}

            {formularioAgente && (
              <Form<CamposAgente>
                layout="vertical"
                requiredMark={false}
                onFinish={pedirAgente}
                style={{ marginTop: 8 }}
              >
                <Form.Item label="Nombre" name="nombreContacto">
                  <Input placeholder="Cómo te llamas" />
                </Form.Item>
                <Form.Item
                  label="Teléfono"
                  name="telefonoContacto"
                  rules={[{ required: true, message: 'Necesitamos un teléfono para llamarte' }]}
                >
                  <Input placeholder="959 214 380" />
                </Form.Item>
                <Form.Item
                  label="Motivo"
                  name="motivo"
                  rules={[{ required: true, message: 'Cuéntanos en qué te ayudamos' }]}
                >
                  <Input.TextArea rows={2} />
                </Form.Item>
                <Button type="primary" htmlType="submit" block loading={solicitarAgente.isPending}>
                  Pedir que me contacten
                </Button>
              </Form>
            )}
          </div>
          <div className="chatbot-entrada">
            <Input
              id="chatbot-mensaje"
              placeholder="Escribe tu mensaje"
              value={texto}
              onChange={(evento) => setTexto(evento.target.value)}
              onPressEnter={() => preguntar(texto)}
              disabled={consultar.isPending}
            />
            <Button
              type="primary"
              aria-label="Enviar mensaje"
              icon={<SendOutlined />}
              loading={consultar.isPending}
              onClick={() => preguntar(texto)}
            />
          </div>
        </section>
      )}
      <Button
        type="primary"
        size="large"
        className="chatbot-boton"
        icon={abierto ? <CloseOutlined /> : <MessageOutlined />}
        onClick={() => setAbierto(!abierto)}
      >
        {abierto ? 'Cerrar' : 'Asistente'}
      </Button>
    </>
  )
}
