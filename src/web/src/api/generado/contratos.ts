// Generado por tools/GeneradorTipos a partir de los contratos de la API (Features/**).
// No editar a mano: después de cambiar un contrato, correr `npm run tipos:generar`.

// ── Aprobaciones ────────────────────────────────────────────────────────────

export type RegistrarSolicitudAprobacionRequest = {
  tipo: string
  entidad: string
  entidadId: string
  detalleCambio: string
  valorAnterior?: number | null
  valorSolicitado?: number | null
  motivo?: string | null
}

export type ResolverSolicitudAprobacionRequest = {
  estado: EstadoAprobacionGerencia
  observaciones?: string | null
}

export type SolicitudAprobacionResponse = {
  id: string
  tipo: string
  entidad: string
  entidadId: string
  usuarioSolicitanteId: string | null
  usuarioSolicitanteNombre: string | null
  fechaSolicitud: string
  estadoId: number
  estado: string
  detalleCambio: string
  valorAnterior: number | null
  valorSolicitado: number | null
  motivo: string | null
  usuarioAprobadorId: string | null
  usuarioAprobadorNombre: string | null
  fechaRespuesta: string | null
  observacionesRespuesta: string | null
}

// ── Auditoria ───────────────────────────────────────────────────────────────

export type EventoAuditoriaResponse = {
  id: string
  usuarioId: string | null
  usuarioNombre: string | null
  fecha: string
  accion: string
  entidad: string
  entidadId: string
  detalle: string | null
}

// ── Auth ────────────────────────────────────────────────────────────────────

export type CambiarPasswordRequest = {
  passwordActual: string
  passwordNueva: string
}

export type EliminarCuentaRequest = {
  password: string
}

export type LoginRequest = {
  email: string
  password: string
}

export type LoginResponse = {
  accessToken: string
  accessTokenExpiration: string
  refreshToken: string
  refreshTokenExpiration: string
}

export type MeResponse = {
  id: string
  email: string
  nombreCompleto: string
  activo: boolean
  roles: string[]
  permisos: string[]
  clienteId: string | null
}

export type RefreshRequest = {
  refreshToken: string
}

export type ResetPasswordRequest = {
  nuevaPassword: string
}

// ── CajaChica ───────────────────────────────────────────────────────────────

export type AperturaCajaRequest = {
  montoApertura: number
  observaciones?: string | null
}

export type CajaChicaDetalleResponse = {
  id: string
  montoApertura: number
  montoCierre: number | null
  saldoCalculado: number
  fechaApertura: string
  fechaCierre: string | null
  estado: EstadoCajaChica
  estadoDescripcion: string
  observacionesApertura: string | null
  observacionesCierre: string | null
  usuarioAperturaId: string | null
  usuarioAperturaNombre: string | null
  usuarioCierreId: string | null
  usuarioCierreNombre: string | null
  totalIngresos: number
  totalEgresos: number
  movimientos: MovimientoCajaResponse[]
  totalIngresosEfectivo: number
  totalIngresosOtrosMetodos: number
}

export type CajaChicaResponse = {
  id: string
  montoApertura: number
  montoCierre: number | null
  saldoCalculado: number
  fechaApertura: string
  fechaCierre: string | null
  estado: EstadoCajaChica
  estadoDescripcion: string
  observacionesApertura: string | null
  observacionesCierre: string | null
  usuarioAperturaId: string | null
  usuarioAperturaNombre: string | null
  usuarioCierreId: string | null
  usuarioCierreNombre: string | null
  totalIngresos: number
  totalEgresos: number
  cantidadMovimientos: number
  fechaCreacion: string
  totalIngresosEfectivo: number
  totalIngresosOtrosMetodos: number
}

export type CierreCajaRequest = {
  observaciones?: string | null
}

export type EstadoCajaActualResponse = {
  tieneCajaAbierta: boolean
  caja: CajaChicaDetalleResponse | null
}

export type MovimientoCajaResponse = {
  id: string
  cajaChicaId: string
  tipo: TipoMovimientoCaja
  tipoDescripcion: string
  monto: number
  concepto: string
  referencia: string | null
  fecha: string
  usuarioId: string | null
  usuarioNombre: string | null
  pagoId: string | null
  metodoPagoId: string | null
  metodoPagoNombre: string | null
}

export type RegistrarMovimientoCajaRequest = {
  tipo: TipoMovimientoCaja
  monto: number
  concepto: string
  referencia?: string | null
  cajaChicaId?: string | null
}

export type ResumenMetodosPagoCajaResponse = {
  cajaChicaId: string | null
  totalEfectivo: number
  totalYapePlin: number
  totalTarjeta: number
  totalTransferencia: number
  porMetodo: Record<string, number>
  totalGeneral: number
}

// ── CategoriasProducto ──────────────────────────────────────────────────────

export type CategoriaProductoResponse = {
  id: string
  nombre: string
  cantidadProductos: number
  activo: boolean
  fechaCreacion: string
  stockMinimoDefault: number | null
}

export type CreateCategoriaProductoRequest = {
  nombre: string
  stockMinimoDefault?: number | null
}

export type UpdateCategoriaProductoRequest = {
  nombre: string
  stockMinimoDefault?: number | null
}

// ── Chatbot ─────────────────────────────────────────────────────────────────

export type AsignarAgenteRequest = {
  agenteId: string
}

export type ConsultaBandejaResponse = {
  id: string
  fecha: string
  canal: string
  clienteId: string | null
  clienteNombre: string | null
  nombreContacto: string | null
  telefonoContacto: string | null
  mensajeConsulta: string
  faqItemId: string | null
  faqPregunta: string | null
  requiereAtencionAgente: boolean
  estadoAtencion: string
  estadoAtencionId: number
  agenteAsignadoId: string | null
  agenteNombre: string | null
  notasAgente: string | null
  fechaDerivacion: string | null
  fechaResolucion: string | null
}

export type ConsultaChatbotRequest = {
  mensaje: string
  nombreContacto?: string | null
  telefonoContacto?: string | null
  canal?: string | null
}

export type ConsultaChatbotResponse = {
  resueltoPorFaq: boolean
  faq: FaqResponse | null
  sugerencias: FaqResponse[]
  requiereAgente: boolean
  consultaId: string | null
  mensajeRespuesta: string
}

export type CreateFaqRequest = {
  categoria: string
  pregunta: string
  respuesta: string
  palabrasClave?: string | null
  orden: number
}

export type FaqResponse = {
  id: string
  categoria: string
  pregunta: string
  respuesta: string
  palabrasClave: string | null
  orden: number
  vecesConsultada: number
  activo: boolean
}

export type ResolverConsultaRequest = {
  estado: EstadoAtencionConsulta
  notasAgente?: string | null
}

export type SolicitarAgenteRequest = {
  consultaId?: string | null
  nombreContacto?: string | null
  telefonoContacto: string
  motivo: string
  canal?: string | null
}

export type SolicitudAgenteResponse = {
  consultaId: string
  estado: string
  mensaje: string
}

export type UpdateFaqRequest = {
  categoria: string
  pregunta: string
  respuesta: string
  palabrasClave?: string | null
  orden: number
  activo: boolean
}

// ── Citas ───────────────────────────────────────────────────────────────────

export type ActualizarCitaRequest = {
  vehiculoId: string
  fechaHoraProgramada: string
  duracionMinutos?: number | null
  motivo: string
  observaciones?: string | null
}

export type CambiarEstadoCitaRequest = {
  nuevoEstado: EstadoCita
  observacion?: string | null
}

export type CancelarCitaRequest = {
  motivoCancelacion: string
}

export type CitaDetalleResponse = {
  id: string
  numeroCita: string
  clienteId: string
  clienteNombre: string
  clienteDocumento: string | null
  clienteTelefono: string | null
  clienteEmail: string | null
  vehiculoId: string
  vehiculoMarca: string
  vehiculoModelo: string
  vehiculoPlaca: string | null
  vehiculoAnio: number | null
  fechaHoraProgramada: string
  duracionMinutos: number
  fechaHoraFinEstimada: string
  motivo: string
  observaciones: string | null
  estado: EstadoCita
  estadoDescripcion: string
  motivoCancelacion: string | null
  ordenServicioId: string | null
  numeroOrdenServicio: string | null
  historial: HistorialEstadoCitaResponse[]
  fechaCreacion: string
}

export type CitaListResponse = {
  id: string
  numeroCita: string
  clienteId: string
  clienteNombre: string
  vehiculoId: string
  vehiculoPlaca: string | null
  vehiculoModelo: string
  fechaHoraProgramada: string
  duracionMinutos: number
  motivo: string
  estado: EstadoCita
  estadoDescripcion: string
  ordenServicioId: string | null
  numeroOrdenServicio: string | null
}

export type CrearCitaRequest = {
  clienteId?: string | null
  vehiculoId: string
  fechaHoraProgramada: string
  duracionMinutos?: number | null
  motivo: string
  observaciones?: string | null
}

export type HistorialEstadoCitaResponse = {
  id: string
  estadoAnterior: EstadoCita | null
  estadoAnteriorDescripcion: string | null
  estadoNuevo: EstadoCita
  estadoNuevoDescripcion: string
  usuarioId: string | null
  usuarioNombre: string | null
  fecha: string
  observacion: string | null
}

export type ReprogramarCitaRequest = {
  nuevaFechaHoraProgramada: string
  nuevaDuracionMinutos?: number | null
  motivoReprogramacion?: string | null
}

export type VincularOrdenCitaRequest = {
  ordenServicioId: string
}

// ── Clientes ────────────────────────────────────────────────────────────────

export type AltaRapidaClienteRequest = {
  numeroDocumento: string
  nombreCompleto: string
  telefono?: string | null
  email?: string | null
  direccion?: string | null
  tipoDocumento?: TipoDocumentoCliente | null
}

export type ClienteResponse = {
  id: string
  nombreCompleto: string
  razonSocial: string | null
  documentoIdentidad: string | null
  telefono: string | null
  email: string | null
  direccion: string | null
  observaciones: string | null
  activo: boolean
  tipoDocumento: string | null
  tipoDocumentoId: number | null
  numeroDocumento: string | null
}

export type CreateClienteRequest = {
  nombreCompleto: string
  razonSocial?: string | null
  documentoIdentidad?: string | null
  telefono?: string | null
  email?: string | null
  direccion?: string | null
  observaciones?: string | null
  tipoDocumento?: TipoDocumentoCliente | null
  numeroDocumento?: string | null
}

export type UpdateClienteRequest = {
  nombreCompleto: string
  razonSocial?: string | null
  documentoIdentidad?: string | null
  telefono?: string | null
  email?: string | null
  direccion?: string | null
  observaciones?: string | null
  tipoDocumento?: TipoDocumentoCliente | null
  numeroDocumento?: string | null
}

// ── Compras ─────────────────────────────────────────────────────────────────

export type ActualizarCompraRequest = {
  proveedorId: string
  serie: string
  numero: string
  fechaEmision: string
  fechaVencimiento?: string | null
  guiaRemision?: string | null
  observaciones?: string | null
}

export type AnularRequest = {
  motivo: string
}

export type CompraResponse = {
  id: string
  numeroCompra: string
  proveedorId: string
  proveedorNombre: string
  proveedorDocumento: string
  tipoComprobante: TipoComprobanteCompra
  serie: string
  numero: string
  fechaEmision: string
  fechaVencimiento: string | null
  moneda: MonedaCompra
  tipoCambio: number
  porcentajeIgv: number
  preciosIncluyenIgv: boolean
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  montoIgv: number
  total: number
  totalSoles: number
  totalPagado: number
  saldo: number
  estadoPago: string | null
  vencida: boolean
  estado: EstadoCompra
  pedidoLimaId: string | null
  numeroPedidoLima: string | null
  guiaRemision: string | null
  observaciones: string | null
  usuarioNombre: string | null
  fechaCreacion: string
  fechaAnulacion: string | null
  motivoAnulacion: string | null
  usuarioAnulacionNombre: string | null
  detalles: DetalleCompraResponse[]
  pagos: PagoCompraResponse[]
}

export type CompraResumenResponse = {
  id: string
  numeroCompra: string
  proveedorId: string
  proveedorNombre: string
  proveedorDocumento: string
  tipoComprobante: TipoComprobanteCompra
  serie: string
  numero: string
  fechaEmision: string
  fechaVencimiento: string | null
  moneda: MonedaCompra
  tipoCambio: number
  total: number
  totalSoles: number
  totalPagado: number
  saldo: number
  estadoPago: string | null
  vencida: boolean
  estado: EstadoCompra
  pedidoLimaId: string | null
  numeroPedidoLima: string | null
  cantidadLineas: number
  fechaCreacion: string
}

export type CrearCompraRequest = {
  proveedorId: string
  tipoComprobante: TipoComprobanteCompra
  serie: string
  numero: string
  fechaEmision: string
  fechaVencimiento?: string | null
  moneda: MonedaCompra
  tipoCambio?: number | null
  porcentajeIgv?: number | null
  preciosIncluyenIgv: boolean
  pedidoLimaId?: string | null
  guiaRemision?: string | null
  observaciones?: string | null
  detalles: DetalleCompraRequest[]
  pagos?: RegistrarPagoCompraRequest[] | null
}

export type DetalleCompraRequest = {
  productoId?: string | null
  descripcion?: string | null
  cantidad: number
  precioUnitario: number
  tipoAfectacionIgv: TipoAfectacionIgv
}

export type DetalleCompraResponse = {
  id: string
  productoId: string | null
  productoCodigo: string | null
  descripcion: string
  cantidad: number
  precioUnitario: number
  tipoAfectacionIgv: TipoAfectacionIgv
  subtotal: number
  montoIgv: number
  total: number
  costoUnitarioSoles: number
  mueveStock: boolean
}

export type GuardarProveedorRequest = {
  tipoDocumento: TipoDocumentoCliente
  numeroDocumento: string
  razonSocial: string
  telefono?: string | null
  email?: string | null
  direccion?: string | null
  contacto?: string | null
}

export type PagoCompraResponse = {
  id: string
  monto: number
  montoSoles: number
  metodoPagoId: string
  metodoPagoNombre: string
  metodoPagoCodigo: string
  fecha: string
  referencia: string | null
  observaciones: string | null
  usuarioNombre: string | null
  salioDeCaja: boolean
  anulado: boolean
  fechaAnulacion: string | null
  motivoAnulacion: string | null
  usuarioAnulacionNombre: string | null
}

export type ProveedorResponse = {
  id: string
  tipoDocumento: TipoDocumentoCliente
  numeroDocumento: string
  razonSocial: string
  telefono: string | null
  email: string | null
  direccion: string | null
  contacto: string | null
  fechaCreacion: string
}

export type RegistrarPagoCompraRequest = {
  monto: number
  metodoPagoId: string
  referencia?: string | null
  observaciones?: string | null
}

// ── Configuracion ───────────────────────────────────────────────────────────

export type ActualizarConfiguracionEmpresaRequest = {
  nombreEmpresa?: string | null
  razonSocial?: string | null
  ruc?: string | null
  direccion?: string | null
  telefono?: string | null
  email?: string | null
  porcentajeIgv?: number | null
  monedaBase?: string | null
  tipoCambioVigente?: number | null
  metodoCosteo?: MetodoCosteo | null
}

export type ActualizarTipoCambioRequest = {
  tipoCambio?: number | null
  valor?: number | null
  valorVenta?: number | null
  valorCompra?: number | null
  monedaOrigen?: string | null
  monedaDestino?: string | null
  fechaVigencia?: string | null
  observaciones?: string | null
}

export type ConfiguracionEmpresaResponse = {
  id: string
  nombreEmpresa: string
  razonSocial: string | null
  ruc: string | null
  direccion: string | null
  telefono: string | null
  email: string | null
  porcentajeIgv: number
  monedaBase: string
  tipoCambioVigente: number | null
  fechaActualizacionTipoCambio: string | null
  metodoCosteo: MetodoCosteo
}

export type ConversionMonedaRequest = {
  montoUsd: number
  tipoCambio?: number | null
}

export type ConversionMonedaResponse = {
  montoUsd: number
  tipoCambio: number
  montoPen: number
  formula: string
}

export type HistorialTipoCambioResponse = {
  id: string
  monedaOrigen: string
  monedaDestino: string
  valorCompra: number
  valorVenta: number
  fechaVigencia: string
  observaciones: string | null
  usuarioId: string | null
  usuarioNombre: string | null
  fechaCreacion: string
}

export type TipoCambioResponse = {
  configurado: boolean
  tipoCambio: number | null
  valorVenta: number | null
  valorCompra: number | null
  monedaBase: string
  monedaExtranjera: string
  fechaActualizacion: string | null
  ultimoUsuarioNombre: string | null
}

// ── Dashboard ───────────────────────────────────────────────────────────────

export type ClientesVehiculosResumenResponse = {
  clientesActivos: number
  vehiculosActivos: number
}

export type DashboardResumenResponse = {
  fechaDesde: string | null
  fechaHasta: string | null
  ordenesServicio: OrdenesServicioResumenResponse
  ventas: VentasResumenResponse
  inventario: InventarioResumenResponse
  crmActivos: ClientesVehiculosResumenResponse
}

export type InventarioResumenResponse = {
  productosConStockBajo: number
  productosSinStock: number
  valorEstimadoInventario: number
}

export type OrdenesServicioResumenResponse = {
  abierta: number
  diagnostico: number
  aprobada: number
  enProceso: number
  lista: number
  entregada: number
  cancelada: number
  total: number
}

export type VentasResumenResponse = {
  confirmadas: number
  cotizaciones: number
  anuladas: number
  montoConfirmadas: number
  ticketPromedio: number
}

// ── Dominio ─────────────────────────────────────────────────────────────────

/** NoAplica = 0 · Pendiente = 1 · Aprobado = 2 · Rechazado = 3 */
export type EstadoAprobacionGerencia = 0 | 1 | 2 | 3

/** Pendiente = 0 · EnAtencion = 1 · Resuelto = 2 · Descartado = 3 */
export type EstadoAtencionConsulta = 0 | 1 | 2 | 3

/** Abierta = 0 · Cerrada = 1 */
export type EstadoCajaChica = 0 | 1

export type EstadoCita = 'Pendiente' | 'Confirmada' | 'EnTaller' | 'Completada' | 'Cancelada' | 'NoAsistio'

/** Registrada = 0 · Anulada = 1 */
export type EstadoCompra = 0 | 1

/** Abierta = 0 · Diagnostico = 1 · Aprobada = 2 · EnProceso = 3 · Lista = 4 · Entregada = 5 · Cancelada = 6 */
export type EstadoOrdenServicio = 0 | 1 | 2 | 3 | 4 | 5 | 6

export type EstadoPedidoLima = 'Pendiente' | 'Confirmado' | 'EnPreparacion' | 'EnTransito' | 'Recibido' | 'Entregado' | 'Cancelado'

/** Pendiente = 0 · Aprobado = 1 · Rechazado = 2 */
export type EstadoPresupuestoCliente = 0 | 1 | 2

/** Cotizacion = 0 · Confirmada = 1 · Anulada = 2 */
export type EstadoVenta = 0 | 1 | 2

/** Ingreso = 0 · Diagnostico = 1 · Reparacion = 2 · Entrega = 3 */
export type EtapaFotoOrdenServicio = 0 | 1 | 2 | 3

/** PromedioPonderado = 0 · UltimoCosto = 1 */
export type MetodoCosteo = 0 | 1

/** EnTaller = 0 · EnSitio = 1 */
export type ModalidadAtencion = 0 | 1

/** PEN = 0 · USD = 1 */
export type MonedaCompra = 0 | 1

/** Android = 0 · IOS = 1 */
export type PlataformaDispositivo = 0 | 1

/** Gravado = 0 · Exonerado = 1 · Inafecto = 2 */
export type TipoAfectacionIgv = 0 | 1 | 2

/** MantenimientoPreventivo = 0 · MantenimientoCorrectivo = 1 · ReclamoGarantia = 2 · Gratuito = 3 */
export type TipoAtencion = 0 | 1 | 2 | 3

/** Factura = 0 · Boleta = 1 · Ticket = 2 · NotaVenta = 3 · ReciboHonorarios = 4 · Otro = 5 */
export type TipoComprobanteCompra = 0 | 1 | 2 | 3 | 4 | 5

/** DNI = 0 · RUC = 1 · Otro = 2 */
export type TipoDocumentoCliente = 0 | 1 | 2

/** Menor = 0 · Mayor = 1 */
export type TipoFalla = 0 | 1

/** Repuesto = 0 · Servicio = 1 · ManoDeObra = 2 · Terceros = 3 */
export type TipoItemServicio = 0 | 1 | 2 | 3

/** Kilometraje = 0 · Horas = 1 */
export type TipoMedidor = 0 | 1

/** Ingreso = 0 · Egreso = 1 */
export type TipoMovimientoCaja = 0 | 1

/** SolicitudAprobacion = 0 · PresupuestoEnviado = 1 · OrdenLista = 2 · PedidoLimaRecibido = 3 · CitaConfirmada = 4 · OrdenAsignada = 5 */
export type TipoNotificacion = 0 | 1 | 2 | 3 | 4 | 5

/** Motocicleta = 0 · Cuatrimoto = 1 · MotoAcuatica = 2 · Generador = 3 · Otro = 4 */
export type TipoUnidad = 0 | 1 | 2 | 3 | 4

// ── Fotos ───────────────────────────────────────────────────────────────────

export type FotoOrdenServicioResponse = {
  id: string
  ordenServicioId: string
  nombreArchivoOriginal: string
  urlRelativa: string
  contentType: string
  tamanioBytes: number
  etapa: EtapaFotoOrdenServicio
  etapaNombre: string
  usuarioId: string
  usuarioNombre: string | null
  observacion: string | null
  fechaCreacion: string
}

export type SubirFotoMetadataRequest = {
  etapa?: EtapaFotoOrdenServicio
  observacion?: string | null
}

// ── Importaciones ───────────────────────────────────────────────────────────

export type ErrorFilaImportacionDto = {
  hoja: string
  fila: number
  columna: string
  valorLeido: string | null
  mensaje: string
}

export type HistorialImportacionItemDto = {
  id: string
  fecha: string
  usuarioId: string | null
  usuarioNombre: string | null
  accion: string
  nombreArchivo: string
  exitoso: boolean
  soloValidacion: boolean
  categoriaId: string | null
  clientesProcesados: number
  catalogoProcesados: number
  totalErrores: number
  unidadesProcesadas: number
  repuestosProcesados: number
  modoDuplicados: string | null
}

export type ResultadoImportacionExcelDto = {
  exitoso: boolean
  soloValidacion: boolean
  nombreArchivo: string
  fechaProceso: string
  categoriaId: string | null
  clientes: ResumenHojaImportacionDto
  catalogo: ResumenHojaImportacionDto
  errores: ErrorFilaImportacionDto[]
  unidades: ResumenHojaImportacionDto | null
  repuestos: ResumenHojaImportacionDto | null
  modoDuplicados: string
}

export type ResumenHojaImportacionDto = {
  totalFilasLeidas: number
  filasValidas: number
  filasConError: number
  registrosCreados: number
  registrosOmitidos: number
}

// ── Inventario ──────────────────────────────────────────────────────────────

export type ActualizarFotoProductoRequest = {
  fotoUrl: string
}

export type AltaRapidaProductoRequest = {
  nombre: string
  precioVenta: number
  codigo?: string | null
  marca?: string | null
  categoriaId?: string | null
  stockInicial?: number
}

export type CreateProductoRequest = {
  categoriaId: string
  codigo: string
  nombre: string
  descripcion?: string | null
  unidad?: string | null
  precioVenta: number
  stockInicial: number
  stockMinimo?: number | null
  costo?: number
  marca?: string | null
  fotoUrl?: string | null
}

export type MovimientoInventarioResponse = {
  id: string
  productoId: string
  productoCodigo: string
  productoNombre: string
  tipo: string
  tipoId: number
  cantidad: number
  motivo: string | null
  ordenServicioId: string | null
  ventaId: string | null
  fechaCreacion: string
  costoUnitario: number | null
}

export type ProductoResponse = {
  id: string
  codigo: string
  nombre: string
  descripcion: string | null
  unidad: string
  precioVenta: number
  stockActual: number
  stockMinimo: number | null
  esBajoStock: boolean
  categoriaId: string
  categoriaNombre: string
  activo: boolean
  fechaCreacion: string
  costo: number
  stockMinimoEfectivo: number
  marca: string | null
  fotoUrl: string | null
}

export type RegistrarAjusteRequest = {
  nuevoStock: number
  motivo: string
  costoUnitario?: number | null
}

export type RegistrarEntradaRequest = {
  cantidad: number
  motivo: string
  costoUnitario?: number | null
}

export type RegistrarSalidaRequest = {
  cantidad: number
  motivo: string
}

export type UpdateProductoRequest = {
  categoriaId: string
  codigo: string
  nombre: string
  descripcion?: string | null
  unidad?: string | null
  precioVenta: number
  stockMinimo?: number | null
  costo?: number | null
  marca?: string | null
  fotoUrl?: string | null
}

// ── Notificaciones ──────────────────────────────────────────────────────────

export type ConteoNotificacionesResponse = {
  noLeidas: number
}

export type DarDeBajaDispositivoRequest = {
  token: string
}

export type NotificacionResponse = {
  id: string
  tipo: TipoNotificacion
  titulo: string
  mensaje: string
  entidad: string | null
  entidadId: string | null
  cantidad: number
  fechaCreacion: string
  fechaLeida: string | null
}

export type RegistrarDispositivoRequest = {
  token: string
  plataforma: PlataformaDispositivo
}

// ── OrdenesServicio ─────────────────────────────────────────────────────────

export type ActualizarDetalleServicioRequest = {
  precioUnitario?: number | null
  cantidad?: number | null
  descripcion?: string | null
  tipoAfectacionIgv?: TipoAfectacionIgv | null
}

export type ActualizarOrdenServicioRequest = {
  motivoFalla?: string | null
  diagnostico?: string | null
  solucion?: string | null
  observaciones?: string | null
  fechaEstimadaEntrega?: string | null
  tipoAtencion?: TipoAtencion | null
  modalidadAtencion?: ModalidadAtencion | null
  tipoFalla?: TipoFalla | null
  kilometrajeIngreso?: number | null
  horasUsoIngreso?: number | null
  lecturaMedidorIngreso?: number | null
  tecnicoAsignadoId?: string | null
}

export type AgregarDetalleServicioRequest = {
  productoId?: string | null
  descripcion?: string | null
  cantidad?: number
  precioUnitario?: number | null
  servicioId?: string | null
  tipoItem?: TipoItemServicio | null
  tipoAfectacionIgv?: TipoAfectacionIgv | null
}

export type AperturaOrdenServicioRequest = {
  vehiculoId?: string | null
  tecnicoAsignadoId?: string | null
  observaciones?: string | null
  motivoFalla?: string | null
  fechaEstimadaEntrega?: string | null
  tipoAtencion?: TipoAtencion
  modalidadAtencion?: ModalidadAtencion
  tipoFalla?: TipoFalla | null
  kilometrajeIngreso?: number | null
  horasUsoIngreso?: number | null
  lecturaMedidorIngreso?: number | null
  clienteId?: string | null
  clienteNuevo?: ClienteNuevoRequest | null
  unidadNueva?: UnidadNuevaRequest | null
}

export type AprobacionGerenciaRequest = {
  estado: EstadoAprobacionGerencia
  observaciones?: string | null
}

export type AsignarTecnicoRequest = {
  tecnicoId?: string | null
  tecnicoAsignadoId?: string | null
  observaciones?: string | null
}

export type CambiarEstadoOrdenServicioRequest = {
  nuevoEstado: EstadoOrdenServicio
  observaciones?: string | null
}

export type ClienteNuevoRequest = {
  tipoDocumento?: TipoDocumentoCliente | null
  numeroDocumento: string
  nombreCompleto: string
  telefono?: string | null
  email?: string | null
}

export type DetalleServicioResponse = {
  id: string
  productoId: string | null
  productoCodigo: string | null
  descripcion: string
  cantidad: number
  precioUnitario: number
  subtotal: number
  esRepuesto: boolean
  servicioId: string | null
  servicioNombre: string | null
  tipoItem: TipoItemServicio
  tipoItemNombre: string | null
  costoUnitarioHistorico: number
  tipoAfectacionIgv: TipoAfectacionIgv
  tipoAfectacionIgvNombre: string | null
  subtotalGravado: number
  porcentajeIgvAplicado: number
  montoIgv: number
  total: number
}

export type FormatoAtencionClienteDto = {
  id: string
  nombreCompleto: string
  razonSocial: string | null
  tipoDocumento: string | null
  numeroDocumento: string | null
  telefono: string | null
  email: string | null
  direccion: string | null
}

export type FormatoAtencionFinancieroDto = {
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  montoIgv: number
  total: number
  totalPagado: number
  saldoPendiente: number
  porcentajeIgv: number
  moneda: string
}

export type FormatoAtencionItemDto = {
  id: string
  tipoItem: TipoItemServicio
  tipoItemNombre: string
  descripcion: string
  cantidad: number
  precioUnitario: number
  total: number
  afectacionIgv: string
}

export type FormatoAtencionOrdenDto = {
  id: string
  numeroOrden: string
  estadoId: number
  estadoNombre: string
  fechaIngreso: string
  fechaEstimadaEntrega: string | null
  fechaSalida: string | null
  tipoAtencion: string
  modalidadAtencion: string
  tipoFalla: string | null
}

export type FormatoAtencionResponse = {
  empresa: FormatoAtencionTallerDto
  orden: FormatoAtencionOrdenDto
  cliente: FormatoAtencionClienteDto
  unidad: FormatoAtencionUnidadDto
  trabajo: FormatoAtencionTrabajoDto
  items: FormatoAtencionItemDto[]
  financiero: FormatoAtencionFinancieroDto
  fechaEmision: string
}

export type FormatoAtencionTallerDto = {
  nombreTaller: string
  razonSocial: string
  ruc: string | null
  direccion: string | null
  telefono: string | null
  email: string | null
}

export type FormatoAtencionTrabajoDto = {
  motivoFalla: string | null
  diagnostico: string | null
  solucion: string | null
  observaciones: string | null
  tecnicoResponsable: string | null
  tecnicoEmail: string | null
}

export type FormatoAtencionUnidadDto = {
  id: string
  tipoUnidad: string
  marca: string
  modelo: string
  anio: number | null
  placa: string | null
  numeroSerieVIN: string | null
  numeroMotor: string | null
  color: string | null
  tipoMedidor: string
  lecturaIngreso: number | null
  lecturaActualSalida: number | null
}

export type HistorialEstadoOrdenResponse = {
  id: string
  ordenServicioId: string
  estadoAnterior: string | null
  estadoAnteriorId: number | null
  estadoNuevo: string
  estadoNuevoId: number
  usuarioId: string | null
  usuarioNombre: string | null
  fechaCambio: string
  observaciones: string | null
}

export type OrdenServicioDetalleResponse = {
  id: string
  vehiculoId: string
  vehiculoPlaca: string | null
  vehiculoMarca: string
  vehiculoModelo: string
  vehiculoAnio: number | null
  vehiculoKilometraje: number | null
  vehiculoColor: string | null
  clienteId: string
  clienteNombre: string
  clienteTelefono: string | null
  clienteDocumentoIdentidad: string | null
  tecnicoAsignadoId: string | null
  tecnicoNombre: string | null
  estado: string
  estadoId: number
  fechaApertura: string
  fechaCierre: string | null
  diagnostico: string | null
  observaciones: string | null
  activo: boolean
  detalles: DetalleServicioResponse[]
  total: number
  numeroOrden: string | null
  fechaIngreso: string | null
  fechaEstimadaEntrega: string | null
  fechaSalida: string | null
  motivoFalla: string | null
  solucion: string | null
  tipoAtencion: string | null
  tipoAtencionId: number | null
  modalidadAtencion: string | null
  modalidadAtencionId: number | null
  tipoFalla: string | null
  tipoFallaId: number | null
  kilometrajeIngreso: number | null
  horasUsoIngreso: number | null
  lecturaMedidorIngreso: number | null
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  montoIgv: number
  estadoPresupuestoClienteId: number
  estadoPresupuestoCliente: string
  fechaRespuestaCliente: string | null
  observacionesPresupuestoCliente: string | null
  estadoAprobacionGerenciaId: number
  estadoAprobacionGerencia: string
  fechaAprobacionGerencia: string | null
  usuarioAprobacionGerenciaId: string | null
  usuarioAprobacionGerenciaNombre: string | null
  observacionesAprobacionGerencia: string | null
  tipoUnidad: string | null
  numeroSerieVIN: string | null
  numeroMotor: string | null
  historial: HistorialEstadoOrdenResponse[] | null
  ventaId: string | null
  comprobanteSerieNumero: string | null
  tipoMedidor: string | null
  totalPagado: number
  saldo: number
  estadoPago: string
  pagos: PagoResponse[] | null
  fechaEnvioPresupuesto: string | null
}

export type OrdenServicioResponse = {
  id: string
  vehiculoId: string
  vehiculoPlaca: string | null
  vehiculoMarca: string
  vehiculoModelo: string
  clienteId: string
  clienteNombre: string
  tecnicoAsignadoId: string | null
  tecnicoNombre: string | null
  estado: string
  estadoId: number
  fechaApertura: string
  fechaCierre: string | null
  diagnostico: string | null
  observaciones: string | null
  activo: boolean
  numeroOrden: string | null
  fechaIngreso: string | null
  fechaEstimadaEntrega: string | null
  fechaSalida: string | null
  motivoFalla: string | null
  solucion: string | null
  tipoAtencion: string | null
  tipoAtencionId: number | null
  modalidadAtencion: string | null
  modalidadAtencionId: number | null
  tipoFalla: string | null
  tipoFallaId: number | null
  kilometrajeIngreso: number | null
  horasUsoIngreso: number | null
  lecturaMedidorIngreso: number | null
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  montoIgv: number
  total: number
  estadoPresupuestoClienteId: number
  estadoPresupuestoCliente: string
  fechaRespuestaCliente: string | null
  observacionesPresupuestoCliente: string | null
  estadoAprobacionGerenciaId: number
  estadoAprobacionGerencia: string
  fechaAprobacionGerencia: string | null
  usuarioAprobacionGerenciaId: string | null
  usuarioAprobacionGerenciaNombre: string | null
  observacionesAprobacionGerencia: string | null
  ventaId: string | null
  comprobanteSerieNumero: string | null
  tipoMedidor: string | null
  totalPagado: number
  saldo: number
  estadoPago: string
  fechaEnvioPresupuesto: string | null
}

export type RegistrarDiagnosticoRequest = {
  diagnostico: string
  tecnicoAsignadoId?: string | null
  observaciones?: string | null
  solucion?: string | null
  fechaEstimadaEntrega?: string | null
  tipoFalla?: TipoFalla | null
}

export type ResponderPresupuestoClienteRequest = {
  estado: EstadoPresupuestoCliente
  observaciones?: string | null
}

export type SolicitarAprobacionGerenciaRequest = {
  observaciones: string
}

export type UnidadNuevaRequest = {
  tipoUnidad: TipoUnidad
  tipoMedidor: TipoMedidor
  marca: string
  modelo: string
  placa?: string | null
  numeroSerieVIN?: string | null
  numeroMotor?: string | null
  anio?: number | null
  color?: string | null
}

// ── PedidosLima ─────────────────────────────────────────────────────────────

export type ActualizarPedidoLimaRequest = {
  empresaTransporte?: string | null
  numeroGuia?: string | null
  fechaEstimadaLlegada?: string | null
  fechaLlegada?: string | null
  fechaEntrega?: string | null
  observaciones?: string | null
}

export type ActualizarPrecioDetallePedidoLimaRequest = {
  precioUnitario: number
  cantidad?: number | null
}

export type AprobacionGerenciaPedidoLimaRequest = {
  estado: EstadoAprobacionGerencia
  observaciones?: string | null
}

export type CambiarEstadoPedidoLimaRequest = {
  nuevoEstado: EstadoPedidoLima
  observacion?: string | null
}

export type CancelarPedidoLimaRequest = {
  motivoCancelacion: string
}

export type CrearDetallePedidoLimaRequest = {
  productoId: string
  cantidad: number
  precioUnitario?: number | null
  tipoAfectacionIgv?: TipoAfectacionIgv | null
}

export type CrearPedidoLimaRequest = {
  clienteId?: string | null
  empresaTransporte?: string | null
  numeroGuia?: string | null
  fechaEstimadaLlegada?: string | null
  observaciones?: string | null
  detalles: CrearDetallePedidoLimaRequest[]
}

export type DetallePedidoLimaResponse = {
  id: string
  productoId: string
  productoCodigo: string
  productoNombre: string
  cantidad: number
  precioUnitario: number
  costoUnitarioHistorico: number
  tipoAfectacionIgv: TipoAfectacionIgv
  tipoAfectacionIgvDescripcion: string
  subtotalGravado: number
  porcentajeIgvAplicado: number
  montoIgv: number
  total: number
}

export type HistorialEstadoPedidoLimaResponse = {
  id: string
  estadoAnterior: EstadoPedidoLima | null
  estadoAnteriorDescripcion: string | null
  estadoNuevo: EstadoPedidoLima
  estadoNuevoDescripcion: string
  usuarioId: string | null
  usuarioNombre: string | null
  fecha: string
  observacion: string | null
}

export type PedidoLimaResponse = {
  id: string
  numeroPedido: string
  clienteId: string
  clienteNombre: string
  clienteDocumento: string | null
  clienteTelefono: string | null
  fecha: string
  estado: EstadoPedidoLima
  estadoDescripcion: string
  empresaTransporte: string | null
  numeroGuia: string | null
  fechaEstimadaLlegada: string | null
  fechaLlegada: string | null
  fechaEntrega: string | null
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  porcentajeIgv: number
  montoIgv: number
  total: number
  observaciones: string | null
  motivoCancelacion: string | null
  stockDeducido: boolean
  detalles: DetallePedidoLimaResponse[]
  historial: HistorialEstadoPedidoLimaResponse[]
  fechaCreacion: string
  totalPagado: number
  saldo: number
  estadoPago: string
  estadoAprobacionGerenciaId: number
  estadoAprobacionGerencia: string
  pagos: PagoResponse[] | null
}

// ── Permisos ────────────────────────────────────────────────────────────────

export type PermisoResponse = {
  id: string
  codigo: string
  descripcion: string | null
  activo: boolean
}

// ── Portal ──────────────────────────────────────────────────────────────────

export type AtencionServicioUnidadResponse = {
  ordenServicioId: string
  numeroOrden: string | null
  fechaIngreso: string
  fechaSalida: string | null
  estado: string
  estadoId: number
  kilometrajeIngreso: number | null
  horasUsoIngreso: number | null
  lecturaMedidorIngreso: number | null
  tipoMedidor: string | null
  motivoFalla: string | null
  solucion: string | null
  observacionesCliente: string | null
  total: number
  items: HistorialServicioItemResponse[]
}

export type HistorialServicioItemResponse = {
  id: string
  descripcion: string
  servicioNombre: string | null
  productoCodigo: string | null
  tipoItem: TipoItemServicio
  tipoItemNombre: string
  cantidad: number
  precioUnitario: number
  total: number
}

export type PortalComprobanteResponse = {
  id: string
  ventaId: string
  ordenServicioId: string | null
  numeroOrden: string | null
  tipo: string
  serie: string | null
  numero: string | null
  fecha: string
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  porcentajeIgv: number
  montoIgv: number
  total: number
  estado: string
  metodoPagoPrincipal: string | null
  observaciones: string | null
}

export type PortalResumenResponse = {
  clienteId: string
  clienteNombre: string
  cantidadUnidades: number
  cantidadOrdenesActivas: number
  cantidadPresupuestosPendientes: number
  saldoPendienteTotal: number
}

// ── Reportes ────────────────────────────────────────────────────────────────

export type ExportarOrdenesServicioExcelRequest = {
  fechaDesde?: string | null
  fechaHasta?: string | null
  estado?: EstadoOrdenServicio | null
  tecnicoId?: string | null
}

export type ExportarVentasExcelRequest = {
  fechaDesde?: string | null
  fechaHasta?: string | null
  estado?: EstadoVenta | null
  clienteId?: string | null
}

export type OrdenServicioReporteResponse = {
  id: string
  vehiculoId: string
  vehiculoPlaca: string | null
  vehiculoMarca: string
  vehiculoModelo: string
  clienteId: string
  clienteNombre: string
  tecnicoId: string | null
  tecnicoNombre: string | null
  estado: string
  estadoId: number
  fechaApertura: string
  fechaCierre: string | null
  tiempoAtencionHoras: number | null
  numeroOrden: string | null
  tipoMedidor: string | null
}

export type RentabilidadOperacionDetalleResponse = {
  documentoTipo: string
  numeroDocumento: string
  operacionId: string
  fecha: string
  clienteNombre: string
  ingresoNeto: number
  costoHistoricoRegistrado: number
  utilidadBruta: number
  margenPorcentual: number
}

export type RentabilidadPorTipoItemResponse = {
  tipoItem: string
  cantidadItems: number
  ingresoNeto: number
  costoHistoricoRegistrado: number
  utilidadBruta: number
  margenPorcentual: number
}

export type RentabilidadReporteResponse = {
  fechaDesde: string | null
  fechaHasta: string | null
  resumen: RentabilidadResumenResponse
  desglosePorTipo: RentabilidadPorTipoItemResponse[]
  detalleOperaciones: RentabilidadOperacionDetalleResponse[]
  rankingRepuestos: RentabilidadRepuestoRankingResponse[]
}

export type RentabilidadRepuestoRankingResponse = {
  productoId: string
  codigo: string
  descripcion: string
  unidadesVendidas: number
  ingresoNeto: number
  costoHistorico: number
  utilidadBruta: number
  margenPorcentual: number
}

export type RentabilidadResumenResponse = {
  ingresosTotalesSinIgv: number
  costoTotalHistorico: number
  utilidadBrutaTotal: number
  margenPorcentualGlobal: number
}

export type StockBajoResponse = {
  productoId: string
  codigo: string
  nombre: string
  categoriaId: string
  categoriaNombre: string
  stockActual: number
  stockMinimo: number
  diferencia: number
  precioVenta: number
}

export type VentaReporteResponse = {
  id: string
  clienteId: string
  clienteNombre: string
  ordenServicioId: string | null
  estado: string
  estadoId: number
  fecha: string
  total: number
}

// ── Roles ───────────────────────────────────────────────────────────────────

export type AsignarPermisoRequest = {
  permisoId: string
}

export type CreateRolRequest = {
  nombre: string
  descripcion?: string | null
}

export type RolResponse = {
  id: string
  nombre: string
  descripcion: string | null
  activo: boolean
}

export type UpdateRolRequest = {
  nombre: string
  descripcion?: string | null
}

// ── Servicios ───────────────────────────────────────────────────────────────

export type ActualizarServicioRequest = {
  nombre: string
  precioSugerido: number
  tipoAfectacionIgv?: TipoAfectacionIgv
  activo?: boolean | null
}

export type CrearServicioRequest = {
  nombre: string
  precioSugerido: number
  tipoAfectacionIgv?: TipoAfectacionIgv
}

export type ServicioResponse = {
  id: string
  nombre: string
  precioSugerido: number
  tipoAfectacionIgv: TipoAfectacionIgv
  activo: boolean
  fechaCreacion: string
}

// ── Usuarios ────────────────────────────────────────────────────────────────

export type AsignarRolRequest = {
  rolId: string
}

export type CreateUsuarioRequest = {
  email: string
  password: string
  nombreCompleto: string
  phoneNumber?: string | null
}

export type UpdateUsuarioRequest = {
  nombreCompleto: string
  phoneNumber?: string | null
}

export type UsuarioResponse = {
  id: string
  email: string
  nombreCompleto: string
  phoneNumber: string | null
  activo: boolean
  roles: string[]
}

// ── Vehiculos ───────────────────────────────────────────────────────────────

export type AltaRapidaVehiculoRequest = {
  clienteId: string
  placa?: string | null
  marca: string
  modelo: string
  kilometraje?: number | null
  color?: string | null
}

export type CreateVehiculoRequest = {
  clienteId: string
  placa?: string | null
  marca: string
  modelo: string
  anio?: number | null
  kilometraje?: number | null
  color?: string | null
  observaciones?: string | null
  tipoUnidad?: TipoUnidad
  numeroSerieVIN?: string | null
  numeroMotor?: string | null
  tipoMedidor?: TipoMedidor
  horasUso?: number | null
  valorEstimado?: number | null
  lecturaMedidorActual?: number | null
}

export type UpdateVehiculoRequest = {
  clienteId: string
  placa?: string | null
  marca: string
  modelo: string
  anio?: number | null
  kilometraje?: number | null
  color?: string | null
  observaciones?: string | null
  tipoUnidad?: TipoUnidad
  numeroSerieVIN?: string | null
  numeroMotor?: string | null
  tipoMedidor?: TipoMedidor
  horasUso?: number | null
  valorEstimado?: number | null
  lecturaMedidorActual?: number | null
}

export type VehiculoResponse = {
  id: string
  clienteId: string
  clienteNombre: string
  placa: string | null
  marca: string
  modelo: string
  anio: number | null
  kilometraje: number | null
  color: string | null
  observaciones: string | null
  activo: boolean
  tipoUnidad: string
  tipoUnidadId: number
  numeroSerieVIN: string | null
  numeroMotor: string | null
  tipoMedidor: string
  tipoMedidorId: number
  horasUso: number | null
  valorEstimado: number | null
  lecturaMedidorActual: number | null
}

// ── Ventas ──────────────────────────────────────────────────────────────────

export type ActualizarVentaRequest = {
  detalles: CreateDetalleVentaRequest[]
  observaciones?: string | null
}

export type ComprobanteResponse = {
  id: string
  ventaId: string
  tipo: string
  serie: string | null
  numero: string | null
  estado: string
  fechaCreacion: string
  activo: boolean
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  porcentajeIgv: number
  montoIgv: number
  total: number
  metodoPagoPrincipal: string | null
  observaciones: string | null
  ordenServicioId: string | null
}

export type CreateDetalleVentaRequest = {
  productoId: string
  cantidad: number
  precioUnitario?: number | null
  descuento?: number | null
  tipoAfectacionIgv?: TipoAfectacionIgv | null
}

export type CreateVentaRequest = {
  clienteId: string
  ordenServicioId?: string | null
  detalles?: CreateDetalleVentaRequest[] | null
  esCotizacion?: boolean
}

export type DetalleVentaResponse = {
  id: string
  productoId: string | null
  productoCodigo: string | null
  productoNombre: string
  cantidad: number
  precioUnitario: number
  subtotal: number
  tipoItem: number
  tipoItemNombre: string | null
  servicioId: string | null
  costoUnitarioHistorico: number
  tipoAfectacionIgv: number
  tipoAfectacionIgvNombre: string | null
  subtotalGravado: number
  porcentajeIgvAplicado: number
  montoIgv: number
  total: number
}

export type MetodoPagoResponse = {
  id: string
  codigo: string
  nombre: string
  activo: boolean
}

export type PagoResponse = {
  id: string
  monto: number
  metodoPagoId: string
  metodoPagoNombre: string
  metodoPagoCodigo: string
  fecha: string
  referencia: string | null
  esAnticipo: boolean
  ventaId: string | null
  ordenServicioId: string | null
  usuarioId: string | null
  usuarioNombre: string | null
  observaciones: string | null
  activo: boolean
}

export type RegistrarComprobanteRequest = {
  tipo: string
  serie?: string | null
  numero?: string | null
  metodoPagoPrincipal?: string | null
  observaciones?: string | null
}

export type RegistrarPagoRequest = {
  monto: number
  metodoPagoId: string
  referencia?: string | null
  esAnticipo?: boolean
  observaciones?: string | null
}

export type VentaDetalleResponse = {
  id: string
  clienteId: string
  clienteNombre: string
  clienteDocumento: string | null
  clienteTelefono: string | null
  ordenServicioId: string | null
  estado: string
  estadoId: number
  fecha: string
  total: number
  detalles: DetalleVentaResponse[]
  activo: boolean
  comprobante: ComprobanteResponse | null
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  montoIgv: number
  totalPagado: number
  saldo: number
  estadoPago: string
  pagos: PagoResponse[] | null
  estadoComprobante: string
  estadoAprobacionGerenciaId: number
  estadoAprobacionGerencia: string
  numeroOrdenServicio: string | null
}

export type VentaResponse = {
  id: string
  clienteId: string
  clienteNombre: string
  ordenServicioId: string | null
  estado: string
  estadoId: number
  fecha: string
  total: number
  cantidadItems: number
  activo: boolean
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  montoIgv: number
  totalPagado: number
  saldo: number
  estadoPago: string
  estadoComprobante: string
  estadoAprobacionGerenciaId: number
  estadoAprobacionGerencia: string
  numeroOrdenServicio: string | null
}

// ── Yamaha ──────────────────────────────────────────────────────────────────

/** Error = 1 · Advertencia = 2 */
export type SeveridadValidacionYamaha = 1 | 2

export type YamahaConsultaMockResponse = {
  esMock: boolean
  avisoLegal: string
  criterioConsultado: string
  tipoBusqueda: string
  codigoModelo: string
  nombreComercial: string
  anioFabricacion: number | null
  categoria: string
  vinEjemplo: string | null
  numeroMotorEjemplo: string | null
  estadoGarantia: string
  campaniasServicio: string[]
  intervalosMantenimiento: string[]
  especificaciones: YamahaEspecificacionesDto
  fechaConsultaUtc: string
}

export type YamahaEspecificacionesDto = {
  motorTipo: string
  cilindradaCc: number
  potenciaHp: number
  torqueNm: number
  refrigeracion: string
  capacidadTanque: string
  capacidadAceiteMotor: string
  tipoBujiaRecomendada: string
  presionNeumaticos: string
}

export type YamahaHttpRespuestaDto = {
  codigoHttp: number
  esExitoso: boolean
  cuerpoRespuesta: string | null
  mensajeError: string | null
  duracion: string
  payloadHashEnviado: string | null
}

export type YamahaResultadoValidacionLoteDto = {
  servicio: string
  totalRegistros: number
  registrosValidos: number
  registrosConErrores: number
  totalErroresBloqueantes: number
  totalAdvertencias: number
  esLoteValido: boolean
  hallazgos: YamahaValidacionHallazgoDto[]
}

export type YamahaValidacionHallazgoDto = {
  indiceRegistro: number
  identificadorRegistro: string
  campo: string
  codigoRegla: string
  severidad: SeveridadValidacionYamaha
  mensaje: string
}
