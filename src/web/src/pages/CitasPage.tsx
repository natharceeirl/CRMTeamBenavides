import { useState } from 'react'
import dayjs, { type Dayjs } from 'dayjs'
import { Button, Select, Space, Spin } from 'antd'
import { LeftOutlined, PlusOutlined, RightOutlined } from '@ant-design/icons'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoCitaTag } from '../components/EstadoCitaTag'
import { ModalCita } from '../components/ModalCita'
import { ModalDetalleCita } from '../components/ModalDetalleCita'
import { esCitaFinal, nombresEstadoCita, rutaCitas, useCitas, type FiltrosCitas } from '../api/citas'
import type { EstadoCita } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { descargarArchivo } from '../utils/descarga'
import { citasPorDia, claveDia, diasDeLaSemana, lunesDe, nombreDia, rangoSemana, textoDuracion } from '../utils/agenda'

const opcionesEstado = Object.entries(nombresEstadoCita).map(([valor, etiqueta]) => ({
  value: valor as EstadoCita,
  label: etiqueta,
}))

export function CitasPage() {
  const [lunes, setLunes] = useState(() => lunesDe(dayjs()))
  const [estado, setEstado] = useState<EstadoCita>()
  // `undefined` cerrado; `null` abierto sin fecha; un día, abierto en ese día.
  const [nueva, setNueva] = useState<Dayjs | null | undefined>(undefined)
  const [citaVista, setCitaVista] = useState<string | null>(null)
  const [exportando, setExportando] = useState(false)
  const [errorExportar, setErrorExportar] = useState<unknown>(null)

  const puedeCrear = useSesion().tienePermiso(PERMISOS.citasCrear)

  const filtros: FiltrosCitas = {
    fechaInicio: lunes.toISOString(),
    fechaFin: lunes.add(7, 'day').subtract(1, 'millisecond').toISOString(),
    estado,
  }
  const citas = useCitas(filtros)
  const porDia = citasPorDia(citas.data ?? [])
  const hoy = dayjs()
  const esEstaSemana = lunes.isSame(lunesDe(hoy), 'day')

  const exportar = async () => {
    setExportando(true)
    setErrorExportar(null)
    try {
      await descargarArchivo(rutaCitas(filtros, '/citas/exportar-excel'), `agenda-${claveDia(lunes)}.xlsx`)
    } catch (error) {
      setErrorExportar(error)
    } finally {
      setExportando(false)
    }
  }

  return (
    <>
      <BarraSuperior
        titulo="Agenda"
        acciones={
          <>
            <Button onClick={exportar} loading={exportando}>
              Exportar a Excel
            </Button>
            {puedeCrear && (
              <Button type="primary" onClick={() => setNueva(null)}>
                Nueva cita
              </Button>
            )}
          </>
        }
      />
      <div className="pagina">
        <section>
          <AvisoError error={citas.error ?? errorExportar} />
          <div className="filtros agenda-filtros">
            <Space.Compact>
              <Button
                icon={<LeftOutlined />}
                aria-label="Semana anterior"
                onClick={() => setLunes(lunes.subtract(7, 'day'))}
              />
              <Button disabled={esEstaSemana} onClick={() => setLunes(lunesDe(dayjs()))}>
                Esta semana
              </Button>
              <Button
                icon={<RightOutlined />}
                aria-label="Semana siguiente"
                onClick={() => setLunes(lunes.add(7, 'day'))}
              />
            </Space.Compact>
            <strong className="agenda-rango sin-salto">{rangoSemana(lunes)}</strong>
            <Select<EstadoCita>
              id="filtro-estado-cita"
              allowClear
              placeholder="Estado"
              value={estado}
              onChange={setEstado}
              options={opcionesEstado}
              style={{ width: 200 }}
            />
          </div>

          <Spin spinning={citas.isPending}>
            <div className="agenda-semana">
              {diasDeLaSemana(lunes).map((dia) => {
                const clave = claveDia(dia)
                const delDia = porDia.get(clave) ?? []
                const pasado = dia.isBefore(hoy, 'day')
                return (
                  <div key={clave} className={`agenda-dia${dia.isSame(hoy, 'day') ? ' hoy' : ''}`}>
                    <div className="agenda-dia-titulo">
                      <span>
                        {nombreDia(dia)} <span className="num">{dia.format('DD/MM')}</span>
                      </span>
                      {puedeCrear && !pasado && (
                        <Button
                          type="text"
                          size="small"
                          icon={<PlusOutlined />}
                          aria-label={`Agendar el ${nombreDia(dia).toLowerCase()} ${dia.format('DD/MM')}`}
                          onClick={() => setNueva(dia.hour(9))}
                        />
                      )}
                    </div>
                    {delDia.map((cita) => (
                      <button
                        type="button"
                        key={cita.id}
                        className={`agenda-cita${esCitaFinal(cita.estado) ? ' cerrada' : ''}`}
                        onClick={() => setCitaVista(cita.id)}
                      >
                        <span className="agenda-cita-hora">
                          {dayjs(cita.fechaHoraProgramada).format('HH:mm')} · {textoDuracion(cita.duracionMinutos)}
                        </span>
                        <span className="agenda-cita-cliente">{cita.clienteNombre}</span>
                        <span className="texto-secundario">
                          {cita.vehiculoModelo} · {cita.vehiculoPlaca ?? 'sin placa'}
                        </span>
                        <EstadoCitaTag estado={cita.estado} />
                      </button>
                    ))}
                  </div>
                )
              })}
            </div>
          </Spin>
          {!citas.isPending && (citas.data ?? []).length === 0 && (
            <p className="texto-secundario" style={{ marginTop: 16 }}>
              Sin citas esta semana.
            </p>
          )}
        </section>
      </div>
      <ModalCita
        abierto={nueva !== undefined}
        fechaInicial={nueva ?? undefined}
        onCerrar={() => setNueva(undefined)}
        onCreada={(cita) => {
          // Si la cita cae en otra semana, la agenda va a esa semana para mostrarla.
          setLunes(lunesDe(dayjs(cita.fechaHoraProgramada)))
          setCitaVista(cita.id)
        }}
      />
      <ModalDetalleCita citaId={citaVista} onCerrar={() => setCitaVista(null)} />
    </>
  )
}
