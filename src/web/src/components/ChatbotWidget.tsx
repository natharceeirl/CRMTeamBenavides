import { useState } from 'react'
import { Button, Input } from 'antd'
import { CloseOutlined, MessageOutlined, SendOutlined } from '@ant-design/icons'

const opciones = ['Estado de mi orden', 'Horarios y ubicación', 'Preguntas frecuentes', 'Hablar con un asesor']

// Wireframe del widget: muestra una conversación de ejemplo, todavía sin conexión a la API.
export function ChatbotWidget() {
  const [abierto, setAbierto] = useState(false)

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
            <div className="mensaje bot">Hola, soy el asistente de Team Benavides. ¿En qué te ayudo?</div>
            <div className="chatbot-opciones">
              {opciones.map((opcion) => (
                <Button key={opcion} block>
                  {opcion}
                </Button>
              ))}
            </div>
            <div className="mensaje usuario">Estado de mi orden</div>
            <div className="mensaje bot">Escribe la placa, el número de serie o tu DNI.</div>
            <div className="mensaje usuario">4821-3A</div>
            <div className="mensaje bot">
              OT-000482 · Yamaha MT-03 2023: el presupuesto espera tu aprobación. Entrega estimada: 16/09, 17:00.
            </div>
          </div>
          <div className="chatbot-entrada">
            <Input id="chatbot-mensaje" placeholder="Escribe tu mensaje" />
            <Button type="primary" aria-label="Enviar mensaje" icon={<SendOutlined />} />
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
