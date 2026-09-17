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
  type TableProps,
} from 'antd'
import { Link, useParams } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EstadoOrdenApiTag } from '../components/EstadoOrdenApiTag'
import { Indicadores } from '../components/Indicadores'
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
import type { DetalleServicioResponse } from '../api/tipos'
import { entero, fechaHora, importe, referenciaOrden, soles } from '../utils/formato'

type CamposManoObra = {
  descripcion: string
  cantidad: number
  precioUnitario: number
}

export function OrdenDetallePage() {
  const { id } = useParams()
  const orden = useOrden(id)
  const usuarios = useUsuarios()

  const guardarDiagnostico = useRegistrarDiagnostico()
  const agregarDetalle = useAgregarDetalle()
  const eliminarDetalle = useEliminarDetalle()
  const cambiarEstado = useCambiarEstado()

  const [formularioMano] = Form.useForm<CamposManoObra>()
  const [textoDiagnostico, setTextoDiagnostico] = useState<string | null>(null)
  const [tecnico, setTecnico] = useState<string | null>(null)
  const [estadoDestino, setEstadoDestino] = useState<number | null>(null)
  const [observacionesCambio, setObservacionesCambio] = useState('')

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

            {puedeEditar && (
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
                <p className="texto-secundario" style={{ marginTop: 16 }}>
                  Para agregar repuestos falta la API de inventario, que sale el martes 22/09. La orden descuenta stock
                  sola cuando el repuesto se asigna, así que no conviene inventar el catálogo desde la web.
                </p>
              </>
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
          </aside>
        </div>
      </div>

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
