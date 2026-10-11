import { useEffect, useState } from 'react'
import dayjs, { type Dayjs } from 'dayjs'
import { Button, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import { useNavigate, useSearchParams } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { useClientePorDocumento } from '../api/clientes'
import { useHistorialServicioUnidad } from '../api/portal'
import { TIPO_DOCUMENTO_CLIENTE, type AperturaOrdenRequest } from '../api/tipos'
import { fechaCorta } from '../utils/formato'
import {
  MODALIDADES_ATENCION,
  TIPOS_ATENCION,
  TIPOS_FALLA,
  motivoEntregaInvalida,
  nombresEstado,
  useAbrirOrden,
  useOrdenes,
} from '../api/ordenes'
import { useVehiculos } from '../api/vehiculos'
import { useCita, useVincularOrdenCita } from '../api/citas'
import { useTecnicos } from '../api/usuarios'
import {
  MEDIDOR_HORAS,
  TIPOS_UNIDAD,
  identificadorUnidad,
  lecturaActualUnidad,
  midePorHoras,
  textoLectura,
  ultimaLecturaRegistrada,
} from '../utils/unidades'
import {
  medidorPorTipoUnidad,
  motivoDocumentoInvalido,
  ordenAbiertaDeUnidad,
  tipoDocumentoSugerido,
} from '../utils/recepcion'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'

type CamposClienteNuevo = {
  tipoDocumento: number
  nombreCompleto: string
  telefono?: string
  email?: string
}

type CamposUnidadNueva = {
  tipoUnidad: number
  tipoMedidor: number
  marca: string
  modelo: string
  placa?: string
  numeroSerieVIN?: string
  color?: string
}

type Campos = {
  vehiculoId: string
  clienteNuevo: CamposClienteNuevo
  unidadNueva: CamposUnidadNueva
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

const unidadNuevaInicial: CamposUnidadNueva = {
  tipoUnidad: 0,
  tipoMedidor: medidorPorTipoUnidad(0),
  marca: 'Yamaha',
  modelo: '',
}

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
  // reduce a las suyas; si no existe, sus datos y los de su unidad se escriben
  // aquí mismo y se guardan junto con la orden.
  const [documento, setDocumento] = useState('')
  const [documentoBuscado, setDocumentoBuscado] = useState<string | null>(null)
  const clienteBuscado = useClientePorDocumento(documentoBuscado)
  const cliente = clienteBuscado.data ?? null
  const clienteNoEncontrado = Boolean(documentoBuscado) && clienteBuscado.isSuccess && !cliente
  // Un cliente nuevo llega con su unidad: hacen falta los dos permisos.
  const registraClienteNuevo = clienteNoEncontrado && puedeRegistrarCliente && puedeRegistrarUnidad
  const [quiereUnidadNueva, setQuiereUnidadNueva] = useState(false)
  const registraUnidadNueva = registraClienteNuevo || (Boolean(cliente) && quiereUnidadNueva && puedeRegistrarUnidad)

  const unidadesVisibles = cliente
    ? (vehiculos.data ?? []).filter((vehiculo) => vehiculo.clienteId === cliente.id)
    : (vehiculos.data ?? [])

  const buscarCliente = (valor: string) => {
    setDocumentoBuscado(valor.trim() || null)
    setQuiereUnidadNueva(false)
    formulario.setFieldsValue({ vehiculoId: undefined })
  }

  // El tipo de documento del cliente nuevo se propone por la cantidad de dígitos.
  useEffect(() => {
    if (clienteNoEncontrado && documentoBuscado) {
      formulario.setFieldsValue({ clienteNuevo: { tipoDocumento: tipoDocumentoSugerido(documentoBuscado) } })
    }
  }, [clienteNoEncontrado, documentoBuscado, formulario])

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
  const medidorUnidadNueva = Form.useWatch(['unidadNueva', 'tipoMedidor'], formulario)
  const unidad = registraUnidadNueva ? undefined : (vehiculos.data ?? []).find((vehiculo) => vehiculo.id === vehiculoId)
  const historial = useHistorialServicioUnidad(unidad ? unidad.id : null)
  const atencionesAnteriores = (historial.data ?? []).slice(0, ATENCIONES_A_MOSTRAR)
  let enHoras = false
  if (registraUnidadNueva) enHoras = medidorUnidadNueva === MEDIDOR_HORAS
  else if (unidad) enHoras = midePorHoras(unidad)
  // El medidor no retrocede: la lectura nueva se compara con la de la unidad y
  // con la de sus órdenes anteriores.
  const ordenesDeLaUnidad = useOrdenes({ vehiculoId: unidad?.id }, Boolean(unidad))
  const ultimaLectura = unidad
    ? ultimaLecturaRegistrada(enHoras, lecturaActualUnidad(unidad), ordenesDeLaUnidad.data ?? [])
    : null
  const textoUltimaLectura = ultimaLectura == null ? '—' : textoLectura(ultimaLectura, enHoras)
  let ayudaLectura: string | undefined
  if (registraUnidadNueva) ayudaLectura = 'Queda como la lectura actual de la unidad.'
  else if (unidad) ayudaLectura = `Última lectura registrada: ${textoUltimaLectura}`
  // Una unidad tiene una sola orden abierta; el backend lo exige, aquí se avisa antes.
  const ordenAbierta = unidad ? ordenAbiertaDeUnidad(ordenesDeLaUnidad.data) : null
  const textoOrdenAbierta = ordenAbierta
    ? `Ya tiene abierta la orden ${ordenAbierta.numeroOrden ?? ''} (${nombresEstado[ordenAbierta.estadoId]}). Entrégala o cancélala antes de abrir otra.`
    : null
  // Se avisa apenas llegan sus órdenes, sin esperar a que se intente guardar.
  const unidadElegidaId = unidad?.id
  useEffect(() => {
    if (unidadElegidaId) formulario.validateFields(['vehiculoId']).catch(() => undefined)
  }, [textoOrdenAbierta, unidadElegidaId, formulario])

  const enviar = async (campos: Campos) => {
    const lectura = campos.lecturaIngreso
    const base = {
      tecnicoAsignadoId: campos.tecnicoAsignadoId ?? null,
      tipoAtencion: campos.tipoAtencion,
      modalidadAtencion: campos.modalidadAtencion,
      tipoFalla: campos.tipoFalla ?? null,
      motivoFalla: sinVacios(campos.motivoFalla),
      observaciones: sinVacios(campos.observaciones),
      fechaEstimadaEntrega: campos.fechaEstimadaEntrega?.toISOString() ?? null,
      kilometrajeIngreso: !enHoras && lectura != null ? Math.round(lectura) : null,
      horasUsoIngreso: enHoras ? (lectura ?? null) : null,
    }

    // Lo que se registra se arma según lo que se ve en pantalla, no según los
    // valores que el formulario pudo guardar de una búsqueda anterior.
    const solicitud: AperturaOrdenRequest = registraUnidadNueva
      ? {
          ...base,
          vehiculoId: null,
          clienteId: registraClienteNuevo ? null : (cliente?.id ?? null),
          clienteNuevo: registraClienteNuevo
            ? {
                tipoDocumento: campos.clienteNuevo.tipoDocumento,
                numeroDocumento: documentoBuscado ?? '',
                nombreCompleto: campos.clienteNuevo.nombreCompleto.trim(),
                telefono: sinVacios(campos.clienteNuevo.telefono),
                email: sinVacios(campos.clienteNuevo.email),
              }
            : null,
          unidadNueva: {
            tipoUnidad: campos.unidadNueva.tipoUnidad,
            tipoMedidor: campos.unidadNueva.tipoMedidor,
            marca: campos.unidadNueva.marca.trim(),
            modelo: campos.unidadNueva.modelo.trim(),
            placa: sinVacios(campos.unidadNueva.placa)?.toUpperCase() ?? null,
            numeroSerieVIN: sinVacios(campos.unidadNueva.numeroSerieVIN)?.toUpperCase() ?? null,
            numeroMotor: null,
            anio: null,
            color: sinVacios(campos.unidadNueva.color),
          },
        }
      : { ...base, vehiculoId: campos.vehiculoId }

    const orden = await abrir.mutateAsync(solicitud)

    if (citaId) {
      // La orden ya existe: si el vínculo falla, se sigue a la orden igual y la
      // cita queda sin ella, a la vista en la agenda.
      await vincular.mutateAsync({ id: citaId, ordenServicioId: orden.id }).catch(() => undefined)
    }
    navigate(`/ordenes/${orden.id}`)
  }

  const ayudaDocumento = () => {
    if (!documentoBuscado || !clienteBuscado.isSuccess) {
      return 'Con el DNI o RUC la lista muestra solo las unidades del cliente; si es nuevo, se registra aquí mismo.'
    }
    if (cliente) {
      return `${cliente.nombreCompleto} · ${unidadesVisibles.length === 1 ? '1 unidad' : `${unidadesVisibles.length} unidades`}`
    }
    if (registraClienteNuevo) return `No hay un cliente con el documento ${documentoBuscado}: completa sus datos abajo.`
    return `No hay un cliente con el documento ${documentoBuscado}. Registrar clientes y unidades necesita permisos que tu usuario no tiene.`
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
          scrollToFirstError
          initialValues={{
            tipoAtencion: 0,
            modalidadAtencion: 0,
            clienteNuevo: { tipoDocumento: TIPO_DOCUMENTO_CLIENTE.dni },
            unidadNueva: unidadNuevaInicial,
          }}
        >
          <section className="bloque">
            <h2>Cliente y unidad</h2>
            <div className="formulario-grid">
              {puedeBuscarCliente && (
                <Form.Item label="Documento del cliente" className="ancho-completo" extra={ayudaDocumento()}>
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

              {registraClienteNuevo && (
                <div className="panel-formulario ancho-completo" style={{ marginBottom: 24 }}>
                  <h3 style={{ marginBottom: 12 }}>Cliente nuevo · {documentoBuscado}</h3>
                  <div className="formulario-grid">
                    <Form.Item
                      label="Tipo de documento"
                      name={['clienteNuevo', 'tipoDocumento']}
                      rules={[
                        {
                          validator: async (_, tipo: number) => {
                            const motivo = motivoDocumentoInvalido(tipo, documentoBuscado ?? '')
                            if (motivo) throw new Error(motivo)
                          },
                        },
                      ]}
                    >
                      <Select
                        options={[
                          { value: TIPO_DOCUMENTO_CLIENTE.dni, label: 'DNI' },
                          { value: TIPO_DOCUMENTO_CLIENTE.ruc, label: 'RUC' },
                          { value: TIPO_DOCUMENTO_CLIENTE.otro, label: 'Otro (carné de extranjería, pasaporte)' },
                        ]}
                      />
                    </Form.Item>
                    <Form.Item
                      label="Nombre completo o razón social"
                      name={['clienteNuevo', 'nombreCompleto']}
                      rules={[{ required: true, whitespace: true, message: 'Ingresa el nombre del cliente' }]}
                    >
                      <Input maxLength={200} />
                    </Form.Item>
                    <Form.Item label="Teléfono" name={['clienteNuevo', 'telefono']}>
                      <Input maxLength={30} />
                    </Form.Item>
                    <Form.Item
                      label="Correo"
                      name={['clienteNuevo', 'email']}
                      rules={[{ type: 'email', message: 'Correo inválido' }]}
                    >
                      <Input maxLength={150} />
                    </Form.Item>
                  </div>
                </div>
              )}

              {registraUnidadNueva ? (
                <div className="panel-formulario ancho-completo" style={{ marginBottom: 24 }}>
                  <div className="seccion-titulo" style={{ marginBottom: 12 }}>
                    <h3>Unidad nueva{cliente ? ` de ${cliente.nombreCompleto}` : ''}</h3>
                    {!registraClienteNuevo && (
                      <Button type="link" onClick={() => setQuiereUnidadNueva(false)}>
                        Elegir una unidad registrada
                      </Button>
                    )}
                  </div>
                  <div className="formulario-grid">
                    <Form.Item label="Tipo de unidad" name={['unidadNueva', 'tipoUnidad']}>
                      <Select
                        options={TIPOS_UNIDAD.map((tipo) => ({ value: tipo.value, label: tipo.label }))}
                        onChange={(tipo: number) =>
                          formulario.setFieldsValue({ unidadNueva: { tipoMedidor: medidorPorTipoUnidad(tipo) } })
                        }
                      />
                    </Form.Item>
                    <Form.Item label="Medidor" name={['unidadNueva', 'tipoMedidor']}>
                      <Select
                        options={[
                          { value: 0, label: 'Kilómetros' },
                          { value: MEDIDOR_HORAS, label: 'Horas de uso' },
                        ]}
                      />
                    </Form.Item>
                    <Form.Item
                      label="Marca"
                      name={['unidadNueva', 'marca']}
                      rules={[{ required: true, whitespace: true, message: 'Ingresa la marca' }]}
                    >
                      <Input maxLength={100} />
                    </Form.Item>
                    <Form.Item
                      label="Modelo"
                      name={['unidadNueva', 'modelo']}
                      rules={[{ required: true, whitespace: true, message: 'Ingresa el modelo' }]}
                    >
                      <Input maxLength={100} />
                    </Form.Item>
                    <Form.Item label="Placa" name={['unidadNueva', 'placa']} extra="Opcional: náutica y generadores no tienen.">
                      <Input maxLength={20} style={{ textTransform: 'uppercase' }} />
                    </Form.Item>
                    <Form.Item label="N.° de serie o VIN" name={['unidadNueva', 'numeroSerieVIN']}>
                      <Input maxLength={50} style={{ textTransform: 'uppercase' }} />
                    </Form.Item>
                    <Form.Item label="Color" name={['unidadNueva', 'color']}>
                      <Input maxLength={50} />
                    </Form.Item>
                  </div>
                </div>
              ) : (
                <Form.Item
                  label="Unidad"
                  name="vehiculoId"
                  className="ancho-completo"
                  rules={[
                    { required: true, message: 'Elige la unidad que ingresa al taller' },
                    {
                      validator: async () => {
                        if (textoOrdenAbierta) throw new Error(textoOrdenAbierta)
                      },
                    },
                  ]}
                  extra={
                    <>
                      {cliente && puedeRegistrarUnidad && (
                        <Button
                          type="link"
                          size="small"
                          style={{ paddingInline: 0 }}
                          onClick={() => {
                            setQuiereUnidadNueva(true)
                            formulario.setFieldsValue({ vehiculoId: undefined })
                          }}
                        >
                          + Registrar unidad nueva
                        </Button>
                      )}
                      {atencionesAnteriores.length > 0 ? (
                        <ul className="lista-simple">
                          {atencionesAnteriores.map((atencion) => (
                            <li key={atencion.ordenServicioId}>
                              {fechaCorta(atencion.fechaIngreso)} · {atencion.numeroOrden ?? 'Sin número'} · {atencion.estado}
                              {atencion.motivoFalla ? ` · ${atencion.motivoFalla}` : ''}
                            </li>
                          ))}
                        </ul>
                      ) : unidad && historial.isSuccess ? (
                        <div>Primera atención de esta unidad en el taller.</div>
                      ) : null}
                    </>
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
              )}

              <Form.Item
                label={enHoras ? 'Horas de uso al ingresar' : 'Kilometraje al ingresar'}
                name="lecturaIngreso"
                dependencies={['vehiculoId']}
                extra={ayudaLectura}
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
    </>
  )
}
