import { Button, Form, Input, Select } from 'antd'
import { useNavigate } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { useAbrirOrden } from '../api/ordenes'
import { useVehiculos } from '../api/vehiculos'
import { useUsuarios } from '../api/usuarios'

type Campos = {
  vehiculoId: string
  tecnicoAsignadoId?: string
  observaciones?: string
}

export function NuevaOrdenPage() {
  const navigate = useNavigate()
  const [formulario] = Form.useForm<Campos>()

  const vehiculos = useVehiculos()
  const usuarios = useUsuarios()
  const abrir = useAbrirOrden()

  const enviar = async (campos: Campos) => {
    const orden = await abrir.mutateAsync({
      vehiculoId: campos.vehiculoId,
      tecnicoAsignadoId: campos.tecnicoAsignadoId ?? null,
      observaciones: campos.observaciones?.trim() ? campos.observaciones.trim() : null,
    })

    navigate(`/ordenes/${orden.id}`)
  }

  return (
    <>
      <BarraSuperior
        antetitulo="Órdenes"
        titulo="Nueva orden de servicio"
        acciones={
          <>
            <Button onClick={() => navigate('/ordenes')}>Cancelar</Button>
            <Button type="primary" loading={abrir.isPending} onClick={() => formulario.submit()}>
              Abrir orden
            </Button>
          </>
        }
      />
      <div className="pagina">
        <AvisoError error={abrir.error} />
        <Form<Campos>
          form={formulario}
          layout="vertical"
          requiredMark={false}
          className="formulario"
          onFinish={enviar}
        >
          <section className="bloque">
            <h2>Unidad que ingresa</h2>
            <div className="formulario-grid">
              <Form.Item
                label="Unidad"
                name="vehiculoId"
                className="ancho-completo"
                rules={[{ required: true, message: 'Elige la unidad que ingresa al taller' }]}
              >
                <Select
                  showSearch
                  optionFilterProp="label"
                  loading={vehiculos.isPending}
                  placeholder="Busca por placa, modelo o cliente"
                  options={(vehiculos.data ?? []).map((vehiculo) => ({
                    value: vehiculo.id,
                    label: `${vehiculo.marca} ${vehiculo.modelo} · ${vehiculo.placa} · ${vehiculo.clienteNombre}`,
                  }))}
                />
              </Form.Item>
              <Form.Item label="Técnico" name="tecnicoAsignadoId">
                <Select
                  allowClear
                  showSearch
                  optionFilterProp="label"
                  loading={usuarios.isPending}
                  placeholder="Asignar técnico (opcional)"
                  options={(usuarios.data ?? []).map((usuario) => ({
                    value: usuario.id,
                    label: usuario.nombreCompleto,
                  }))}
                />
              </Form.Item>
            </div>
          </section>

          <section className="bloque">
            <h2>Recepción</h2>
            <div className="formulario-grid">
              <Form.Item label="Motivo de ingreso y observaciones" name="observaciones" className="ancho-completo">
                <Input.TextArea rows={3} placeholder="Qué pide el cliente y qué síntomas reporta" />
              </Form.Item>
            </div>
            {/* La lectura del medidor, la fecha de entrega prometida, la prioridad, los accesorios recibidos y las
                fotos de ingreso están en el mockup pero todavía no existen en la API. */}
          </section>
        </Form>
      </div>
    </>
  )
}
