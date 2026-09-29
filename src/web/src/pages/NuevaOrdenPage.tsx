import type { Dayjs } from 'dayjs'
import { Button, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import { useNavigate } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { MODALIDADES_ATENCION, TIPOS_ATENCION, TIPOS_FALLA, useAbrirOrden } from '../api/ordenes'
import { useVehiculos } from '../api/vehiculos'
import { useTecnicos } from '../api/usuarios'
import { identificadorUnidad, lecturaMedidor, midePorHoras } from '../utils/unidades'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'

type Campos = {
  vehiculoId: string
  tecnicoAsignadoId?: string
  tipoAtencion: number
  modalidadAtencion: number
  tipoFalla?: number
  motivoFalla?: string
  observaciones?: string
  fechaEstimadaEntrega?: Dayjs | null
  lecturaIngreso?: number
}

const sinVacios = (valor?: string) => (valor?.trim() ? valor.trim() : null)

export function NuevaOrdenPage() {
  const navigate = useNavigate()
  const [formulario] = Form.useForm<Campos>()
  const { tienePermiso } = useSesion()
  const puedeAsignar = tienePermiso(PERMISOS.ordenesAsignarTecnico)

  const vehiculos = useVehiculos()
  const tecnicos = useTecnicos(puedeAsignar && tienePermiso(PERMISOS.usuariosVer))
  const abrir = useAbrirOrden()

  const vehiculoId = Form.useWatch('vehiculoId', formulario)
  const unidad = (vehiculos.data ?? []).find((vehiculo) => vehiculo.id === vehiculoId)
  const enHoras = unidad ? midePorHoras(unidad) : false

  const enviar = async (campos: Campos) => {
    const orden = await abrir.mutateAsync({
      vehiculoId: campos.vehiculoId,
      tecnicoAsignadoId: campos.tecnicoAsignadoId ?? null,
      tipoAtencion: campos.tipoAtencion,
      modalidadAtencion: campos.modalidadAtencion,
      tipoFalla: campos.tipoFalla ?? null,
      motivoFalla: sinVacios(campos.motivoFalla),
      observaciones: sinVacios(campos.observaciones),
      fechaEstimadaEntrega: campos.fechaEstimadaEntrega?.toISOString() ?? null,
      kilometrajeIngreso: !enHoras && campos.lecturaIngreso != null ? Math.round(campos.lecturaIngreso) : null,
      horasUsoIngreso: enHoras ? (campos.lecturaIngreso ?? null) : null,
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
          initialValues={{ tipoAtencion: 0, modalidadAtencion: 0 }}
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
                  placeholder="Busca por placa, serie, modelo o cliente"
                  options={(vehiculos.data ?? []).map((vehiculo) => ({
                    value: vehiculo.id,
                    label: `${vehiculo.marca} ${vehiculo.modelo} · ${identificadorUnidad(vehiculo)} · ${vehiculo.clienteNombre}`,
                  }))}
                />
              </Form.Item>
              <Form.Item
                label={enHoras ? 'Horas de uso al ingresar' : 'Kilometraje al ingresar'}
                name="lecturaIngreso"
                extra={unidad ? `Última lectura registrada: ${lecturaMedidor(unidad)}` : undefined}
              >
                <InputNumber min={0} precision={enHoras ? 1 : 0} style={{ width: '100%' }} />
              </Form.Item>
              {puedeAsignar && (
                <Form.Item label="Técnico" name="tecnicoAsignadoId">
                  <Select
                    allowClear
                    showSearch
                    optionFilterProp="label"
                    loading={tecnicos.isPending}
                    placeholder="Asignar técnico (opcional)"
                    options={(tecnicos.data ?? []).map((tecnico) => ({
                      value: tecnico.id,
                      label: tecnico.nombreCompleto,
                    }))}
                  />
                </Form.Item>
              )}
            </div>
          </section>

          <section className="bloque">
            <h2>Recepción</h2>
            <div className="formulario-grid">
              <Form.Item label="Tipo de atención" name="tipoAtencion">
                <Select options={TIPOS_ATENCION} />
              </Form.Item>
              <Form.Item label="Modalidad" name="modalidadAtencion">
                <Select options={MODALIDADES_ATENCION} />
              </Form.Item>
              <Form.Item label="Tipo de falla" name="tipoFalla">
                <Select allowClear placeholder="Sin clasificar" options={TIPOS_FALLA} />
              </Form.Item>
              <Form.Item label="Entrega estimada" name="fechaEstimadaEntrega">
                <DatePicker showTime={{ format: 'HH:mm' }} format="DD/MM/YYYY HH:mm" style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item label="Falla o pedido del cliente" name="motivoFalla" className="ancho-completo">
                <Input.TextArea rows={3} placeholder="Qué pide el cliente y qué síntomas reporta" />
              </Form.Item>
              <Form.Item label="Observaciones de recepción" name="observaciones" className="ancho-completo">
                <Input.TextArea rows={2} placeholder="Estado de la unidad al recibirla, accesorios, golpes…" />
              </Form.Item>
            </div>
          </section>
        </Form>
      </div>
    </>
  )
}
