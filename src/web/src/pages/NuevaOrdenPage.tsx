import { useState } from 'react'
import { Button, Checkbox, DatePicker, Form, Input, InputNumber, Segmented, Select, Upload } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { useNavigate } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { clientes, identificadorUnidad, nombreUnidad, ordenes, unidades } from '../data/ejemplo'
import { entero } from '../utils/formato'

const tecnicos = [...new Set(ordenes.map((orden) => orden.tecnico))]
const accesorios = ['Casco', 'Llaves', 'Espejos', 'Tapa de tanque', 'Documentos']

export function NuevaOrdenPage() {
  const navigate = useNavigate()
  const [clienteId, setClienteId] = useState<string>()
  const [unidadId, setUnidadId] = useState<string>()

  const unidadesCliente = unidades.filter((unidad) => unidad.clienteId === clienteId)
  const unidad = unidades.find((item) => item.id === unidadId)

  return (
    <>
      <BarraSuperior
        antetitulo="Órdenes"
        titulo="Nueva orden de servicio"
        acciones={
          <>
            <Button onClick={() => navigate('/ordenes')}>Cancelar</Button>
            <Button type="primary" onClick={() => navigate('/ordenes')}>
              Abrir orden
            </Button>
          </>
        }
      />
      <div className="pagina">
        <Form layout="vertical" requiredMark={false} className="formulario">
          <section className="bloque">
            <h2>Cliente y unidad</h2>
            <div className="formulario-grid">
              <Form.Item label="Cliente" htmlFor="cliente">
                <Select<string>
                  id="cliente"
                  showSearch
                  optionFilterProp="label"
                  placeholder="Busca por nombre o documento"
                  value={clienteId}
                  onChange={(valor) => {
                    setClienteId(valor)
                    setUnidadId(undefined)
                  }}
                  options={clientes.map((cliente) => ({
                    value: cliente.id,
                    label: `${cliente.nombre} · ${cliente.tipoDocumento} ${cliente.documento}`,
                  }))}
                />
              </Form.Item>
              <Form.Item label="Unidad" htmlFor="unidad">
                <Select<string>
                  id="unidad"
                  placeholder={clienteId ? 'Elige la unidad' : 'Primero elige un cliente'}
                  disabled={!clienteId}
                  value={unidadId}
                  onChange={setUnidadId}
                  options={unidadesCliente.map((item) => ({
                    value: item.id,
                    label: `${nombreUnidad(item)} · ${identificadorUnidad(item)}`,
                  }))}
                />
              </Form.Item>
            </div>
          </section>

          <section className="bloque">
            <h2>Recepción</h2>
            <div className="formulario-grid">
              <Form.Item label="Motivo de ingreso" htmlFor="motivo" className="ancho-completo">
                <Input.TextArea id="motivo" rows={3} placeholder="Qué pide el cliente y qué síntomas reporta" />
              </Form.Item>
              <Form.Item
                label={`Lectura del medidor (${unidad?.unidadMedidor === 'h' ? 'horas' : 'km'})`}
                htmlFor="medidor"
              >
                <InputNumber
                  id="medidor"
                  min={0}
                  style={{ width: '100%' }}
                  placeholder={unidad ? `Última lectura: ${entero(unidad.medidor)}` : undefined}
                />
              </Form.Item>
              <Form.Item label="Entrega estimada" htmlFor="entrega">
                <DatePicker
                  id="entrega"
                  showTime
                  format="DD/MM/YYYY HH:mm"
                  placeholder="Fecha y hora"
                  style={{ width: '100%' }}
                />
              </Form.Item>
              <Form.Item label="Técnico" htmlFor="tecnico">
                <Select<string>
                  id="tecnico"
                  placeholder="Asignar técnico"
                  options={tecnicos.map((tecnico) => ({ value: tecnico, label: tecnico }))}
                />
              </Form.Item>
              <Form.Item label="Prioridad">
                <Segmented options={['Normal', 'Urgente']} defaultValue="Normal" />
              </Form.Item>
            </div>
          </section>

          <section className="bloque">
            <h2>Accesorios entregados</h2>
            <Checkbox.Group options={accesorios} />
          </section>

          <section className="bloque">
            <h2>Fotos de ingreso</h2>
            <Upload listType="picture-card" accept="image/*" multiple beforeUpload={() => false}>
              <button type="button" className="subir-foto">
                <PlusOutlined />
                <span>Agregar foto</span>
              </button>
            </Upload>
          </section>
        </Form>
      </div>
    </>
  )
}
