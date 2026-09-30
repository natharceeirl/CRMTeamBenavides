import { useState } from 'react'
import { Button, Input, Modal } from 'antd'
import {
  GERENCIA,
  PRESUPUESTO,
  esEstadoTerminal,
  nombresGerencia,
  nombresPresupuesto,
  useAprobacionGerencia,
  useResponderPresupuesto,
} from '../api/ordenes'
import type { OrdenServicioDetalleResponse } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { fechaHora } from '../utils/formato'
import { AvisoError } from './AvisoError'
import { EtiquetaEstado, type TonoEstado } from './EtiquetaEstado'

type Props = {
  orden: OrdenServicioDetalleResponse
}

type Accion = {
  destino: 'cliente' | 'gerencia'
  estado: number
  titulo: string
  /** El rechazo siempre lleva motivo: queda en el historial para quien retome la orden. */
  motivoObligatorio: boolean
}

// Lo pendiente pide acción; lo aprobado queda sobrio; lo rechazado, en suave.
const tonoPresupuesto: Record<number, TonoEstado> = {
  [PRESUPUESTO.pendiente]: 'alerta',
  [PRESUPUESTO.aprobado]: 'hecho',
  [PRESUPUESTO.rechazado]: 'suave',
}

const tonoGerencia: Record<number, TonoEstado> = {
  [GERENCIA.noAplica]: 'apagado',
  [GERENCIA.pendiente]: 'alerta',
  [GERENCIA.aprobado]: 'hecho',
  [GERENCIA.rechazado]: 'suave',
}

/**
 * Respuesta del cliente al presupuesto y aprobación de Gerencia. Son decisiones
 * separadas: la del cliente la registra el personal (en la app la da el propio
 * cliente) y la de Gerencia solo quien tiene `ordenes.aprobar_gerencia`.
 */
export function PanelAprobaciones({ orden }: Readonly<Props>) {
  const sesion = useSesion()
  const { tienePermiso, tieneAlgunPermiso, esCliente, esPersonal } = sesion
  const esSoloCliente = esCliente && !esPersonal
  const puedeRegistrarRespuesta = tieneAlgunPermiso([
    PERMISOS.ordenesEditar,
    PERMISOS.ventasCrear,
    PERMISOS.portalAcceso,
  ])
  const puedeDecidirGerencia = tienePermiso(PERMISOS.ordenesAprobarGerencia)
  const cerrada = esEstadoTerminal(orden.estadoId)

  const responder = useResponderPresupuesto()
  const gerencia = useAprobacionGerencia()
  const [accion, setAccion] = useState<Accion | null>(null)
  const [observacion, setObservacion] = useState('')

  const presupuesto = orden.estadoPresupuestoClienteId ?? PRESUPUESTO.pendiente
  const aprobacion = orden.estadoAprobacionGerenciaId ?? GERENCIA.noAplica

  const cerrar = () => {
    setAccion(null)
    setObservacion('')
    responder.reset()
    gerencia.reset()
  }

  const confirmar = async () => {
    if (!accion) return
    const datos = { estado: accion.estado, observaciones: observacion.trim() || null }
    if (accion.destino === 'cliente') {
      await responder.mutateAsync({ id: orden.id, datos })
    } else {
      await gerencia.mutateAsync({ id: orden.id, datos })
    }
    cerrar()
  }

  const abrir = (nueva: Accion) => {
    setObservacion('')
    setAccion(nueva)
  }

  return (
    <section>
      <div className="seccion-titulo">
        <h2>Aprobaciones</h2>
      </div>
      <div className="aprobaciones">
        <div className="aprobacion">
          <div className="aprobacion-cabecera">
            <span>Presupuesto del cliente</span>
            <EtiquetaEstado tono={tonoPresupuesto[presupuesto]}>{nombresPresupuesto[presupuesto]}</EtiquetaEstado>
          </div>
          {orden.fechaRespuestaCliente && (
            <div className="texto-secundario">{fechaHora(orden.fechaRespuestaCliente)}</div>
          )}
          {orden.observacionesPresupuestoCliente && (
            <p className="aprobacion-nota">{orden.observacionesPresupuestoCliente}</p>
          )}
          {!cerrada && puedeRegistrarRespuesta && (
            <div className="aprobacion-acciones">
              {presupuesto !== PRESUPUESTO.aprobado && (
                <Button
                  type={esSoloCliente ? 'primary' : 'default'}
                  onClick={() =>
                    abrir({
                      destino: 'cliente',
                      estado: PRESUPUESTO.aprobado,
                      titulo: esSoloCliente ? 'Aprobar presupuesto' : 'El cliente aprobó el presupuesto',
                      motivoObligatorio: false,
                    })
                  }
                >
                  {esSoloCliente ? 'Aprobar presupuesto' : 'El cliente aprobó'}
                </Button>
              )}
              {presupuesto !== PRESUPUESTO.rechazado && (
                <Button
                  danger
                  onClick={() =>
                    abrir({
                      destino: 'cliente',
                      estado: PRESUPUESTO.rechazado,
                      titulo: esSoloCliente ? 'Rechazar presupuesto' : 'El cliente rechazó el presupuesto',
                      motivoObligatorio: true,
                    })
                  }
                >
                  {esSoloCliente ? 'Rechazar presupuesto' : 'El cliente rechazó'}
                </Button>
              )}
            </div>
          )}
        </div>

        <div className="aprobacion">
          <div className="aprobacion-cabecera">
            <span>Gerencia</span>
            <EtiquetaEstado tono={tonoGerencia[aprobacion]}>{nombresGerencia[aprobacion]}</EtiquetaEstado>
          </div>
          {orden.fechaAprobacionGerencia && aprobacion !== GERENCIA.noAplica && (
            <div className="texto-secundario">
              {fechaHora(orden.fechaAprobacionGerencia)}
              {orden.usuarioAprobacionGerenciaNombre ? ` · ${orden.usuarioAprobacionGerenciaNombre}` : ''}
            </div>
          )}
          {orden.observacionesAprobacionGerencia && (
            <p className="aprobacion-nota">{orden.observacionesAprobacionGerencia}</p>
          )}
          {!cerrada && puedeDecidirGerencia && (
            <div className="aprobacion-acciones">
              {aprobacion !== GERENCIA.aprobado && (
                <Button
                  type="primary"
                  onClick={() =>
                    abrir({
                      destino: 'gerencia',
                      estado: GERENCIA.aprobado,
                      titulo: 'Aprobar como Gerencia',
                      motivoObligatorio: false,
                    })
                  }
                >
                  Aprobar
                </Button>
              )}
              {aprobacion !== GERENCIA.rechazado && (
                <Button
                  danger
                  onClick={() =>
                    abrir({
                      destino: 'gerencia',
                      estado: GERENCIA.rechazado,
                      titulo: 'Rechazar como Gerencia',
                      motivoObligatorio: true,
                    })
                  }
                >
                  Rechazar
                </Button>
              )}
              {aprobacion === GERENCIA.noAplica && (
                <Button
                  onClick={() =>
                    abrir({
                      destino: 'gerencia',
                      estado: GERENCIA.pendiente,
                      titulo: 'Requerir aprobación de Gerencia',
                      motivoObligatorio: true,
                    })
                  }
                >
                  Requerir aprobación
                </Button>
              )}
            </div>
          )}
        </div>
      </div>

      <Modal
        title={accion?.titulo}
        open={accion !== null}
        onCancel={cerrar}
        onOk={confirmar}
        okText="Confirmar"
        cancelText="Cancelar"
        okButtonProps={{ disabled: Boolean(accion?.motivoObligatorio) && !observacion.trim() }}
        confirmLoading={responder.isPending || gerencia.isPending}
        destroyOnHidden
      >
        <AvisoError error={responder.error ?? gerencia.error} />
        {accion?.destino === 'gerencia' && accion.estado === GERENCIA.pendiente && (
          <p>Mientras esté pendiente, la orden no puede pasar a «Aprobada».</p>
        )}
        <Input.TextArea
          rows={3}
          value={observacion}
          onChange={(evento) => setObservacion(evento.target.value)}
          placeholder={accion?.motivoObligatorio ? 'Motivo (obligatorio)' : 'Observación (opcional)'}
        />
        <p className="texto-secundario" style={{ marginTop: 8 }}>
          Queda en el historial de la orden con tu usuario y la hora.
        </p>
      </Modal>
    </section>
  )
}
