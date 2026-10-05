import { useState } from 'react'
import dayjs, { type Dayjs } from 'dayjs'
import { Link, useNavigate } from 'react-router'
import { Button, DatePicker, Form, Input, Modal, Select } from 'antd'
import { useSesion } from '../auth/sesion'
import { ACCESO_ORDENES, PERMISOS, cumpleAcceso } from '../auth/acceso'
import {
  citaAntesDelTaller,
  nombresEstadoCita,
  siguientesEstadosCita,
  useCambiarEstadoCita,
  useCancelarCita,
  useActualizarCita,
  useCita,
  useReprogramarCita,
} from '../api/citas'
import { useVehiculos } from '../api/vehiculos'
import { ESTADO_CITA, type CitaDetalleResponse, type EstadoCita } from '../api/tipos'
import { AvisoError } from './AvisoError'
import { EstadoCitaTag } from './EstadoCitaTag'
import { HistorialEstados } from './HistorialEstados'
import { fechaHoraConAnio } from '../utils/formato'
import { identificadorUnidad } from '../utils/unidades'
import { DURACIONES_CITA, reglaFechaCita, textoDuracion } from '../utils/agenda'

type Props = {
  citaId: string | null
  onCerrar: () => void
}

/** Lo que se confirma en el modal chico: un cambio de estado o la cancelación, con su nota. */
type Paso = { tipo: 'estado'; destino: EstadoCita } | { tipo: 'cancelar' }

type CamposReprogramar = {
  fechaHora: Dayjs
  duracionMinutos: number
  motivo?: string
}

/** Cómo se lee cada paso en el botón. */
const accionEstado: Partial<Record<EstadoCita, string>> = {
  [ESTADO_CITA.confirmada]: 'Confirmar',
  [ESTADO_CITA.enTaller]: 'Llegó al taller',
  [ESTADO_CITA.completada]: 'Completar',
  [ESTADO_CITA.noAsistio]: 'No asistió',
}

/** Se monta cada vez que se abre, para empezar con la fecha vigente y no con la última que se escribió. */
function ModalReprogramar({ cita, onCerrar }: Readonly<{ cita: CitaDetalleResponse; onCerrar: () => void }>) {
  const [formulario] = Form.useForm<CamposReprogramar>()
  const reprogramar = useReprogramarCita()

  const guardar = async (campos: CamposReprogramar) => {
    await reprogramar.mutateAsync({
      id: cita.id,
      datos: {
        nuevaFechaHoraProgramada: campos.fechaHora.second(0).millisecond(0).toISOString(),
        nuevaDuracionMinutos: campos.duracionMinutos,
        motivoReprogramacion: campos.motivo?.trim() || null,
      },
    })
    onCerrar()
  }

  return (
    <Modal
      title="Reprogramar cita"
      open
      onCancel={onCerrar}
      onOk={() => formulario.submit()}
      okText="Reprogramar"
      cancelText="Volver"
      confirmLoading={reprogramar.isPending}
    >
      <AvisoError error={reprogramar.error} />
      <Form<CamposReprogramar>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={guardar}
        initialValues={{ fechaHora: dayjs(cita.fechaHoraProgramada), duracionMinutos: cita.duracionMinutos }}
      >
        <Form.Item
          label="Nueva fecha y hora"
          name="fechaHora"
          rules={[reglaFechaCita]}
        >
          <DatePicker
            showTime={{ format: 'HH:mm', minuteStep: 15 }}
            format="DD/MM/YYYY HH:mm"
            disabledDate={(dia) => dia.isBefore(dayjs(), 'day')}
            style={{ width: '100%' }}
          />
        </Form.Item>
        <Form.Item label="Duración" name="duracionMinutos">
          <Select options={DURACIONES_CITA} />
        </Form.Item>
        <Form.Item label="Motivo del cambio" name="motivo">
          <Input.TextArea rows={2} maxLength={500} placeholder="Opcional" />
        </Form.Item>
      </Form>
    </Modal>
  )
}

type CamposEditar = {
  vehiculoId: string
  motivo: string
  observaciones?: string
}

/** Unidad, motivo y observaciones. La fecha va por Reprogramar, que la anota en el historial. */
function ModalEditar({ cita, onCerrar }: Readonly<{ cita: CitaDetalleResponse; onCerrar: () => void }>) {
  const [formulario] = Form.useForm<CamposEditar>()
  const { tienePermiso } = useSesion()
  const unidades = useVehiculos(cita.clienteId, tienePermiso(PERMISOS.unidadesVer))
  const actualizar = useActualizarCita()

  const guardar = async (campos: CamposEditar) => {
    await actualizar.mutateAsync({
      id: cita.id,
      datos: {
        vehiculoId: campos.vehiculoId,
        fechaHoraProgramada: cita.fechaHoraProgramada,
        duracionMinutos: cita.duracionMinutos,
        motivo: campos.motivo.trim(),
        observaciones: campos.observaciones?.trim() || null,
      },
    })
    onCerrar()
  }

  return (
    <Modal
      title="Editar cita"
      open
      onCancel={onCerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Volver"
      confirmLoading={actualizar.isPending}
    >
      <AvisoError error={actualizar.error} />
      <Form<CamposEditar>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={guardar}
        initialValues={{ vehiculoId: cita.vehiculoId, motivo: cita.motivo, observaciones: cita.observaciones ?? '' }}
      >
        <Form.Item label="Unidad" name="vehiculoId" rules={[{ required: true, message: 'Elige la unidad' }]}>
          <Select
            loading={unidades.isPending}
            options={(unidades.data ?? [])
              .filter((unidad) => unidad.activo || unidad.id === cita.vehiculoId)
              .map((unidad) => ({
                value: unidad.id,
                label: `${unidad.marca} ${unidad.modelo} · ${identificadorUnidad(unidad)}`,
              }))}
          />
        </Form.Item>
        <Form.Item
          label="Motivo"
          name="motivo"
          rules={[{ required: true, whitespace: true, message: 'Escribe el motivo de la cita' }]}
        >
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
        <Form.Item label="Observaciones" name="observaciones">
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

export function ModalDetalleCita({ citaId, onCerrar }: Readonly<Props>) {
  const sesion = useSesion()
  const puedeEditar = sesion.tienePermiso(PERMISOS.citasEditar)
  const puedeCancelar = sesion.tienePermiso(PERMISOS.citasCancelar)
  const veOrdenes = cumpleAcceso(ACCESO_ORDENES, sesion)
  const puedeAbrirOrden = sesion.tienePermiso(PERMISOS.ordenesCrear)
  const navigate = useNavigate()

  const cita = useCita(citaId)
  const cambiarEstado = useCambiarEstadoCita()
  const cancelar = useCancelarCita()

  const [paso, setPaso] = useState<Paso | null>(null)
  const [nota, setNota] = useState('')
  const [reprogramando, setReprogramando] = useState(false)
  const [editando, setEditando] = useState(false)

  const datos = cita.data
  const antesDelTaller = datos ? citaAntesDelTaller(datos.estado) : false
  const destinos = datos && puedeEditar ? siguientesEstadosCita(datos.estado) : []
  // Recibir la unidad es abrir su orden: desde la cita, con la unidad y el motivo ya puestos.
  const abreOrden =
    datos !== undefined &&
    puedeAbrirOrden &&
    !datos.ordenServicioId &&
    (antesDelTaller || datos.estado === ESTADO_CITA.enTaller)

  const cerrarPaso = () => {
    setPaso(null)
    setNota('')
    cambiarEstado.reset()
    cancelar.reset()
  }

  const cerrar = () => {
    cerrarPaso()
    setReprogramando(false)
    setEditando(false)
    onCerrar()
  }

  const confirmarPaso = async () => {
    if (!datos || !paso) return
    if (paso.tipo === 'cancelar') {
      await cancelar.mutateAsync({ id: datos.id, motivo: nota.trim() })
    } else {
      await cambiarEstado.mutateAsync({ id: datos.id, nuevoEstado: paso.destino, observacion: nota.trim() || null })
    }
    cerrarPaso()
  }

  return (
    <Modal
      title={datos ? `Cita ${datos.numeroCita}` : 'Cita'}
      open={citaId !== null}
      onCancel={cerrar}
      onOk={cerrar}
      okText="Cerrar"
      cancelButtonProps={{ style: { display: 'none' } }}
      width={760}
      destroyOnHidden
    >
      <AvisoError error={cita.error} />
      {datos && (
        <>
          <table className="tabla-simple">
            <tbody>
              <tr>
                <td>Cliente</td>
                <td>{datos.clienteNombre}</td>
              </tr>
              <tr>
                <td>Teléfono</td>
                <td>{datos.clienteTelefono ?? '—'}</td>
              </tr>
              <tr>
                <td>Unidad</td>
                <td>
                  {datos.vehiculoMarca} {datos.vehiculoModelo} · {datos.vehiculoPlaca ?? 'sin placa'}
                </td>
              </tr>
              <tr>
                <td>Fecha y hora</td>
                <td className="sin-salto">{fechaHoraConAnio(datos.fechaHoraProgramada)}</td>
              </tr>
              <tr>
                <td>Duración</td>
                <td>{textoDuracion(datos.duracionMinutos)}</td>
              </tr>
              <tr>
                <td>Motivo</td>
                <td>{datos.motivo}</td>
              </tr>
              {datos.observaciones && (
                <tr>
                  <td>Observaciones</td>
                  <td>{datos.observaciones}</td>
                </tr>
              )}
              <tr>
                <td>Estado</td>
                <td>
                  <EstadoCitaTag estado={datos.estado} />
                </td>
              </tr>
              {datos.motivoCancelacion && (
                <tr>
                  <td>Motivo de cancelación</td>
                  <td>{datos.motivoCancelacion}</td>
                </tr>
              )}
              {datos.ordenServicioId && (
                <tr>
                  <td>Orden de servicio</td>
                  <td>
                    {veOrdenes ? (
                      <Link to={`/ordenes/${datos.ordenServicioId}`}>{datos.numeroOrdenServicio ?? 'Ver orden'}</Link>
                    ) : (
                      (datos.numeroOrdenServicio ?? '—')
                    )}
                  </td>
                </tr>
              )}
            </tbody>
          </table>

          <section style={{ marginTop: 28 }}>
            <div className="seccion-titulo">
              <h2>Seguimiento</h2>
              <div className="acciones">
                {antesDelTaller && puedeCancelar && (
                  <Button onClick={() => setPaso({ tipo: 'cancelar' })}>Cancelar cita</Button>
                )}
                {antesDelTaller && puedeEditar && <Button onClick={() => setEditando(true)}>Editar</Button>}
                {antesDelTaller && puedeEditar && <Button onClick={() => setReprogramando(true)}>Reprogramar</Button>}
                {abreOrden && (
                  <Button
                    onClick={() => {
                      cerrar()
                      navigate(`/ordenes/nueva?cita=${datos.id}`)
                    }}
                  >
                    Abrir orden
                  </Button>
                )}
                {destinos.map((destino, indice) => (
                  <Button
                    key={destino}
                    type={indice === 0 ? 'primary' : 'default'}
                    onClick={() => setPaso({ tipo: 'estado', destino })}
                  >
                    {accionEstado[destino]}
                  </Button>
                ))}
              </div>
            </div>
            <HistorialEstados cambios={datos.historial} nombres={nombresEstadoCita} />
          </section>

          {reprogramando && <ModalReprogramar cita={datos} onCerrar={() => setReprogramando(false)} />}
          {editando && <ModalEditar cita={datos} onCerrar={() => setEditando(false)} />}
        </>
      )}

      <Modal
        title={paso?.tipo === 'estado' ? `Pasar a «${nombresEstadoCita[paso.destino]}»` : 'Cancelar cita'}
        open={paso !== null}
        onCancel={cerrarPaso}
        onOk={confirmarPaso}
        okText={paso?.tipo === 'cancelar' ? 'Cancelar cita' : 'Confirmar'}
        cancelText="Volver"
        okButtonProps={{ disabled: paso?.tipo === 'cancelar' && !nota.trim() }}
        confirmLoading={cambiarEstado.isPending || cancelar.isPending}
        destroyOnHidden
      >
        <AvisoError error={cambiarEstado.error ?? cancelar.error} />
        <Input.TextArea
          rows={3}
          maxLength={500}
          value={nota}
          onChange={(evento) => setNota(evento.target.value)}
          placeholder={paso?.tipo === 'cancelar' ? 'Motivo de la cancelación' : 'Observación (opcional)'}
        />
      </Modal>
    </Modal>
  )
}
