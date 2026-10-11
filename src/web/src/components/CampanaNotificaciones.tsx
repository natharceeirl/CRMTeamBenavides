import { useState } from 'react'
import { useNavigate } from 'react-router'
import { Badge, Button, Drawer, Grid, Popover } from 'antd'
import { BellOutlined } from '@ant-design/icons'
import {
  rutaDeNotificacion,
  useConteoNotificaciones,
  useMarcarNotificacionLeida,
  useMarcarTodasLeidas,
  useNotificaciones,
} from '../api/notificaciones'
import type { NotificacionResponse } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { fechaHora } from '../utils/formato'
import { AvisoError } from './AvisoError'

/**
 * Campanita con los avisos del usuario: solicitudes por aprobar, órdenes
 * asignadas y, en el portal, lo del cliente. Al tocar uno se marca como leído
 * y lleva a su pantalla.
 */
export function CampanaNotificaciones() {
  const navigate = useNavigate()
  const { esCliente, esPersonal } = useSesion()
  const [abierta, setAbierta] = useState(false)
  // En el celular un panel flotante junto al botón no cabe: se abre a pantalla completa.
  const pantallas = Grid.useBreakpoint()
  const enCelular = pantallas.md === false
  const conteo = useConteoNotificaciones()
  const lista = useNotificaciones(abierta)
  const marcar = useMarcarNotificacionLeida()
  const marcarTodas = useMarcarTodasLeidas()

  const noLeidas = conteo.data?.noLeidas ?? 0
  const avisos = lista.data ?? []

  const abrirAviso = (aviso: NotificacionResponse) => {
    if (!aviso.fechaLeida) marcar.mutate(aviso.id)
    setAbierta(false)
    navigate(rutaDeNotificacion(aviso, esCliente && !esPersonal))
  }

  const contenido = (
    <div className="notificaciones">
      <div className="notificaciones-cabecera">
        {/* El panel del celular ya trae el título. */}
        {!enCelular && <strong>Notificaciones</strong>}
        {noLeidas > 0 && (
          <Button type="link" size="small" loading={marcarTodas.isPending} onClick={() => marcarTodas.mutate()}>
            Marcar todas como leídas
          </Button>
        )}
      </div>
      <AvisoError error={lista.error ?? marcarTodas.error} />
      {lista.isPending ? (
        <p className="texto-secundario">Cargando…</p>
      ) : avisos.length === 0 ? (
        <p className="texto-secundario">No tienes notificaciones.</p>
      ) : (
        <ul className="notificaciones-lista">
          {avisos.map((aviso) => (
            <li key={aviso.id}>
              <button
                type="button"
                className={aviso.fechaLeida ? 'notificacion' : 'notificacion no-leida'}
                onClick={() => abrirAviso(aviso)}
              >
                <span className="notificacion-titulo">{aviso.titulo}</span>
                <span>{aviso.mensaje}</span>
                <span className="texto-secundario">{fechaHora(aviso.fechaCreacion)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )

  const boton = (
    <Button
      type="text"
      className="campana"
      aria-label={noLeidas > 0 ? `Notificaciones, ${noLeidas} sin leer` : 'Notificaciones'}
      onClick={enCelular ? () => setAbierta(true) : undefined}
      icon={
        <Badge count={noLeidas} size="small" overflowCount={99}>
          <BellOutlined className="campana-icono" />
        </Badge>
      }
    />
  )

  if (enCelular) {
    return (
      <>
        {boton}
        <Drawer
          title="Notificaciones"
          placement="right"
          size="100%"
          open={abierta}
          onClose={() => setAbierta(false)}
          destroyOnHidden
        >
          {contenido}
        </Drawer>
      </>
    )
  }

  return (
    <Popover
      content={contenido}
      trigger="click"
      placement="bottomLeft"
      open={abierta}
      onOpenChange={setAbierta}
      arrow={false}
    >
      {boton}
    </Popover>
  )
}
