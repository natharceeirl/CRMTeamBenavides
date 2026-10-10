import { useEffect, useState } from 'react'
import dayjs, { type Dayjs } from 'dayjs'
import { Button, Checkbox, Collapse, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useNavigate } from 'react-router'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { ModalProveedor } from '../components/ModalProveedor'
import { ModalAltaRapidaRepuesto } from '../components/ModalesAltaRapida'
import { nombresTipoComprobanteCompra, useProveedores, useRegistrarCompra } from '../api/compras'
import { useProductos } from '../api/inventario'
import { useConfiguracionEmpresa, useTipoCambio } from '../api/configuracion'
import { useMetodosPago } from '../api/pagos'
import { useCajaActual } from '../api/caja'
import { usePedidosLima } from '../api/pedidosLima'
import {
  ESTADO_PEDIDO_LIMA,
  MONEDA_COMPRA,
  TIPO_AFECTACION_IGV,
  TIPO_COMPROBANTE_COMPRA,
  nombresTipoAfectacion,
  type MonedaCompra,
  type ProductoResponse,
  type ProveedorResponse,
  type TipoComprobanteCompra,
} from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { calcularLinea, calcularTotales, montoEnMoneda, redondear } from '../utils/compras'
import { soles } from '../utils/formato'

type LineaForm = {
  /** Un repuesto mueve stock; un concepto libre (flete, servicio externo) no. */
  tipo: 'repuesto' | 'concepto'
  productoId?: string
  descripcion?: string
  cantidad?: number
  precioUnitario?: number
  tipoAfectacionIgv: number
}

type PagoForm = {
  monto?: number
  metodoPagoId?: string
  referencia?: string
}

type Campos = {
  proveedorId?: string
  tipoComprobante: TipoComprobanteCompra
  serie: string
  numero: string
  fechaEmision: Dayjs
  fechaVencimiento?: Dayjs | null
  moneda: MonedaCompra
  tipoCambio?: number | null
  porcentajeIgv: number
  preciosIncluyenIgv: boolean
  pedidoLimaId?: string
  guiaRemision?: string
  observaciones?: string
  detalles: LineaForm[]
  registrarPagos: boolean
  pagos: PagoForm[]
}

const lineaVacia = (tipo: LineaForm['tipo']): LineaForm => ({
  tipo,
  cantidad: 1,
  tipoAfectacionIgv: TIPO_AFECTACION_IGV.gravado,
})

const opcionesComprobante = Object.entries(nombresTipoComprobanteCompra).map(([valor, nombre]) => ({
  value: Number(valor) as TipoComprobanteCompra,
  label: nombre,
}))

const opcionesAfectacion = Object.entries(nombresTipoAfectacion).map(([valor, nombre]) => ({
  value: Number(valor),
  label: nombre,
}))

const sinVacios = (valor?: string) => (valor?.trim() ? valor.trim() : null)

const comoLinea = (linea: LineaForm | undefined) => ({
  cantidad: linea?.cantidad ?? 0,
  precioUnitario: linea?.precioUnitario ?? 0,
  tipoAfectacionIgv: linea?.tipoAfectacionIgv ?? TIPO_AFECTACION_IGV.gravado,
})

export function NuevaCompraPage() {
  const navigate = useNavigate()
  const [formulario] = Form.useForm<Campos>()
  const { tienePermiso } = useSesion()
  const puedeCrearRepuesto = tienePermiso(PERMISOS.inventarioCrear)
  const vePedidos = tienePermiso(PERMISOS.pedidosLimaVer)

  const proveedores = useProveedores()
  const productos = useProductos()
  const configuracion = useConfiguracionEmpresa()
  const tipoCambio = useTipoCambio()
  const pedidos = usePedidosLima({})
  const registrar = useRegistrarCompra()

  const [modalProveedor, setModalProveedor] = useState(false)
  const [modalRepuesto, setModalRepuesto] = useState(false)
  // Un repuesto o proveedor recién registrado puede no estar todavía en la lista que se recarga.
  const [repuestosNuevos, setRepuestosNuevos] = useState<ProductoResponse[]>([])
  const [proveedoresNuevos, setProveedoresNuevos] = useState<ProveedorResponse[]>([])

  const lineas = Form.useWatch('detalles', formulario) ?? []
  const pagos = Form.useWatch('pagos', formulario) ?? []
  const moneda = Form.useWatch('moneda', formulario) ?? MONEDA_COMPRA.pen
  const tipoCambioCompra = Form.useWatch('tipoCambio', formulario)
  const porcentajeIgv = Form.useWatch('porcentajeIgv', formulario) ?? 0
  const preciosIncluyenIgv = Form.useWatch('preciosIncluyenIgv', formulario) ?? false
  const registrarPagos = Form.useWatch('registrarPagos', formulario) ?? false
  const pedidoLimaId = Form.useWatch('pedidoLimaId', formulario)
  const tipoComprobante = Form.useWatch('tipoComprobante', formulario)
  const fechaEmision = Form.useWatch('fechaEmision', formulario)

  const metodos = useMetodosPago(registrarPagos)
  const caja = useCajaActual(registrarPagos)

  // El IGV de Configuración llega después de abrir la página.
  const igvConfiguracion = configuracion.data?.porcentajeIgv
  useEffect(() => {
    if (igvConfiguracion != null && !formulario.isFieldTouched('porcentajeIgv')) {
      formulario.setFieldsValue({ porcentajeIgv: igvConfiguracion })
    }
  }, [igvConfiguracion, formulario])

  const enDolares = moneda === MONEDA_COMPRA.usd
  const totales = calcularTotales(lineas.map(comoLinea), porcentajeIgv, preciosIncluyenIgv)
  const totalPagos = redondear(pagos.reduce((suma, pago) => suma + (pago?.monto ?? 0), 0))
  const pagaEnEfectivo = pagos.some(
    (pago) => (metodos.data ?? []).find((metodo) => metodo.id === pago?.metodoPagoId)?.codigo === 'EFECTIVO',
  )

  const catalogo = [
    ...(productos.data ?? []),
    ...repuestosNuevos.filter((nuevo) => !(productos.data ?? []).some((producto) => producto.id === nuevo.id)),
  ]
  // Se busca por código o nombre; un lector de código de barras escribe el código y Enter elige el que coincide.
  const opcionesRepuesto = catalogo.map((producto) => ({
    value: producto.id,
    label: `${producto.codigo} · ${producto.nombre}`,
  }))

  const cambiarMoneda = (nueva: MonedaCompra) => {
    if (nueva === MONEDA_COMPRA.usd && !formulario.getFieldValue('tipoCambio')) {
      formulario.setFieldsValue({ tipoCambio: tipoCambio.data?.valorVenta ?? null })
    }
    if (nueva === MONEDA_COMPRA.pen) {
      formulario.setFieldsValue({ tipoCambio: null })
    }
  }

  const activarPagos = (activar: boolean) => {
    if (activar && (formulario.getFieldValue('pagos') ?? []).length === 0) {
      formulario.setFieldsValue({ pagos: [{ monto: totales.total > 0 ? totales.total : undefined }] })
    }
  }

  const agregarRepuestoNuevo = (repuesto: ProductoResponse) => {
    setModalRepuesto(false)
    setRepuestosNuevos((anteriores) => [...anteriores, repuesto])
    const actuales: LineaForm[] = formulario.getFieldValue('detalles') ?? []
    const libre = actuales.findIndex((linea) => linea.tipo === 'repuesto' && !linea.productoId)
    const linea = { ...lineaVacia('repuesto'), productoId: repuesto.id }
    // Ocupa la primera línea de repuesto vacía antes de agregar otra.
    formulario.setFieldsValue({
      detalles: libre >= 0 ? actuales.map((actual, indice) => (indice === libre ? { ...actual, ...linea } : actual)) : [...actuales, linea],
    })
  }

  const enviar = async (campos: Campos) => {
    const compra = await registrar.mutateAsync({
      proveedorId: campos.proveedorId!,
      tipoComprobante: campos.tipoComprobante,
      serie: campos.serie.trim().toUpperCase(),
      numero: campos.numero.trim(),
      fechaEmision: campos.fechaEmision.format('YYYY-MM-DD'),
      fechaVencimiento: campos.fechaVencimiento?.format('YYYY-MM-DD') ?? null,
      moneda: campos.moneda,
      tipoCambio: campos.moneda === MONEDA_COMPRA.usd ? (campos.tipoCambio ?? null) : null,
      porcentajeIgv: campos.porcentajeIgv,
      preciosIncluyenIgv: campos.preciosIncluyenIgv,
      pedidoLimaId: campos.pedidoLimaId ?? null,
      guiaRemision: sinVacios(campos.guiaRemision),
      observaciones: sinVacios(campos.observaciones),
      detalles: campos.detalles.map((linea) => ({
        productoId: linea.tipo === 'repuesto' ? (linea.productoId ?? null) : null,
        descripcion: linea.tipo === 'concepto' ? (linea.descripcion?.trim() ?? null) : null,
        cantidad: linea.cantidad ?? 0,
        precioUnitario: linea.precioUnitario ?? 0,
        tipoAfectacionIgv: linea.tipoAfectacionIgv,
      })),
      pagos: campos.registrarPagos
        ? (campos.pagos ?? []).map((pago) => ({
            monto: pago.monto ?? 0,
            metodoPagoId: pago.metodoPagoId!,
            referencia: sinVacios(pago.referencia),
            observaciones: null,
          }))
        : [],
    })
    navigate(`/compras?ver=${compra.id}`)
  }

  return (
    <>
      <BarraSuperior
        antetitulo="Compras"
        titulo="Nueva compra"
        acciones={
          <>
            <Button onClick={() => navigate('/compras')}>Cancelar</Button>
            <Button type="primary" loading={registrar.isPending} onClick={() => formulario.submit()}>
              Registrar compra
            </Button>
          </>
        }
      />
      <div className="pagina">
        <AvisoError error={registrar.error ?? proveedores.error ?? productos.error} />
        <Form<Campos>
          form={formulario}
          layout="vertical"
          requiredMark={false}
          className="formulario"
          onFinish={enviar}
          scrollToFirstError
          initialValues={{
            tipoComprobante: TIPO_COMPROBANTE_COMPRA.factura,
            fechaEmision: dayjs(),
            moneda: MONEDA_COMPRA.pen,
            porcentajeIgv: 18,
            preciosIncluyenIgv: false,
            detalles: [lineaVacia('repuesto')],
            registrarPagos: false,
            pagos: [],
          }}
        >
          <section className="bloque">
            <h2>Comprobante</h2>
            <div className="formulario-grid">
              <Form.Item
                label="Proveedor"
                name="proveedorId"
                className="ancho-completo"
                rules={[{ required: true, message: 'Elige el proveedor' }]}
                extra={
                  <Button type="link" size="small" style={{ paddingInline: 0 }} onClick={() => setModalProveedor(true)}>
                    + Nuevo proveedor
                  </Button>
                }
              >
                <Select
                  showSearch
                  optionFilterProp="label"
                  loading={proveedores.isPending}
                  placeholder="Busca por razón social o RUC"
                  notFoundContent="No hay un proveedor con ese nombre o documento"
                  options={[
                    ...(proveedores.data ?? []),
                    ...proveedoresNuevos.filter((nuevo) => !(proveedores.data ?? []).some((p) => p.id === nuevo.id)),
                  ].map((proveedor) => ({
                    value: proveedor.id,
                    label: `${proveedor.razonSocial} · ${proveedor.numeroDocumento}`,
                  }))}
                />
              </Form.Item>
              <Form.Item
                label="Tipo de comprobante"
                name="tipoComprobante"
                extra={
                  tipoComprobante !== undefined && tipoComprobante !== TIPO_COMPROBANTE_COMPRA.factura
                    ? 'Sin factura el IGV no se recupera: el costo del repuesto lo incluye.'
                    : undefined
                }
              >
                <Select options={opcionesComprobante} />
              </Form.Item>
              <div className="formulario-grid" style={{ columnGap: 16 }}>
                <Form.Item
                  label="Serie"
                  name="serie"
                  rules={[
                    { required: true, whitespace: true, message: 'Ingresa la serie' },
                    { pattern: /^[A-Za-z0-9]{1,10}$/, message: 'Hasta 10 letras o números' },
                  ]}
                >
                  <Input placeholder="F001" maxLength={10} style={{ textTransform: 'uppercase' }} />
                </Form.Item>
                <Form.Item
                  label="Número"
                  name="numero"
                  rules={[
                    { required: true, whitespace: true, message: 'Ingresa el número' },
                    { pattern: /^[A-Za-z0-9-]{1,20}$/, message: 'Letras, números o guiones' },
                  ]}
                >
                  <Input placeholder="123" maxLength={20} />
                </Form.Item>
              </div>
              <Form.Item
                label="Fecha de emisión"
                name="fechaEmision"
                rules={[{ required: true, message: 'Indica la fecha de emisión' }]}
              >
                <DatePicker
                  format="DD/MM/YYYY"
                  disabledDate={(dia) => dia.isAfter(dayjs(), 'day')}
                  style={{ width: '100%' }}
                />
              </Form.Item>
              <Form.Item
                label="Fecha de vencimiento"
                name="fechaVencimiento"
                dependencies={['fechaEmision']}
                extra="Si se paga al crédito. Con saldo y la fecha pasada, la compra figura vencida."
                rules={[
                  ({ getFieldValue }) => ({
                    validator: async (_, vencimiento: Dayjs | null | undefined) => {
                      const emision: Dayjs | undefined = getFieldValue('fechaEmision')
                      if (vencimiento && emision && vencimiento.isBefore(emision, 'day')) {
                        throw new Error('No puede ser anterior a la emisión')
                      }
                    },
                  }),
                ]}
              >
                <DatePicker
                  format="DD/MM/YYYY"
                  disabledDate={(dia) => (fechaEmision ? dia.isBefore(fechaEmision, 'day') : false)}
                  style={{ width: '100%' }}
                />
              </Form.Item>
              <Form.Item label="Moneda" name="moneda">
                <Select
                  onChange={cambiarMoneda}
                  options={[
                    { value: MONEDA_COMPRA.pen, label: 'Soles (S/)' },
                    { value: MONEDA_COMPRA.usd, label: 'Dólares (US$)' },
                  ]}
                />
              </Form.Item>
              {enDolares ? (
                <Form.Item
                  label="Tipo de cambio (S/ por US$)"
                  name="tipoCambio"
                  extra="Se propone el vigente; corrígelo con el de la factura si es otro."
                  rules={[{ required: true, message: 'Ingresa el tipo de cambio' }]}
                >
                  <InputNumber min={0.0001} max={99} precision={4} style={{ width: '100%' }} />
                </Form.Item>
              ) : (
                <div />
              )}
              <Form.Item label="IGV (%)" name="porcentajeIgv" rules={[{ required: true, message: 'Indica el IGV' }]}>
                <InputNumber min={0} max={100} precision={2} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item
                label="Precios"
                name="preciosIncluyenIgv"
                valuePropName="checked"
                extra="Márcalo si el precio unitario del comprobante ya trae el IGV."
              >
                <Checkbox>Los precios incluyen IGV</Checkbox>
              </Form.Item>
            </div>
            <Collapse
              ghost
              items={[
                {
                  key: 'adicional',
                  label: 'Información adicional',
                  forceRender: true,
                  children: (
                    <div className="formulario-grid">
                      <Form.Item label="Guía de remisión" name="guiaRemision">
                        <Input maxLength={50} placeholder="T001-123" />
                      </Form.Item>
                      {vePedidos && (
                        <Form.Item
                          label="Pedido a Lima"
                          name="pedidoLimaId"
                          extra={
                            pedidoLimaId
                              ? 'Esta compra no suma stock: los repuestos entran cuando el pedido se marca «Recibido».'
                              : 'Si la compra es de un pedido a Lima de un cliente.'
                          }
                        >
                          <Select
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            loading={pedidos.isPending}
                            placeholder="Ninguno"
                            options={(pedidos.data ?? [])
                              .filter((pedido) => pedido.estado !== ESTADO_PEDIDO_LIMA.cancelado)
                              .map((pedido) => ({
                                value: pedido.id,
                                label: `${pedido.numeroPedido} · ${pedido.clienteNombre}`,
                              }))}
                          />
                        </Form.Item>
                      )}
                      <Form.Item label="Observaciones" name="observaciones" className="ancho-completo">
                        <Input.TextArea rows={2} maxLength={500} />
                      </Form.Item>
                    </div>
                  ),
                },
              ]}
            />
          </section>

          <section className="bloque">
            <h2>Repuestos y conceptos</h2>
            {pedidoLimaId && (
              <p className="texto-secundario" style={{ marginBottom: 12 }}>
                Compra de un pedido a Lima: no suma stock ni cambia el costo.
              </p>
            )}
            <Form.List
              name="detalles"
              rules={[
                {
                  validator: async (_, detalles: LineaForm[] | undefined) => {
                    if (!detalles?.length) throw new Error('Agrega al menos un repuesto o concepto')
                  },
                },
              ]}
            >
              {(campos, { add, remove }, { errors }) => (
                <>
                  <div className="linea-compra linea-compra-cabecera" aria-hidden="true">
                    <span>Repuesto o concepto</span>
                    <span>IGV</span>
                    <span>Cant.</span>
                    <span>{enDolares ? 'P. unit. US$' : 'P. unit. S/'}</span>
                    <span className="num">Total</span>
                    <span />
                  </div>
                  {campos.map((campo) => {
                    const linea: LineaForm | undefined = lineas[campo.name]
                    const montos = calcularLinea(comoLinea(linea), porcentajeIgv, preciosIncluyenIgv)
                    return (
                      <div key={campo.key} className="linea-compra">
                        {linea?.tipo === 'concepto' ? (
                          <Form.Item
                            label="Concepto"
                            className="lc-item"
                            name={[campo.name, 'descripcion']}
                            rules={[{ required: true, whitespace: true, message: 'Escribe el concepto' }]}
                          >
                            <Input placeholder="Flete, servicio externo…" maxLength={250} />
                          </Form.Item>
                        ) : (
                          <Form.Item
                            label="Repuesto"
                            className="lc-item"
                            name={[campo.name, 'productoId']}
                            rules={[{ required: true, message: 'Elige el repuesto' }]}
                          >
                            <Select
                              showSearch
                              optionFilterProp="label"
                              loading={productos.isPending}
                              placeholder="Código o nombre del repuesto"
                              options={opcionesRepuesto}
                            />
                          </Form.Item>
                        )}
                        <Form.Item label="IGV" className="lc-igv" name={[campo.name, 'tipoAfectacionIgv']}>
                          <Select options={opcionesAfectacion} />
                        </Form.Item>
                        <Form.Item
                          label="Cantidad"
                          className="lc-cant"
                          name={[campo.name, 'cantidad']}
                          rules={[{ required: true, message: 'Cantidad' }]}
                        >
                          <InputNumber min={1} max={100000} precision={0} style={{ width: '100%' }} />
                        </Form.Item>
                        <Form.Item
                          label={enDolares ? 'Precio unitario US$' : 'Precio unitario S/'}
                          className="lc-precio"
                          name={[campo.name, 'precioUnitario']}
                          rules={[{ required: true, message: 'Precio' }]}
                        >
                          <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
                        </Form.Item>
                        <div className="linea-compra-total lc-total num">
                          <span className="etiqueta">Total</span>
                          {montoEnMoneda(moneda, montos.total)}
                        </div>
                        <Button
                          type="text"
                          className="lc-quitar"
                          aria-label="Quitar línea"
                          icon={<DeleteOutlined />}
                          disabled={campos.length === 1}
                          onClick={() => remove(campo.name)}
                        />
                        <Form.Item name={[campo.name, 'tipo']} hidden>
                          <Input />
                        </Form.Item>
                      </div>
                    )
                  })}
                  <Form.ErrorList errors={errors} />
                  <div className="filtros" style={{ marginTop: 8 }}>
                    <Button icon={<PlusOutlined />} onClick={() => add(lineaVacia('repuesto'))}>
                      Agregar repuesto
                    </Button>
                    <Button icon={<PlusOutlined />} onClick={() => add(lineaVacia('concepto'))}>
                      Agregar flete u otro concepto
                    </Button>
                    {puedeCrearRepuesto && (
                      <Button type="link" onClick={() => setModalRepuesto(true)}>
                        Registrar repuesto nuevo
                      </Button>
                    )}
                  </div>
                </>
              )}
            </Form.List>

            <div className="totales">
              <div>
                <div className="etiqueta">Op. gravadas</div>
                <div className="valor">{montoEnMoneda(moneda, totales.gravado)}</div>
              </div>
              {totales.exonerado > 0 && (
                <div>
                  <div className="etiqueta">Exoneradas</div>
                  <div className="valor">{montoEnMoneda(moneda, totales.exonerado)}</div>
                </div>
              )}
              {totales.inafecto > 0 && (
                <div>
                  <div className="etiqueta">Inafectas</div>
                  <div className="valor">{montoEnMoneda(moneda, totales.inafecto)}</div>
                </div>
              )}
              <div>
                <div className="etiqueta">IGV</div>
                <div className="valor">{montoEnMoneda(moneda, totales.igv)}</div>
              </div>
              <div>
                <div className="etiqueta">Total</div>
                <div className="valor total">{montoEnMoneda(moneda, totales.total)}</div>
              </div>
              {enDolares && tipoCambioCompra ? (
                <div>
                  <div className="etiqueta">Total en soles</div>
                  <div className="valor">{soles(redondear(totales.total * tipoCambioCompra))}</div>
                </div>
              ) : null}
            </div>
            <p className="texto-secundario" style={{ marginTop: 8 }}>
              Compara el total con el del comprobante antes de registrar.
            </p>
          </section>

          <section className="bloque">
            <h2>Pago al proveedor</h2>
            <Form.Item name="registrarPagos" valuePropName="checked" style={{ marginBottom: 12 }}>
              <Checkbox onChange={(evento) => activarPagos(evento.target.checked)}>
                Registrar pagos ahora
              </Checkbox>
            </Form.Item>
            {registrarPagos ? (
              <Form.List
                name="pagos"
                rules={[
                  {
                    validator: async (_, lista: PagoForm[] | undefined) => {
                      const suma = redondear((lista ?? []).reduce((total, pago) => total + (pago?.monto ?? 0), 0))
                      if (suma > totales.total) {
                        throw new Error(`Los pagos (${montoEnMoneda(moneda, suma)}) pasan el total de la compra`)
                      }
                    },
                  },
                ]}
              >
                {(campos, { add, remove }, { errors }) => (
                  <>
                    {campos.map((campo) => (
                      <div key={campo.key} className="linea-pago-compra">
                        <Form.Item
                          className="lp-monto"
                          name={[campo.name, 'monto']}
                          rules={[{ required: true, message: 'Monto' }]}
                        >
                          <InputNumber
                            min={0.01}
                            precision={2}
                            prefix={enDolares ? 'US$' : 'S/'}
                            style={{ width: '100%' }}
                            aria-label="Monto"
                          />
                        </Form.Item>
                        <Form.Item
                          className="lp-metodo"
                          name={[campo.name, 'metodoPagoId']}
                          rules={[{ required: true, message: 'Método' }]}
                        >
                          <Select
                            loading={metodos.isPending}
                            placeholder="Método de pago"
                            aria-label="Método de pago"
                            options={(metodos.data ?? [])
                              .filter((metodo) => metodo.activo)
                              .map((metodo) => ({ value: metodo.id, label: metodo.nombre }))}
                          />
                        </Form.Item>
                        <Form.Item className="lp-referencia" name={[campo.name, 'referencia']}>
                          <Input placeholder="N.° de operación (opcional)" maxLength={100} aria-label="Referencia" />
                        </Form.Item>
                        <Button
                          type="text"
                          className="lp-quitar"
                          aria-label="Quitar pago"
                          icon={<DeleteOutlined />}
                          onClick={() => remove(campo.name)}
                        />
                      </div>
                    ))}
                    <Form.ErrorList errors={errors} />
                    <Button icon={<PlusOutlined />} onClick={() => add({})}>
                      Agregar otro pago
                    </Button>
                    <p className="texto-secundario" style={{ marginTop: 12 }}>
                      Queda por pagar: <strong>{montoEnMoneda(moneda, Math.max(0, redondear(totales.total - totalPagos)))}</strong>.
                      {' '}Lo pagado en efectivo sale de la caja chica abierta.
                    </p>
                    {pagaEnEfectivo && caja.data && !caja.data.tieneCajaAbierta && (
                      <p className="texto-secundario" style={{ marginTop: 4 }}>
                        <strong>No hay caja chica abierta:</strong> ábrela antes o paga por otro método.
                      </p>
                    )}
                  </>
                )}
              </Form.List>
            ) : (
              <p className="texto-secundario">
                La compra queda por pagar; los pagos se registran después desde su detalle.
              </p>
            )}
          </section>
        </Form>
      </div>

      <ModalProveedor
        abierto={modalProveedor}
        onCerrar={() => setModalProveedor(false)}
        onGuardado={(proveedor) => {
          setProveedoresNuevos((anteriores) => [...anteriores, proveedor])
          formulario.setFieldsValue({ proveedorId: proveedor.id })
        }}
      />
      <ModalAltaRapidaRepuesto
        abierto={modalRepuesto}
        sinStockInicial
        onCerrar={() => setModalRepuesto(false)}
        onRegistrado={agregarRepuestoNuevo}
      />
    </>
  )
}
