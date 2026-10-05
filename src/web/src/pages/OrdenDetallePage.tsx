import { useState } from 'react'
import { Button, Input, Modal, Popconfirm, Table, Tag, Timeline, Tooltip, type TableProps } from 'antd'
import { Link, useParams } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { Indicadores } from '../components/Indicadores'
import { ModalRepuestoOrden } from '../components/ModalRepuestoOrden'
import { ModalItemOrden } from '../components/ModalItemOrden'
import { ModalEditarOrden } from '../components/ModalEditarOrden'
import { ModalAsignarTecnico } from '../components/ModalAsignarTecnico'
import { PanelAprobaciones } from '../components/PanelAprobaciones'
import { ModalFotosOrden } from '../components/ModalFotosOrden'
import { ModalFormatoAtencion } from '../components/ModalFormatoAtencion'
import { ModalYamahaMock } from '../components/ModalYamahaMock'
import { ModalRegistrarPago } from '../components/ModalRegistrarPago'
import { ModalDetalleVenta } from '../components/ModalDetalleVenta'
import { ResumenCobro } from '../components/ResumenCobro'
import { tieneSaldo, useRegistrarPagoOrden } from '../api/pagos'
import { useCrearVenta } from '../api/ventas'
import {
  ESTADO,
  MODALIDADES_ATENCION,
  TIPOS_ATENCION,
  TIPOS_FALLA,
  esEstadoTerminal,
  fechaIngresoOrden,
  estadosVedadosAlTecnico,
  etiquetaDe,
  motivoBloqueoAprobacion,
  nombresEstado,
  motivoItemsBloqueados,
  useCambiarEstado,
  useEliminarDetalle,
  useOrden,
  useRegistrarDiagnostico,
  transicionesValidas,
} from '../api/ordenes'
import {
  nombresTipoAfectacion,
  nombresTipoItem,
  type DetalleServicioResponse,
  type HistorialEstadoOrdenResponse,
} from '../api/tipos'
import { fechaHora, importe, referenciaOrden, soles } from '../utils/formato'
import { lecturaIngresoOrden } from '../utils/unidades'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'

const porFecha = (a: HistorialEstadoOrdenResponse, b: HistorialEstadoOrdenResponse) =>
  new Date(a.fechaCambio).getTime() - new Date(b.fechaCambio).getTime()

export function OrdenDetallePage() {
  const { id } = useParams()
  const sesion = useSesion()
  const { tienePermiso } = sesion
  const puedeCambiarEstado = tienePermiso(PERMISOS.ordenesCambiarEstado)
  const puedeDiagnosticar = tienePermiso(PERMISOS.ordenesDiagnostico)
  const puedeAgregarItems = tienePermiso(PERMISOS.ordenesAgregarItems)
  const puedeFijarPrecios = tienePermiso(PERMISOS.preciosModificar)
  const puedeQuitarItems = tienePermiso(PERMISOS.ordenesEditar)
  const puedeEditarOrden = tienePermiso(PERMISOS.ordenesEditar)
  const puedeAsignar = tienePermiso(PERMISOS.ordenesAsignarTecnico)
  const puedeCobrar = tienePermiso(PERMISOS.ventasCrear)
  // El técnico no ve pagos: la sección es de quien vende o consulta ventas.
  const veCobro = sesion.tieneAlgunPermiso([PERMISOS.ventasVer, PERMISOS.ventasCrear])
  // El backend trata como técnico a quien lo es sin ser también Gerencia o Recepción.
  const soloTecnico = sesion.esTecnico && !sesion.esGerencia && !sesion.esRecepcion

  const orden = useOrden(id)

  const guardarDiagnostico = useRegistrarDiagnostico()
  const eliminarDetalle = useEliminarDetalle()
  const cambiarEstado = useCambiarEstado()

  const [textoDiagnostico, setTextoDiagnostico] = useState<string | null>(null)
  const [textoSolucion, setTextoSolucion] = useState<string | null>(null)
  const [estadoDestino, setEstadoDestino] = useState<number | null>(null)
  const [observacionesCambio, setObservacionesCambio] = useState('')
  const [modalRepuesto, setModalRepuesto] = useState(false)
  const [modalItem, setModalItem] = useState(false)
  const [modalEditar, setModalEditar] = useState(false)
  const [modalTecnico, setModalTecnico] = useState(false)
  const [modalFotos, setModalFotos] = useState(false)
  const [modalFormato, setModalFormato] = useState(false)
  const [modalYamaha, setModalYamaha] = useState(false)
  const [modalAdelanto, setModalAdelanto] = useState(false)
  const [ventaAbierta, setVentaAbierta] = useState<string | null>(null)

  const registrarAdelanto = useRegistrarPagoOrden()
  const liquidar = useCrearVenta()

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
  const itemsBloqueados = motivoItemsBloqueados(datos)
  const editableItems = itemsBloqueados === null
  const diagnosticoAbierto = puedeDiagnosticar && !esEstadoTerminal(datos.estadoId)
  const destinos = puedeCambiarEstado
    ? (transicionesValidas[datos.estadoId] ?? []).filter(
        (destino) => !soloTecnico || !estadosVedadosAlTecnico.includes(destino),
      )
    : []
  const historial = [...(datos.historial ?? [])].sort(porFecha)
  const anulando = estadoDestino === ESTADO.cancelada
  const bloqueoAprobacion = motivoBloqueoAprobacion(datos)

  // Cobro: los adelantos se registran en la orden; al liquidarla nace la venta,
  // que se queda con esos adelantos y recibe el saldo y el comprobante.
  const sinVenta = !datos.ventaId
  const puedeAdelantar =
    puedeCobrar && sinVenta && datos.estadoId !== ESTADO.cancelada && tieneSaldo({ total: datos.total, saldo: datos.saldo ?? datos.total })
  const puedeLiquidar =
    puedeCobrar &&
    sinVenta &&
    (datos.estadoId === ESTADO.lista || datos.estadoId === ESTADO.entregada) &&
    datos.detalles.length > 0

  const liquidarOrden = async () => {
    const venta = await liquidar.mutateAsync({
      clienteId: datos.clienteId,
      ordenServicioId: datos.id,
      detalles: null,
      esCotizacion: false,
    })
    setVentaAbierta(venta.id)
  }

  // Pocas columnas para que la tabla quepa junto a la columna lateral en una
  // laptop de 1366 px: tipo, código y afectación van bajo el concepto, y el
  // subtotal está en el bloque de totales.
  const columnas: TableProps<DetalleServicioResponse>['columns'] = [
    {
      title: 'Concepto',
      dataIndex: 'descripcion',
      render: (descripcion: string, detalle) => {
        const tipo = nombresTipoItem[detalle.tipoItem ?? (detalle.esRepuesto ? 0 : 2)] ?? detalle.tipoItemNombre
        const afectacion = detalle.tipoAfectacionIgv ?? 0
        const datos = [
          tipo,
          detalle.productoCodigo,
          // Gravado es lo normal: solo se avisa cuando no lo es.
          afectacion === 0 ? null : (nombresTipoAfectacion[afectacion] ?? detalle.tipoAfectacionIgvNombre),
        ].filter(Boolean)
        return (
          <>
            <div>{descripcion}</div>
            <div className="texto-secundario">{datos.join(' · ')}</div>
          </>
        )
      },
    },
    { title: 'Cant.', dataIndex: 'cantidad', align: 'right', className: 'num' },
    {
      title: 'P. unit.',
      dataIndex: 'precioUnitario',
      align: 'right',
      className: 'num',
      render: (precio: number) => importe(precio),
    },
    {
      title: 'IGV',
      dataIndex: 'montoIgv',
      align: 'right',
      className: 'num',
      render: (igv: number | undefined) => importe(igv ?? 0),
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
        editableItems && puedeQuitarItems ? (
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

  const diagnosticoActual = (textoDiagnostico ?? datos.diagnostico ?? '').trim()

  const guardarDiagnosticoActual = async () => {
    const solucion = (textoSolucion ?? datos.solucion ?? '').trim()
    await guardarDiagnostico.mutateAsync({
      id: datos.id,
      datos: {
        diagnostico: diagnosticoActual,
        tecnicoAsignadoId: datos.tecnicoAsignadoId,
        observaciones: null,
        solucion: solucion || null,
      },
    })
    setTextoDiagnostico(null)
    setTextoSolucion(null)
  }

  const cerrarCambioDeEstado = () => {
    setEstadoDestino(null)
    setObservacionesCambio('')
    cambiarEstado.reset()
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

    cerrarCambioDeEstado()
  }

  return (
    <>
      <header className="barra-superior detalle">
        <div>
          <div className="etiqueta">{referenciaOrden(datos)}</div>
          <h1 className="titulo-orden">
            {datos.vehiculoMarca} {datos.vehiculoModelo} · {datos.vehiculoPlaca ?? datos.numeroSerieVIN ?? 'sin placa'}
          </h1>
          <div className="etiquetas-orden">
            <EstadoOrdenApiTag estadoId={datos.estadoId} />
            <Tag style={{ marginInlineEnd: 0 }}>{datos.tecnicoNombre ?? 'Sin técnico'}</Tag>
            <Tag style={{ marginInlineEnd: 0 }}>{etiquetaDe(TIPOS_ATENCION, datos.tipoAtencionId)}</Tag>
          </div>
        </div>
        <div className="acciones">
          {puedeAsignar && !esEstadoTerminal(datos.estadoId) && (
            <Button onClick={() => setModalTecnico(true)}>
              {datos.tecnicoAsignadoId ? 'Cambiar técnico' : 'Asignar técnico'}
            </Button>
          )}
          {puedeEditarOrden && !esEstadoTerminal(datos.estadoId) && (
            <Button onClick={() => setModalEditar(true)}>Editar datos</Button>
          )}
          <Button onClick={() => setModalFotos(true)}>Fotos</Button>
          <Button onClick={() => setModalFormato(true)}>Formato de atención</Button>
          <Button onClick={() => setModalYamaha(true)}>Catálogo Yamaha</Button>
          {destinos.map((destino) => {
            const bloqueado = destino === ESTADO.aprobada ? bloqueoAprobacion : null
            return (
              <Tooltip key={destino} title={bloqueado}>
                <Button
                  danger={destino === ESTADO.cancelada}
                  type={destino === ESTADO.cancelada ? 'default' : 'primary'}
                  disabled={Boolean(bloqueado)}
                  onClick={() => setEstadoDestino(destino)}
                >
                  {destino === ESTADO.cancelada ? 'Anular' : `Pasar a ${nombresEstado[destino]}`}
                </Button>
              </Tooltip>
            )
          })}
        </div>
      </header>

      <div className="pagina">
        <AvisoError error={eliminarDetalle.error ?? guardarDiagnostico.error ?? liquidar.error} />

        <Indicadores
          tamano="mediano"
          items={[
            { etiqueta: 'Cliente', valor: datos.clienteNombre },
            { etiqueta: 'Ingreso', valor: fechaHora(fechaIngresoOrden(datos)) },
            { etiqueta: 'Entrega estimada', valor: fechaHora(datos.fechaEstimadaEntrega) },
            { etiqueta: 'Medidor al ingresar', valor: lecturaIngresoOrden(datos) },
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
                <div className="etiqueta">Subtotal gravado</div>
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
                <div className="etiqueta">IGV</div>
                <div className="valor">{soles(datos.montoIgv ?? 0)}</div>
              </div>
              <div>
                <div className="etiqueta">Total</div>
                <div className="valor total">{soles(datos.total)}</div>
              </div>
            </div>

            {editableItems && puedeAgregarItems && (
              <>
                <div style={{ marginTop: 16, display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                  <Button type="primary" onClick={() => setModalItem(true)}>
                    Agregar ítem (repuesto o servicio)
                  </Button>
                  <Button onClick={() => setModalRepuesto(true)}>Agregar repuesto rápido</Button>
                </div>
                {!puedeFijarPrecios && (
                  <p className="texto-secundario" style={{ marginTop: 8 }}>
                    Los precios salen del catálogo; tu usuario no puede cambiarlos.
                  </p>
                )}
              </>
            )}
            {itemsBloqueados && (
              <p className="texto-secundario" style={{ marginTop: 16 }}>
                {itemsBloqueados}
              </p>
            )}

            {veCobro && (
              <>
                <div className="seccion-titulo" style={{ marginTop: 36 }}>
                  <h2>Cobro</h2>
                  <div className="acciones">
                    {puedeAdelantar && <Button onClick={() => setModalAdelanto(true)}>Registrar adelanto</Button>}
                    {puedeLiquidar && (
                      <Popconfirm
                        title="Liquidar la orden"
                        description="Se genera la venta con los ítems y los adelantos de la orden. Los repuestos no se vuelven a descontar del stock."
                        okText="Liquidar"
                        cancelText="Cancelar"
                        onConfirm={liquidarOrden}
                      >
                        <Button type="primary" loading={liquidar.isPending}>
                          Liquidar y generar comprobante
                        </Button>
                      </Popconfirm>
                    )}
                    {datos.ventaId && (
                      <Button onClick={() => setVentaAbierta(datos.ventaId ?? null)}>Ver venta y comprobante</Button>
                    )}
                  </div>
                </div>
                <ResumenCobro
                  total={datos.total}
                  totalPagado={datos.totalPagado ?? 0}
                  saldo={datos.saldo ?? datos.total}
                  estadoPago={datos.estadoPago}
                  pagos={datos.pagos ?? []}
                />
                {datos.comprobanteSerieNumero && (
                  <p style={{ marginTop: 12 }}>Comprobante: {datos.comprobanteSerieNumero}</p>
                )}
                {!datos.ventaId && puedeCobrar && !puedeLiquidar && datos.estadoId !== ESTADO.cancelada && (
                  <p className="texto-secundario" style={{ marginTop: 12 }}>
                    La orden se liquida cuando está «Lista» o «Entregada». Mientras tanto se pueden registrar adelantos.
                  </p>
                )}
              </>
            )}
          </section>

          <aside className="columna">
            <PanelAprobaciones orden={datos} />

            <section>
              <div className="seccion-titulo">
                <h2>Diagnóstico</h2>
              </div>
              <Input.TextArea
                id="diagnostico"
                rows={4}
                value={textoDiagnostico ?? datos.diagnostico ?? ''}
                onChange={(evento) => setTextoDiagnostico(evento.target.value)}
                placeholder="Qué encontró el técnico"
                disabled={!diagnosticoAbierto}
              />
              <Input.TextArea
                id="solucion"
                rows={3}
                style={{ marginTop: 12 }}
                value={textoSolucion ?? datos.solucion ?? ''}
                onChange={(evento) => setTextoSolucion(evento.target.value)}
                placeholder="Solución propuesta o aplicada"
                disabled={!diagnosticoAbierto}
              />
              {diagnosticoAbierto && (
                <Button
                  type="primary"
                  style={{ marginTop: 12 }}
                  loading={guardarDiagnostico.isPending}
                  disabled={diagnosticoActual.length === 0}
                  onClick={guardarDiagnosticoActual}
                >
                  Guardar diagnóstico
                </Button>
              )}
              {diagnosticoAbierto && datos.estadoId === ESTADO.abierta && (
                <p className="texto-secundario" style={{ marginTop: 8 }}>
                  Al guardar el diagnóstico la orden pasa sola a «Diagnóstico».
                </p>
              )}
            </section>

            <section>
              <div className="seccion-titulo">
                <h2>Recepción</h2>
              </div>
              <table className="tabla-simple">
                <tbody>
                  <tr>
                    <td>Falla reportada</td>
                    <td>{datos.motivoFalla || '—'}</td>
                  </tr>
                  <tr>
                    <td>Tipo de falla</td>
                    <td>{etiquetaDe(TIPOS_FALLA, datos.tipoFallaId)}</td>
                  </tr>
                  <tr>
                    <td>Modalidad</td>
                    <td>{etiquetaDe(MODALIDADES_ATENCION, datos.modalidadAtencionId)}</td>
                  </tr>
                  <tr>
                    <td>Observaciones</td>
                    <td>{datos.observaciones || '—'}</td>
                  </tr>
                </tbody>
              </table>
            </section>

            <section>
              <div className="seccion-titulo">
                <h2>Historial</h2>
              </div>
              {historial.length === 0 ? (
                <p className="texto-secundario">Sin cambios de estado registrados.</p>
              ) : (
                <Timeline
                  items={historial.map((cambio) => {
                    // Las aprobaciones y la asignación de técnico se anotan sin cambiar el estado.
                    const cambiaEstado = cambio.estadoAnteriorId !== cambio.estadoNuevoId
                    return {
                      key: cambio.id,
                      color: cambiaEstado ? undefined : 'gray',
                      content: (
                        <div>
                          <strong>
                            {cambiaEstado
                              ? (nombresEstado[cambio.estadoNuevoId] ?? cambio.estadoNuevo)
                              : (cambio.observaciones ?? 'Actualización')}
                          </strong>
                          <div className="texto-secundario">
                            {fechaHora(cambio.fechaCambio)} · {cambio.usuarioNombre ?? 'Sistema'}
                          </div>
                          {cambiaEstado && cambio.observaciones && <div>{cambio.observaciones}</div>}
                        </div>
                      ),
                    }
                  })}
                />
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
                    <td>{datos.vehiculoPlaca ?? '—'}</td>
                  </tr>
                  <tr>
                    <td>VIN o serie</td>
                    <td>{datos.numeroSerieVIN ?? '—'}</td>
                  </tr>
                  <tr>
                    <td>Motor</td>
                    <td>{datos.numeroMotor ?? '—'}</td>
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
                      {tienePermiso(PERMISOS.clientesVer) ? (
                        <Link to={`/clientes/${datos.clienteId}`}>{datos.clienteNombre}</Link>
                      ) : (
                        datos.clienteNombre
                      )}
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
                  <tr>
                    <td>Salida</td>
                    <td>{fechaHora(datos.fechaSalida ?? datos.fechaCierre)}</td>
                  </tr>
                </tbody>
              </table>
            </section>
          </aside>
        </div>
      </div>

      <ModalItemOrden
        abierto={modalItem}
        ordenId={datos.id}
        puedeModificarPrecios={puedeFijarPrecios}
        onCerrar={() => setModalItem(false)}
      />

      <ModalRepuestoOrden
        abierto={modalRepuesto}
        ordenId={datos.id}
        onCerrar={() => setModalRepuesto(false)}
      />

      <ModalEditarOrden abierto={modalEditar} orden={datos} onCerrar={() => setModalEditar(false)} />

      <ModalAsignarTecnico
        abierto={modalTecnico}
        ordenId={datos.id}
        tecnicoActualId={datos.tecnicoAsignadoId}
        onCerrar={() => setModalTecnico(false)}
      />

      <Modal
        title={anulando ? 'Anular la orden' : `Pasar a ${estadoDestino === null ? '' : nombresEstado[estadoDestino]}`}
        open={estadoDestino !== null}
        onCancel={cerrarCambioDeEstado}
        onOk={confirmarCambioDeEstado}
        okText="Confirmar"
        cancelText="Cancelar"
        okButtonProps={{ danger: anulando, disabled: anulando && !observacionesCambio.trim() }}
        confirmLoading={cambiarEstado.isPending}
        destroyOnHidden
      >
        <AvisoError error={cambiarEstado.error} />
        {anulando && <p>Los repuestos asignados vuelven al stock. La orden no se puede reabrir.</p>}
        <Input.TextArea
          rows={3}
          value={observacionesCambio}
          onChange={(evento) => setObservacionesCambio(evento.target.value)}
          placeholder={anulando ? 'Motivo de la anulación (obligatorio)' : 'Observación (opcional)'}
        />
        <p className="texto-secundario" style={{ marginTop: 8 }}>
          Queda en el historial de la orden con tu usuario y la hora.
        </p>
      </Modal>

      <ModalRegistrarPago
        abierto={modalAdelanto}
        titulo="Registrar adelanto"
        saldo={datos.saldo ?? datos.total}
        esAnticipo
        guardando={registrarAdelanto.isPending}
        error={registrarAdelanto.error}
        onRegistrar={(pago) => registrarAdelanto.mutateAsync({ ordenId: datos.id, datos: pago })}
        onCerrar={() => {
          registrarAdelanto.reset()
          setModalAdelanto(false)
        }}
      />

      <ModalDetalleVenta abierto={ventaAbierta !== null} ventaId={ventaAbierta} onCerrar={() => setVentaAbierta(null)} />

      <ModalFotosOrden
        abierto={modalFotos}
        ordenServicioId={datos.id}
        numeroOrden={datos.numeroOrden}
        onCerrar={() => setModalFotos(false)}
      />

      <ModalFormatoAtencion
        abierto={modalFormato}
        ordenServicioId={datos.id}
        onCerrar={() => setModalFormato(false)}
      />

      <ModalYamahaMock
        abierto={modalYamaha}
        criterioInicial={datos.vehiculoModelo || datos.numeroSerieVIN || 'MT-03'}
        onCerrar={() => setModalYamaha(false)}
      />
    </>
  )
}
