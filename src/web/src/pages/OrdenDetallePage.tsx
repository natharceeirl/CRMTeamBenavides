import { useState } from 'react'
import {
  Button,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Table,
  Tag,
  Timeline,
  type TableProps,
} from 'antd'
import { Link, useParams } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { Indicadores } from '../components/Indicadores'
import { ModalRepuestoOrden } from '../components/ModalRepuestoOrden'
import { ModalEditarOrden } from '../components/ModalEditarOrden'
import {
  ESTADO,
  MODALIDADES_ATENCION,
  TIPOS_ATENCION,
  TIPOS_FALLA,
  esEstadoTerminal,
  estadosVedadosAlTecnico,
  etiquetaDe,
  nombresEstado,
  permiteEditarDetalles,
  useAgregarDetalle,
  useCambiarEstado,
  useEliminarDetalle,
  useOrden,
  useRegistrarDiagnostico,
  transicionesValidas,
} from '../api/ordenes'
import { useTecnicos } from '../api/usuarios'
import type { DetalleServicioResponse, HistorialEstadoOrdenResponse } from '../api/tipos'
import { fechaHora, importe, referenciaOrden, soles } from '../utils/formato'
import { lecturaIngresoOrden } from '../utils/unidades'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'

type CamposManoObra = {
  descripcion: string
  cantidad: number
  precioUnitario: number
}

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
  // El backend trata como técnico a quien lo es sin ser también Gerencia o Recepción.
  const soloTecnico = sesion.esTecnico && !sesion.esGerencia && !sesion.esRecepcion

  const orden = useOrden(id)
  const tecnicos = useTecnicos(puedeAsignar && tienePermiso(PERMISOS.usuariosVer))

  const guardarDiagnostico = useRegistrarDiagnostico()
  const agregarDetalle = useAgregarDetalle()
  const eliminarDetalle = useEliminarDetalle()
  const cambiarEstado = useCambiarEstado()

  const [formularioMano] = Form.useForm<CamposManoObra>()
  const [textoDiagnostico, setTextoDiagnostico] = useState<string | null>(null)
  const [textoSolucion, setTextoSolucion] = useState<string | null>(null)
  const [tecnico, setTecnico] = useState<string | null>(null)
  const [estadoDestino, setEstadoDestino] = useState<number | null>(null)
  const [observacionesCambio, setObservacionesCambio] = useState('')
  const [modalRepuesto, setModalRepuesto] = useState(false)
  const [modalEditar, setModalEditar] = useState(false)

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
  const editableItems = permiteEditarDetalles(datos.estadoId)
  const diagnosticoAbierto = puedeDiagnosticar && !esEstadoTerminal(datos.estadoId)
  const destinos = puedeCambiarEstado
    ? (transicionesValidas[datos.estadoId] ?? []).filter(
        (destino) => !soloTecnico || !estadosVedadosAlTecnico.includes(destino),
      )
    : []
  const historial = [...(datos.historial ?? [])].sort(porFecha)
  const anulando = estadoDestino === ESTADO.cancelada

  const repuestos = datos.detalles
    .filter((detalle) => detalle.esRepuesto)
    .reduce((suma, detalle) => suma + detalle.subtotal, 0)
  const manoDeObra = datos.total - repuestos

  const columnas: TableProps<DetalleServicioResponse>['columns'] = [
    { title: 'Concepto', dataIndex: 'descripcion' },
    {
      title: 'Tipo',
      key: 'tipo',
      render: (_, detalle) => (detalle.esRepuesto ? 'Repuesto' : 'Mano de obra'),
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
      title: 'Importe',
      dataIndex: 'subtotal',
      align: 'right',
      className: 'num',
      render: (subtotal: number) => importe(subtotal),
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

  const registrarManoDeObra = async (campos: CamposManoObra) => {
    await agregarDetalle.mutateAsync({
      id: datos.id,
      datos: {
        productoId: null,
        descripcion: campos.descripcion.trim(),
        cantidad: campos.cantidad,
        precioUnitario: campos.precioUnitario,
      },
    })
    formularioMano.resetFields()
  }

  const diagnosticoActual = (textoDiagnostico ?? datos.diagnostico ?? '').trim()

  const guardarDiagnosticoActual = async () => {
    const solucion = (textoSolucion ?? datos.solucion ?? '').trim()
    await guardarDiagnostico.mutateAsync({
      id: datos.id,
      datos: {
        diagnostico: diagnosticoActual,
        tecnicoAsignadoId: tecnico ?? datos.tecnicoAsignadoId,
        observaciones: null,
        solucion: solucion || null,
      },
    })
    setTextoDiagnostico(null)
    setTextoSolucion(null)
    setTecnico(null)
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
          {puedeEditarOrden && !esEstadoTerminal(datos.estadoId) && (
            <Button onClick={() => setModalEditar(true)}>Editar datos</Button>
          )}
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
        <AvisoError error={agregarDetalle.error ?? eliminarDetalle.error ?? guardarDiagnostico.error} />

        <Indicadores
          tamano="mediano"
          items={[
            { etiqueta: 'Cliente', valor: datos.clienteNombre },
            { etiqueta: 'Ingreso', valor: fechaHora(datos.fechaIngreso ?? datos.fechaApertura) },
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
                <div className="etiqueta">Mano de obra</div>
                <div className="valor">{importe(manoDeObra)}</div>
              </div>
              <div>
                <div className="etiqueta">Repuestos</div>
                <div className="valor">{importe(repuestos)}</div>
              </div>
              <div>
                <div className="etiqueta">Total</div>
                <div className="valor total">{soles(datos.total)}</div>
              </div>
            </div>

            {editableItems && puedeAgregarItems && (
              <>
                {puedeFijarPrecios && (
                  <>
                    <div className="seccion-titulo" style={{ marginTop: 24 }}>
                      <h2>Agregar mano de obra</h2>
                    </div>
                    <Form<CamposManoObra>
                      form={formularioMano}
                      layout="vertical"
                      requiredMark={false}
                      onFinish={registrarManoDeObra}
                      initialValues={{ cantidad: 1 }}
                    >
                      <div className="formulario-grid">
                        <Form.Item
                          label="Concepto"
                          name="descripcion"
                          className="ancho-completo"
                          rules={[{ required: true, message: 'Describe el trabajo' }]}
                        >
                          <Input placeholder="Mantenimiento de 12 000 km, revisión de frenos…" />
                        </Form.Item>
                        <Form.Item
                          label="Cantidad"
                          name="cantidad"
                          rules={[{ required: true, message: 'Indica la cantidad' }]}
                        >
                          <InputNumber min={1} style={{ width: '100%' }} />
                        </Form.Item>
                        <Form.Item
                          label="Precio unitario"
                          name="precioUnitario"
                          rules={[{ required: true, message: 'Indica el precio' }]}
                        >
                          <InputNumber min={0} precision={2} style={{ width: '100%' }} />
                        </Form.Item>
                      </div>
                      <Button type="primary" htmlType="submit" loading={agregarDetalle.isPending}>
                        Agregar
                      </Button>
                    </Form>
                  </>
                )}
                <Button style={{ marginTop: 16 }} onClick={() => setModalRepuesto(true)}>
                  Agregar repuesto del inventario
                </Button>
                {!puedeFijarPrecios && (
                  <p className="texto-secundario" style={{ marginTop: 8 }}>
                    El precio de cada repuesto sale del catálogo. La mano de obra la registra Recepción.
                  </p>
                )}
              </>
            )}
            {!editableItems && (
              <p className="texto-secundario" style={{ marginTop: 16 }}>
                La orden está en «{nombresEstado[datos.estadoId]}» y ya no admite cambios en los ítems.
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
              {puedeAsignar && (
                <div style={{ marginTop: 12 }}>
                  <Select
                    allowClear
                    showSearch
                    optionFilterProp="label"
                    style={{ width: '100%' }}
                    placeholder="Técnico asignado"
                    value={tecnico ?? datos.tecnicoAsignadoId ?? undefined}
                    onChange={(valor) => setTecnico(valor ?? null)}
                    disabled={!diagnosticoAbierto}
                    loading={tecnicos.isPending}
                    options={(tecnicos.data ?? []).map((usuario) => ({
                      value: usuario.id,
                      label: usuario.nombreCompleto,
                    }))}
                  />
                </div>
              )}
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
                  items={historial.map((cambio) => ({
                    key: cambio.id,
                    children: (
                      <div>
                        <strong>{nombresEstado[cambio.estadoNuevoId] ?? cambio.estadoNuevo}</strong>
                        <div className="texto-secundario">
                          {fechaHora(cambio.fechaCambio)} · {cambio.usuarioNombre ?? 'Sistema'}
                        </div>
                        {cambio.observaciones && <div>{cambio.observaciones}</div>}
                      </div>
                    ),
                  }))}
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

      <ModalRepuestoOrden
        abierto={modalRepuesto}
        ordenId={datos.id}
        onCerrar={() => setModalRepuesto(false)}
      />

      <ModalEditarOrden abierto={modalEditar} orden={datos} onCerrar={() => setModalEditar(false)} />

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
    </>
  )
}
