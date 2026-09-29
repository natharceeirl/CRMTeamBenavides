import { useState } from 'react'
import {
  Button,
  Input,
  Modal,
  Popconfirm,
  Select,
  Table,
  Tag,
  type TableProps,
} from 'antd'
import { Link, useParams } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { Indicadores } from '../components/Indicadores'
import { ModalRepuestoOrden } from '../components/ModalRepuestoOrden'
import { ModalItemOrden } from '../components/ModalItemOrden'
import {
  ESTADO,
  esEstadoTerminal,
  nombresEstado,
  permiteEditarDetalles,
  useAgregarDetalle,
  useCambiarEstado,
  useEliminarDetalle,
  useOrden,
  useRegistrarDiagnostico,
  transicionesValidas,
} from '../api/ordenes'
import { useUsuarios } from '../api/usuarios'
import {
  nombresTipoAfectacion,
  nombresTipoItem,
  type DetalleServicioResponse,
} from '../api/tipos'
import { entero, fechaHora, importe, referenciaOrden, soles } from '../utils/formato'

export function OrdenDetallePage() {
  const { id } = useParams()
  const orden = useOrden(id)
  const usuarios = useUsuarios()

  const guardarDiagnostico = useRegistrarDiagnostico()
  const agregarDetalle = useAgregarDetalle()
  const eliminarDetalle = useEliminarDetalle()
  const cambiarEstado = useCambiarEstado()

  const [textoDiagnostico, setTextoDiagnostico] = useState<string | null>(null)
  const [tecnico, setTecnico] = useState<string | null>(null)
  const [estadoDestino, setEstadoDestino] = useState<number | null>(null)
  const [observacionesCambio, setObservacionesCambio] = useState('')
  const [modalRepuesto, setModalRepuesto] = useState(false)
  const [modalItem, setModalItem] = useState(false)

  if (orden.isPending) {
    return (
      <>
        <BarraSuperior antetitulo="Órdenes" titulo="Cargando orden…" />
        <div className="pagina" />
      </>
    )
  }

  if (orden.isError || !orden.data) {
    return (
      <>
        <BarraSuperior antetitulo="Órdenes" titulo="Orden no encontrada" />
        <div className="pagina">
          <AvisoError error={orden.error} />
          <p>
            <Link to="/ordenes">Volver a órdenes</Link>
          </p>
        </div>
      </>
    )
  }

  const datos = orden.data
  const puedeEditar = permiteEditarDetalles(datos.estadoId)
  const puedeDiagnosticar = !esEstadoTerminal(datos.estadoId)
  const destinos = transicionesValidas[datos.estadoId] ?? []


  const columnas: TableProps<DetalleServicioResponse>['columns'] = [
    { title: 'Concepto', dataIndex: 'descripcion' },
    {
      title: 'Tipo',
      key: 'tipo',
      render: (_, detalle) => {
        const nombre =
          detalle.tipoItemNombre ??
          nombresTipoItem[detalle.tipoItem ?? (detalle.esRepuesto ? 0 : 2)] ??
          'Repuesto'
        return <Tag>{nombre}</Tag>
      },
    },
    {
      title: 'Afectación',
      key: 'afectacion',
      render: (_, detalle) => {
        const afectacion =
          detalle.tipoAfectacionIgvNombre ??
          nombresTipoAfectacion[detalle.tipoAfectacionIgv ?? 0] ??
          'Gravado'
        return (
          <Tag color={detalle.tipoAfectacionIgv === 0 ? 'blue' : 'default'}>
            {afectacion}
          </Tag>
        )
      },
    },
    { title: 'Código', dataIndex: 'productoCodigo', className: 'num', render: (codigo: string | null) => codigo ?? '—' },
    { title: 'Cant.', dataIndex: 'cantidad', align: 'right', className: 'num' },
    {
      title: 'P. unit.',
      dataIndex: 'precioUnitario',
      align: 'right',
      className: 'num',
      render: (precio: number) => importe(precio),
    },
    {
      title: 'Subtotal',
      dataIndex: 'subtotalGravado',
      align: 'right',
      className: 'num',
      render: (_, detalle) => importe(detalle.subtotalGravado ?? detalle.subtotal),
    },
    {
      title: 'IGV',
      dataIndex: 'montoIgv',
      align: 'right',
      className: 'num',
      render: (igv: number) => importe(igv ?? 0),
    },
    {
      title: 'Total',
      dataIndex: 'total',
      align: 'right',
      className: 'num',
      render: (_, detalle) => importe(detalle.total ?? detalle.subtotal),
    },
    {
      title: '',
      key: 'acciones',
      align: 'right',
      render: (_, detalle) =>
        puedeEditar ? (
          <Popconfirm
            title="Quitar el ítem"
            description={detalle.esRepuesto ? 'El repuesto vuelve al stock.' : undefined}
            okText="Quitar"
            cancelText="Cancelar"
            onConfirm={() => eliminarDetalle.mutate({ id: datos.id, detalleId: detalle.id })}
          >
            <Button type="link">Quitar</Button>
          </Popconfirm>
        ) : null,
    },
  ]


  const guardarDiagnosticoActual = async () => {
    await guardarDiagnostico.mutateAsync({
      id: datos.id,
      datos: {
        diagnostico: (textoDiagnostico ?? datos.diagnostico ?? '').trim(),
        tecnicoAsignadoId: tecnico ?? datos.tecnicoAsignadoId,
        observaciones: null,
      },
    })
    setTextoDiagnostico(null)
    setTecnico(null)
  }

  const confirmarCambioDeEstado = async () => {
    if (estadoDestino === null) {
      return
    }

    await cambiarEstado.mutateAsync({
      id: datos.id,
      datos: {
        nuevoEstado: estadoDestino,
        observaciones: observacionesCambio.trim() ? observacionesCambio.trim() : null,
      },
    })

    setEstadoDestino(null)
    setObservacionesCambio('')
  }

  return (
    <>
      <header className="barra-superior detalle">
        <div>
          <div className="etiqueta">{referenciaOrden(datos.id)}</div>
          <h1 className="titulo-orden">
            {datos.vehiculoMarca} {datos.vehiculoModelo} · {datos.vehiculoPlaca}
          </h1>
          <div className="etiquetas-orden">
            <EstadoOrdenApiTag estadoId={datos.estadoId} />
            <Tag style={{ marginInlineEnd: 0 }}>{datos.tecnicoNombre ?? 'Sin técnico'}</Tag>
          </div>
        </div>
        <div className="acciones">
          {destinos.map((destino) => (
            <Button
              key={destino}
              danger={destino === ESTADO.cancelada}
              type={destino === ESTADO.cancelada ? 'default' : 'primary'}
              onClick={() => setEstadoDestino(destino)}
            >
              {destino === ESTADO.cancelada ? 'Anular' : `Pasar a ${nombresEstado[destino]}`}
            </Button>
          ))}
        </div>
      </header>

      <div className="pagina">
        <AvisoError
          error={
            cambiarEstado.error ??
            agregarDetalle.error ??
            eliminarDetalle.error ??
            guardarDiagnostico.error
          }
        />

        <Indicadores
          tamano="mediano"
          items={[
            { etiqueta: 'Cliente', valor: datos.clienteNombre },
            { etiqueta: 'Ingreso', valor: fechaHora(datos.fechaApertura) },
            {
              etiqueta: 'Kilometraje',
              valor: datos.vehiculoKilometraje === null ? '—' : `${entero(datos.vehiculoKilometraje)} km`,
            },
            { etiqueta: 'Total', valor: datos.total > 0 ? soles(datos.total) : '—' },
          ]}
        />

        <div className="dos-columnas">
          <section>
            <div className="seccion-titulo">
              <h2>Trabajos y repuestos</h2>
            </div>
            <Table
              rowKey="id"
              columns={columnas}
              dataSource={datos.detalles}
              pagination={false}
              locale={{ emptyText: 'Aún no hay trabajos ni repuestos registrados' }}
            />
            <div className="totales">
              <div>
                <div className="etiqueta">Subtotal Gravado</div>
                <div className="valor">{soles(datos.subtotalGravado ?? 0)}</div>
              </div>
              {(datos.subtotalExonerado ?? 0) > 0 && (
                <div>
                  <div className="etiqueta">Exonerado</div>
                  <div className="valor">{soles(datos.subtotalExonerado ?? 0)}</div>
                </div>
              )}
              {(datos.subtotalInafecto ?? 0) > 0 && (
                <div>
                  <div className="etiqueta">Inafecto</div>
                  <div className="valor">{soles(datos.subtotalInafecto ?? 0)}</div>
                </div>
              )}
              <div>
                <div className="etiqueta">IGV (18%)</div>
                <div className="valor">{soles(datos.montoIgv ?? 0)}</div>
              </div>
              <div>
                <div className="etiqueta">Total</div>
                <div className="valor total">{soles(datos.total)}</div>
              </div>
            </div>

            {puedeEditar && (
              <div style={{ marginTop: 16, display: 'flex', gap: 12 }}>
                <Button type="primary" onClick={() => setModalItem(true)}>
                  Agregar ítem (Repuesto / Servicio)
                </Button>
                <Button onClick={() => setModalRepuesto(true)}>
                  Agregar repuesto rápido
                </Button>
              </div>
            )}
            {!puedeEditar && (
              <p className="texto-secundario" style={{ marginTop: 16 }}>
                La orden está en «{datos.estado}» y ya no admite cambios en los ítems.
              </p>
            )}
          </section>

          <aside className="columna">
            <section>
              <div className="seccion-titulo">
                <h2>Diagnóstico</h2>
              </div>
              <Input.TextArea
                id="diagnostico"
                rows={5}
                value={textoDiagnostico ?? datos.diagnostico ?? ''}
                onChange={(evento) => setTextoDiagnostico(evento.target.value)}
                placeholder="Qué encontró el técnico"
                disabled={!puedeDiagnosticar}
              />
              <div style={{ marginTop: 12 }}>
                <Select
                  allowClear
                  showSearch
                  optionFilterProp="label"
                  style={{ width: '100%' }}
                  placeholder="Técnico asignado"
                  value={tecnico ?? datos.tecnicoAsignadoId ?? undefined}
                  onChange={(valor) => setTecnico(valor ?? null)}
                  disabled={!puedeDiagnosticar}
                  options={(usuarios.data ?? []).map((usuario) => ({
                    value: usuario.id,
                    label: usuario.nombreCompleto,
                  }))}
                />
              </div>
              <Button
                type="primary"
                style={{ marginTop: 12 }}
                loading={guardarDiagnostico.isPending}
                disabled={
                  !puedeDiagnosticar || (textoDiagnostico ?? datos.diagnostico ?? '').trim().length === 0
                }
                onClick={guardarDiagnosticoActual}
              >
                Guardar diagnóstico
              </Button>
              {datos.estadoId === ESTADO.abierta && (
                <p className="texto-secundario" style={{ marginTop: 8 }}>
                  Al guardar el diagnóstico la orden pasa sola a «Diagnóstico».
                </p>
              )}
            </section>

            <section>
              <div className="seccion-titulo">
                <h2>Unidad y cliente</h2>
              </div>
              <table className="tabla-simple">
                <tbody>
                  <tr>
                    <td>Placa</td>
                    <td>{datos.vehiculoPlaca}</td>
                  </tr>
                  <tr>
                    <td>Año</td>
                    <td>{datos.vehiculoAnio ?? '—'}</td>
                  </tr>
                  <tr>
                    <td>Color</td>
                    <td>{datos.vehiculoColor ?? '—'}</td>
                  </tr>
                  <tr>
                    <td>Cliente</td>
                    <td>
                      <Link to={`/clientes/${datos.clienteId}`}>{datos.clienteNombre}</Link>
                    </td>
                  </tr>
                  <tr>
                    <td>Documento</td>
                    <td>{datos.clienteDocumentoIdentidad ?? '—'}</td>
                  </tr>
                  <tr>
                    <td>Teléfono</td>
                    <td>{datos.clienteTelefono ?? '—'}</td>
                  </tr>
                  {datos.lecturaMedidorIngreso !== null && datos.lecturaMedidorIngreso !== undefined && (
                    <tr>
                      <td>Lectura Medidor</td>
                      <td>{datos.lecturaMedidorIngreso}</td>
                    </tr>
                  )}
                  <tr>
                    <td>Cierre</td>
                    <td>{fechaHora(datos.fechaCierre)}</td>
                  </tr>
                </tbody>
              </table>
            </section>

            <section>
              <div className="seccion-titulo">
                <h2>Observaciones de recepción</h2>
              </div>
              <p className={datos.observaciones ? undefined : 'texto-secundario'}>
                {datos.observaciones ?? 'Sin observaciones.'}
              </p>
            </section>

            {datos.historial && datos.historial.length > 0 && (
              <section>
                <div className="seccion-titulo">
                  <h2>Historial de estados</h2>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8, fontSize: 13 }}>
                  {datos.historial.map((h) => (
                    <div key={h.id} style={{ borderLeft: '2px solid #1677ff', paddingLeft: 8 }}>
                      <div>
                        <strong>{h.estadoNuevo}</strong> ·{' '}
                        <span className="texto-secundario">{fechaHora(h.fechaCambio)}</span>
                        {h.usuarioNombre && <span> ({h.usuarioNombre})</span>}
                      </div>
                      {h.observaciones && <div className="texto-secundario">{h.observaciones}</div>}
                    </div>
                  ))}
                </div>
              </section>
            )}
          </aside>
        </div>
      </div>

      <ModalItemOrden
        abierto={modalItem}
        ordenId={datos.id}
        puedeModificarPrecios={true}
        onCerrar={() => setModalItem(false)}
      />

      <ModalRepuestoOrden
        abierto={modalRepuesto}
        ordenId={datos.id}
        onCerrar={() => setModalRepuesto(false)}
      />

      <Modal
        title={
          estadoDestino === ESTADO.cancelada
            ? 'Anular la orden'
            : `Pasar a ${estadoDestino === null ? '' : nombresEstado[estadoDestino]}`
        }
        open={estadoDestino !== null}
        onCancel={() => {
          setEstadoDestino(null)
          setObservacionesCambio('')
        }}
        onOk={confirmarCambioDeEstado}
        okText="Confirmar"
        cancelText="Cancelar"
        okButtonProps={{ danger: estadoDestino === ESTADO.cancelada }}
        confirmLoading={cambiarEstado.isPending}
        destroyOnHidden
      >
        {estadoDestino === ESTADO.cancelada && (
          <p>Los repuestos asignados vuelven al stock. La orden no se puede reabrir.</p>
        )}
        <Input.TextArea
          rows={3}
          value={observacionesCambio}
          onChange={(evento) => setObservacionesCambio(evento.target.value)}
          placeholder="Observaciones (opcional)"
        />
      </Modal>
    </>
  )
}
