import { useEffect, useState } from 'react'
import dayjs, { type Dayjs } from 'dayjs'
import { Button, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import { useNavigate, useSearchParams } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalAltaRapidaCliente, ModalAltaRapidaUnidad } from '../components/ModalesAltaRapida'
import { useClientePorDocumento } from '../api/clientes'
import { useHistorialServicioUnidad } from '../api/portal'
import type { ClienteResponse } from '../api/tipos'
import { fechaCorta } from '../utils/formato'
import {
  MODALIDADES_ATENCION,
  TIPOS_ATENCION,
  TIPOS_FALLA,
  motivoEntregaInvalida,
  useAbrirOrden,
  useOrdenes,
} from '../api/ordenes'
import { useVehiculos } from '../api/vehiculos'
import { useCita, useVincularOrdenCita } from '../api/citas'
import { useTecnicos } from '../api/usuarios'
import {
  identificadorUnidad,
  lecturaActualUnidad,
  midePorHoras,
  textoLectura,
  ultimaLecturaRegistrada,
} from '../utils/unidades'
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

// Las atenciones anteriores que se muestran al elegir la unidad.
const ATENCIONES_A_MOSTRAR = 3

export function NuevaOrdenPage() {
  const navigate = useNavigate()
  const [formulario] = Form.useForm<Campos>()
  const { tienePermiso } = useSesion()
  const puedeAsignar = tienePermiso(PERMISOS.ordenesAsignarTecnico)
  const puedeBuscarCliente = tienePermiso(PERMISOS.clientesVer)
  const puedeRegistrarCliente = tienePermiso(PERMISOS.clientesCrear)
  const puedeRegistrarUnidad = tienePermiso(PERMISOS.unidadesCrear)

  const vehiculos = useVehiculos()
  const tecnicos = useTecnicos(puedeAsignar && tienePermiso(PERMISOS.usuariosVer))
  const abrir = useAbrirOrden()

  // Recepción por documento: con el cliente encontrado, la lista de unidades se
  // reduce a las suyas; si no existe, se registra ahí mismo.
  const [documento, setDocumento] = useState('')
  const [documentoBuscado, setDocumentoBuscado] = useState<string | null>(null)
  const clienteBuscado = useClientePorDocumento(documentoBuscado)
  const cliente = clienteBuscado.data ?? null
  const [modalCliente, setModalCliente] = useState(false)
  const [clienteDeUnidadNueva, setClienteDeUnidadNueva] = useState<Pick<ClienteResponse, 'id' | 'nombreCompleto'> | null>(
    null,
  )
  const unidadesVisibles = cliente
    ? (vehiculos.data ?? []).filter((vehiculo) => vehiculo.clienteId === cliente.id)
    : (vehiculos.data ?? [])

  const buscarCliente = (valor: string) => {
    setDocumentoBuscado(valor.trim() || null)
    formulario.setFieldsValue({ vehiculoId: undefined })
  }

  // Si el cliente tiene una sola unidad, queda elegida.
  const unicaUnidad = cliente && unidadesVisibles.length === 1 ? unidadesVisibles[0].id : null
  useEffect(() => {
    if (unicaUnidad && !formulario.getFieldValue('vehiculoId')) {
      formulario.setFieldsValue({ vehiculoId: unicaUnidad })
    }
  }, [unicaUnidad, formulario])

  // Desde la agenda: la orden se abre con la unidad y el motivo de la cita, y al
  // guardarse queda vinculada a ella.
  const [parametros] = useSearchParams()
  const citaId = parametros.get('cita')
  const cita = useCita(citaId)
  const vincular = useVincularOrdenCita()
  const datosCita = cita.data
  useEffect(() => {
    if (!datosCita) return
    formulario.setFieldsValue({
      vehiculoId: datosCita.vehiculoId,
      motivoFalla: datosCita.motivo,
      observaciones: datosCita.observaciones ?? undefined,
    })
  }, [datosCita, formulario])

  const vehiculoId = Form.useWatch('vehiculoId', formulario)
  const unidad = (vehiculos.data ?? []).find((vehiculo) => vehiculo.id === vehiculoId)
  const historial = useHistorialServicioUnidad(vehiculoId ?? null)
  const atencionesAnteriores = (historial.data ?? []).slice(0, ATENCIONES_A_MOSTRAR)
  const enHoras = unidad ? midePorHoras(unidad) : false
  // El medidor no retrocede: la lectura nueva se compara con la de la unidad y
  // con la de sus órdenes anteriores.
  const ordenesDeLaUnidad = useOrdenes({ vehiculoId }, Boolean(vehiculoId))
  const ultimaLectura = unidad
    ? ultimaLecturaRegistrada(enHoras, lecturaActualUnidad(unidad), ordenesDeLaUnidad.data ?? [])
    : null
  const textoUltimaLectura = ultimaLectura == null ? '—' : textoLectura(ultimaLectura, enHoras)

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

    if (citaId) {
      // La orden ya existe: si el vínculo falla, se sigue a la orden igual y la
      // cita queda sin ella, a la vista en la agenda.
      await vincular.mutateAsync({ id: citaId, ordenServicioId: orden.id }).catch(() => undefined)
    }
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
        <AvisoError error={abrir.error ?? cita.error ?? clienteBuscado.error} />
        {datosCita && (
          <p className="texto-secundario" style={{ margin: 0 }}>
            Se abre desde la cita {datosCita.numeroCita} de {datosCita.clienteNombre}. Al guardarla, la cita queda
            «En taller» con esta orden.
          </p>
        )}
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
              {puedeBuscarCliente && (
                <Form.Item
                  label="Documento del cliente"
                  className="ancho-completo"
                  extra={
                    documentoBuscado && clienteBuscado.isSuccess ? (
                      cliente ? (
                        <>
                          {cliente.nombreCompleto} ·{' '}
                          {unidadesVisibles.length === 1 ? '1 unidad' : `${unidadesVisibles.length} unidades`}
                          {puedeRegistrarUnidad && (
                            <Button type="link" size="small" onClick={() => setClienteDeUnidadNueva(cliente)}>
                              Registrar unidad
                            </Button>
                          )}
                        </>
                      ) : (
                        <>
                          No hay un cliente con el documento {documentoBuscado}.
                          {puedeRegistrarCliente && (
                            <Button type="link" size="small" onClick={() => setModalCliente(true)}>
                              Registrar cliente
                            </Button>
                          )}
                        </>
                      )
                    ) : (
                      'Opcional: con el DNI o RUC la lista muestra solo las unidades del cliente.'
                    )
                  }
                >
                  <Input.Search
                    allowClear
                    enterButton="Buscar"
                    placeholder="DNI o RUC"
                    value={documento}
                    onChange={(evento) => setDocumento(evento.target.value)}
                    onSearch={buscarCliente}
                    loading={clienteBuscado.isFetching}
                    style={{ maxWidth: 360 }}
                  />
                </Form.Item>
              )}
              <Form.Item
                label="Unidad"
                name="vehiculoId"
                className="ancho-completo"
                rules={[{ required: true, message: 'Elige la unidad que ingresa al taller' }]}
                extra={
                  atencionesAnteriores.length > 0 ? (
                    <ul className="lista-simple">
                      {atencionesAnteriores.map((atencion) => (
                        <li key={atencion.ordenServicioId}>
                          {fechaCorta(atencion.fechaIngreso)} · {atencion.numeroOrden ?? 'Sin número'} · {atencion.estado}
                          {atencion.motivoFalla ? ` · ${atencion.motivoFalla}` : ''}
                        </li>
                      ))}
                    </ul>
                  ) : unidad && historial.isSuccess ? (
                    'Primera atención de esta unidad en el taller.'
                  ) : undefined
                }
              >
                <Select
                  showSearch
                  optionFilterProp="label"
                  loading={vehiculos.isPending}
                  placeholder={cliente ? 'Elige una unidad del cliente' : 'Busca por placa, serie, modelo o cliente'}
                  notFoundContent={cliente ? 'El cliente no tiene unidades registradas' : undefined}
                  options={unidadesVisibles.map((vehiculo) => ({
                    value: vehiculo.id,
                    label: `${vehiculo.marca} ${vehiculo.modelo} · ${identificadorUnidad(vehiculo)} · ${vehiculo.clienteNombre}`,
                  }))}
                />
              </Form.Item>
              <Form.Item
                label={enHoras ? 'Horas de uso al ingresar' : 'Kilometraje al ingresar'}
                name="lecturaIngreso"
                dependencies={['vehiculoId']}
                extra={unidad ? `Última lectura registrada: ${textoUltimaLectura}` : undefined}
                rules={[
                  {
                    validator: async (_, lectura: number | null | undefined) => {
                      if (lectura != null && ultimaLectura != null && lectura < ultimaLectura) {
                        throw new Error(
                          `No puede ser menor que la última lectura registrada (${textoLectura(ultimaLectura, enHoras)}).`,
                        )
                      }
                    },
                  },
                ]}
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
              <Form.Item
                label="Entrega estimada"
                name="fechaEstimadaEntrega"
                rules={[
                  {
                    // La unidad ingresa ahora: la entrega no puede quedar en el pasado.
                    validator: async (_, entrega: Dayjs | null | undefined) => {
                      const motivo = motivoEntregaInvalida(entrega, dayjs())
                      if (motivo) throw new Error(motivo)
                    },
                  },
                ]}
              >
                <DatePicker
                  showTime={{ format: 'HH:mm' }}
                  format="DD/MM/YYYY HH:mm"
                  disabledDate={(dia) => dia.isBefore(dayjs(), 'day')}
                  style={{ width: '100%' }}
                />
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

      <ModalAltaRapidaCliente
        abierto={modalCliente}
        numeroDocumento={documentoBuscado ?? ''}
        onCerrar={() => setModalCliente(false)}
        onRegistrado={(nuevo) => {
          setModalCliente(false)
          const numero = nuevo.numeroDocumento ?? nuevo.documentoIdentidad ?? documentoBuscado
          setDocumento(numero ?? '')
          setDocumentoBuscado(numero)
          // Un cliente nuevo todavía no tiene unidades: se pasa directo a registrar la suya.
          if (puedeRegistrarUnidad) setClienteDeUnidadNueva(nuevo)
        }}
      />
      {clienteDeUnidadNueva && (
        <ModalAltaRapidaUnidad
          abierto
          cliente={clienteDeUnidadNueva}
          onCerrar={() => setClienteDeUnidadNueva(null)}
          onRegistrada={(nueva) => {
            setClienteDeUnidadNueva(null)
            formulario.setFieldsValue({ vehiculoId: nueva.id })
          }}
        />
      )}
    </>
  )
}
