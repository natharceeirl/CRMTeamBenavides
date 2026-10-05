import { useState } from 'react'
import type { Dayjs } from 'dayjs'
import { DatePicker, Select, Table, Tag, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { useAuditoria, type FiltrosAuditoria } from '../api/auditoria'
import type { EventoAuditoriaResponse } from '../api/tipos'
import { cambiosDelDetalle, camposDelDetalle, nombreAccion, nombreEntidad, nombresAccion, nombresEntidad } from '../utils/auditoria'
import { useUsuarios } from '../api/usuarios'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { fechaHora } from '../utils/formato'

const opcionesEntidad = Object.entries(nombresEntidad).map(([valor, etiqueta]) => ({ value: valor, label: etiqueta }))
const opcionesAccion = Object.entries(nombresAccion).map(([valor, etiqueta]) => ({ value: valor, label: etiqueta }))
const opcionesLimite = [50, 100, 200].map((limite) => ({ value: limite, label: `Últimos ${limite}` }))

const columnas: TableProps<EventoAuditoriaResponse>['columns'] = [
  { title: 'Fecha', dataIndex: 'fecha', className: 'num', render: (fecha: string) => fechaHora(fecha) },
  { title: 'Usuario', dataIndex: 'usuarioNombre', render: (nombre: string | null) => nombre ?? 'Sistema' },
  { title: 'Acción', dataIndex: 'accion', render: (accion: string) => <Tag style={{ marginInlineEnd: 0 }}>{nombreAccion(accion)}</Tag> },
  { title: 'Registro', dataIndex: 'entidad', render: (entidad: string) => nombreEntidad(entidad) },
  {
    title: 'Id',
    dataIndex: 'entidadId',
    className: 'num',
    render: (id: string) => <span className="texto-secundario">{id.slice(0, 8).toUpperCase()}</span>,
  },
]

function DetalleEvento({ evento }: Readonly<{ evento: EventoAuditoriaResponse }>) {
  const cambios = cambiosDelDetalle(evento.detalle)
  if (cambios) {
    return cambios.length === 0 ? (
      <span className="texto-secundario">Se guardó sin cambios.</span>
    ) : (
      <table className="tabla-simple" style={{ maxWidth: 760 }}>
        <tbody>
          {cambios.map(({ campo, antes, despues }) => (
            <tr key={campo}>
              <td>{campo}</td>
              <td className="texto-secundario">{antes}</td>
              <td>{despues}</td>
            </tr>
          ))}
        </tbody>
      </table>
    )
  }

  const detalle = camposDelDetalle(evento.detalle)
  if (detalle === null) return <span className="texto-secundario">Sin detalle.</span>
  if (typeof detalle === 'string') return <code style={{ whiteSpace: 'pre-wrap' }}>{detalle}</code>
  return (
    <table className="tabla-simple" style={{ maxWidth: 640 }}>
      <tbody>
        {detalle.map(({ campo, valor }) => (
          <tr key={campo}>
            <td>{campo}</td>
            <td>{valor}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

/** Quién hizo qué y cuándo. Solo con `auditoria.ver`. */
export function AuditoriaPage() {
  const [entidad, setEntidad] = useState<string>()
  const [accion, setAccion] = useState<string>()
  const [rango, setRango] = useState<[Dayjs | null, Dayjs | null] | null>(null)
  const [limite, setLimite] = useState(50)
  const [usuarioId, setUsuarioId] = useState<string>()
  const { tienePermiso } = useSesion()
  const usuarios = useUsuarios(tienePermiso(PERMISOS.usuariosVer))

  const filtros: FiltrosAuditoria = {
    entidad,
    accion,
    fechaDesde: rango?.[0]?.startOf('day').toISOString(),
    fechaHasta: rango?.[1]?.endOf('day').toISOString(),
    limite,
    usuarioId,
  }
  const eventos = useAuditoria(filtros)

  return (
    <>
      <BarraSuperior titulo="Auditoría" />
      <div className="pagina">
        <section>
          <AvisoError error={eventos.error} />
          <div className="filtros">
            <Select
              allowClear
              placeholder="Registro"
              value={entidad}
              onChange={setEntidad}
              options={opcionesEntidad}
              style={{ width: 220 }}
            />
            <Select
              allowClear
              placeholder="Acción"
              value={accion}
              onChange={setAccion}
              options={opcionesAccion}
              style={{ width: 180 }}
            />
            {usuarios.data && (
              <Select
                allowClear
                showSearch
                optionFilterProp="label"
                placeholder="Usuario"
                value={usuarioId}
                onChange={setUsuarioId}
                options={usuarios.data.map((usuario) => ({ value: usuario.id, label: usuario.nombreCompleto }))}
                style={{ width: 220 }}
              />
            )}
            <DatePicker.RangePicker value={rango} onChange={setRango} format="DD/MM/YYYY" allowEmpty={[true, true]} />
            <Select value={limite} onChange={setLimite} options={opcionesLimite} style={{ width: 160 }} />
          </div>
          <Table
            rowKey="id"
            columns={columnas}
            dataSource={eventos.data ?? []}
            pagination={false}
            loading={eventos.isPending}
            expandable={{ expandedRowRender: (evento) => <DetalleEvento evento={evento} /> }}
            locale={{ emptyText: 'No hay eventos con esos filtros' }}
          />
        </section>
      </div>
    </>
  )
}
