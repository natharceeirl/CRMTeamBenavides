// Contratos del backend: src/CRMTeamBenavides.Api/Features/**/*Contracts.cs
// ASP.NET serializa las propiedades en camelCase y los enums como número.

export type SolicitudLogin = {
  email: string
  password: string
}

export type RespuestaLogin = {
  accessToken: string
  accessTokenExpiration: string
  refreshToken: string
  refreshTokenExpiration: string
}

/** GET /api/auth/me */
export type UsuarioActualResponse = {
  id: string
  email: string
  nombreCompleto: string
  activo: boolean
  roles: string[]
  permisos: string[]
  clienteId: string | null
}

/** Nombres exactos de CambiarPasswordRequest en el backend. */
export type CambiarPasswordRequest = {
  passwordActual: string
  passwordNueva: string
}

/** Nombre exacto de ResetPasswordRequest en el backend. */
export type ResetPasswordRequest = {
  nuevaPassword: string
}

export type ClienteResponse = {
  id: string
  nombreCompleto: string
  razonSocial: string | null
  documentoIdentidad: string | null
  tipoDocumento?: string | null
  tipoDocumentoId?: number | null
  numeroDocumento?: string | null
  telefono: string | null
  email: string | null
  direccion: string | null
  observaciones: string | null
  activo: boolean
}

/** Crear y actualizar cliente reciben los mismos campos. */
export type ClienteRequest = {
  nombreCompleto: string
  razonSocial: string | null
  documentoIdentidad: string | null
  tipoDocumento?: number | string | null
  numeroDocumento?: string | null
  telefono: string | null
  email: string | null
  direccion: string | null
  observaciones: string | null
}

/** Enum TipoDocumentoCliente del backend. */
export const TIPO_DOCUMENTO_CLIENTE = { dni: 0, ruc: 1, otro: 2 } as const

/** POST /api/clientes/alta-rapida: si el documento ya está registrado, devuelve ese cliente. */
export type AltaRapidaClienteRequest = {
  numeroDocumento: string
  nombreCompleto: string
  telefono: string | null
  email: string | null
  direccion: string | null
  tipoDocumento: number | null
}

/** POST /api/vehiculos/alta-rapida: una unidad con kilometraje; lo demás se completa en su ficha. */
export type AltaRapidaVehiculoRequest = {
  clienteId: string
  placa: string | null
  marca: string
  modelo: string
  kilometraje: number | null
  color: string | null
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
  tipoUnidad?: string
  tipoUnidadId?: number
  numeroSerieVIN?: string | null
  numeroMotor?: string | null
  tipoMedidor?: string
  tipoMedidorId?: number
  horasUso?: number | null
  valorEstimado?: number | null
  lecturaMedidorActual?: number | null
}

export type VehiculoRequest = {
  clienteId: string
  placa?: string | null
  marca: string
  modelo: string
  anio: number | null
  kilometraje: number | null
  color: string | null
  observaciones: string | null
  tipoUnidad?: number | string
  numeroSerieVIN?: string | null
  numeroMotor?: string | null
  tipoMedidor?: number | string
  horasUso?: number | null
  valorEstimado?: number | null
  lecturaMedidorActual?: number | null
}

export type UsuarioResponse = {
  id: string
  email: string
  nombreCompleto: string
  phoneNumber: string | null
  activo: boolean
  roles: string[]
}

export type CrearUsuarioRequest = {
  email: string
  password: string
  nombreCompleto: string
  phoneNumber: string | null
}

export type ActualizarUsuarioRequest = {
  nombreCompleto: string
  phoneNumber: string | null
}

export type RolResponse = {
  id: string
  nombre: string
  descripcion: string | null
  activo: boolean
}

export type RolRequest = {
  nombre: string
  descripcion: string | null
}

export type PermisoResponse = {
  id: string
  codigo: string
  descripcion: string | null
  activo: boolean
}

export const TIPO_ITEM_SERVICIO = {
  repuesto: 0,
  servicio: 1,
  manoDeObra: 2,
  terceros: 3,
} as const

export const nombresTipoItem: Record<number, string> = {
  0: 'Repuesto',
  1: 'Servicio',
  2: 'Mano de obra',
  3: 'Terceros',
}

export const TIPO_AFECTACION_IGV = {
  gravado: 0,
  exonerado: 1,
  inafecto: 2,
} as const

export const nombresTipoAfectacion: Record<number, string> = {
  0: 'Gravado',
  1: 'Exonerado',
  2: 'Inafecto',
}

export type ServicioResponse = {
  id: string
  nombre: string
  precioSugerido: number
  tipoAfectacionIgv: number
  activo: boolean
  fechaCreacion: string
}

export type CrearServicioRequest = {
  nombre: string
  precioSugerido: number
  tipoAfectacionIgv?: number
}

export type ActualizarServicioRequest = {
  nombre: string
  precioSugerido: number
  tipoAfectacionIgv?: number
  activo?: boolean | null
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
}

/** Lo que no se manda (o va en null) queda como estaba. */
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
  servicioId?: string | null
  servicioNombre?: string | null
  tipoItem?: number
  tipoItemNombre?: string | null
  costoUnitarioHistorico?: number
  tipoAfectacionIgv?: number
  tipoAfectacionIgvNombre?: string | null
  subtotalGravado?: number
  porcentajeIgvAplicado?: number
  montoIgv?: number
  total?: number
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
  numeroOrden?: string | null
  fechaIngreso?: string
  fechaEstimadaEntrega?: string | null
  fechaSalida?: string | null
  motivoFalla?: string | null
  solucion?: string | null
  tipoAtencion?: string
  tipoAtencionId?: number
  modalidadAtencion?: string
  modalidadAtencionId?: number
  tipoFalla?: string | null
  tipoFallaId?: number | null
  kilometrajeIngreso?: number | null
  horasUsoIngreso?: number | null
  lecturaMedidorIngreso?: number | null
  subtotalGravado?: number
  subtotalExonerado?: number
  subtotalInafecto?: number
  montoIgv?: number
  total?: number
  ventaId?: string | null
  comprobanteSerieNumero?: string | null
  /** Suma de adelantos y pagos, también los de la venta que liquidó la orden. */
  totalPagado?: number
  saldo?: number
  /** Pendiente, Parcial o Pagado: lo calcula el backend. */
  estadoPago?: string
  /** Enum EstadoPresupuestoCliente: 0 pendiente, 1 aprobado, 2 rechazado. */
  estadoPresupuestoClienteId?: number
  estadoPresupuestoCliente?: string
  fechaRespuestaCliente?: string | null
  observacionesPresupuestoCliente?: string | null
  /** Enum EstadoAprobacionGerencia: 0 no aplica, 1 pendiente, 2 aprobado, 3 rechazado. */
  estadoAprobacionGerenciaId?: number
  estadoAprobacionGerencia?: string
  fechaAprobacionGerencia?: string | null
  usuarioAprobacionGerenciaId?: string | null
  usuarioAprobacionGerenciaNombre?: string | null
  observacionesAprobacionGerencia?: string | null
}

/** PUT /api/ordenes-servicio/{id}/aprobacion-cliente */
export type RespuestaPresupuestoRequest = {
  estado: number
  observaciones: string | null
}

/** PUT /api/ordenes-servicio/{id}/aprobacion-gerencia */
export type AprobacionGerenciaRequest = {
  estado: number
  observaciones: string | null
}

export type OrdenServicioDetalleResponse = OrdenServicioResponse & {
  vehiculoAnio: number | null
  vehiculoKilometraje: number | null
  vehiculoColor: string | null
  clienteTelefono: string | null
  clienteDocumentoIdentidad: string | null
  detalles: DetalleServicioResponse[]
  total: number
  tipoUnidad?: string | null
  numeroSerieVIN?: string | null
  numeroMotor?: string | null
  historial?: HistorialEstadoOrdenResponse[]
  pagos?: PagoResponse[] | null
}

export type AperturaOrdenRequest = {
  vehiculoId: string
  tecnicoAsignadoId: string | null
  observaciones: string | null
  motivoFalla?: string | null
  fechaEstimadaEntrega?: string | null
  tipoAtencion?: number
  modalidadAtencion?: number
  tipoFalla?: number | null
  kilometrajeIngreso?: number | null
  horasUsoIngreso?: number | null
  lecturaMedidorIngreso?: number | null
}

export type DiagnosticoRequest = {
  diagnostico: string
  tecnicoAsignadoId: string | null
  observaciones: string | null
  solucion?: string | null
  fechaEstimadaEntrega?: string | null
  tipoFalla?: number | null
}

export type ActualizarOrdenRequest = {
  motivoFalla?: string | null
  diagnostico?: string | null
  solucion?: string | null
  observaciones?: string | null
  fechaEstimadaEntrega?: string | null
  tipoAtencion?: number
  modalidadAtencion?: number
  tipoFalla?: number | null
  kilometrajeIngreso?: number | null
  horasUsoIngreso?: number | null
  lecturaMedidorIngreso?: number | null
  tecnicoAsignadoId?: string | null
}

export type AgregarDetalleRequest = {
  productoId?: string | null
  servicioId?: string | null
  tipoItem?: number | null
  descripcion?: string | null
  cantidad: number
  precioUnitario?: number | null
  tipoAfectacionIgv?: number | null
}

/**
 * PUT /api/ordenes-servicio/{id}/detalles/{detalleId}: solo se envía lo que cambia.
 * Un precio distinto al de lista deja la orden pendiente de Gerencia; volver al de lista la libera.
 */
export type ActualizarDetalleRequest = {
  precioUnitario?: number | null
  cantidad?: number | null
  descripcion?: string | null
  tipoAfectacionIgv?: number | null
}

export type AsignarTecnicoRequest = {
  tecnicoId: string
  observaciones?: string | null
}

export type CambiarEstadoRequest = {
  nuevoEstado: number
  observaciones: string | null
}

export type CategoriaProductoResponse = {
  id: string
  nombre: string
  cantidadProductos: number
  activo: boolean
  fechaCreacion: string
  stockMinimoDefault?: number | null
}

export type CategoriaProductoRequest = {
  nombre: string
  stockMinimoDefault?: number | null
}

export type ProductoResponse = {
  id: string
  codigo: string
  nombre: string
  descripcion: string | null
  unidad: string
  precioVenta: number
  costo?: number
  stockActual: number
  stockMinimo?: number | null
  stockMinimoEfectivo?: number
  esBajoStock: boolean
  categoriaId: string
  categoriaNombre: string
  activo: boolean
  fechaCreacion: string
  marca?: string | null
  /** Enlace a una imagen pública del repuesto; la API no recibe archivos. */
  fotoUrl?: string | null
}

/** POST /api/productos/alta-rapida: nombre y precio; código y categoría los completa el backend si faltan. */
export type AltaRapidaProductoRequest = {
  nombre: string
  precioVenta: number
  codigo: string | null
  marca: string | null
  categoriaId: string | null
  stockInicial: number
}

export type CrearProductoRequest = {
  categoriaId: string
  codigo: string
  nombre: string
  descripcion: string | null
  unidad: string | null
  precioVenta: number
  costo?: number
  stockInicial: number
  stockMinimo?: number | null
  marca?: string | null
  fotoUrl?: string | null
}

/** Al actualizar no se toca el stock: eso va por entradas, salidas o ajustes. */
export type ActualizarProductoRequest = {
  categoriaId: string
  codigo: string
  nombre: string
  descripcion: string | null
  unidad: string | null
  precioVenta: number
  costo?: number
  stockMinimo?: number | null
  marca?: string | null
  fotoUrl?: string | null
}

export type EntradaRequest = {
  cantidad: number
  motivo: string
  costoUnitario?: number | null
}

export type SalidaRequest = {
  cantidad: number
  motivo: string
}

export type AjusteRequest = {
  nuevoStock: number
  motivo: string
  costoUnitario?: number | null
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
  costoUnitario?: number | null
}

/** Montos que el backend calcula en ventas, órdenes y comprobantes. */
type DesgloseIgv = {
  subtotalGravado: number
  subtotalExonerado: number
  subtotalInafecto: number
  montoIgv: number
}

/** Pagado, saldo y estado (Pendiente, Parcial o Pagado) de una venta. */
type SituacionPago = {
  totalPagado: number
  saldo: number
  estadoPago: string
}

export type VentaResponse = DesgloseIgv &
  SituacionPago & {
    id: string
    clienteId: string
    clienteNombre: string
    ordenServicioId: string | null
    /** El número de la orden liquidada (OS-000024), o null en una venta de mostrador. */
    numeroOrdenServicio?: string | null
    estado: string
    estadoId: number
    fecha: string
    total: number
    cantidadItems: number
    activo: boolean
  } & EstadosAdicionalesVenta

/** «Pendiente» o «Emitido» para el comprobante; la aprobación usa el enum EstadoAprobacionGerencia. */
type EstadosAdicionalesVenta = {
  estadoComprobante?: string
  estadoAprobacionGerenciaId?: number
  estadoAprobacionGerencia?: string
}

export type DetalleVentaResponse = {
  id: string
  /** Null en los servicios y la mano de obra que vienen de una orden. */
  productoId: string | null
  productoCodigo: string | null
  productoNombre: string
  cantidad: number
  precioUnitario: number
  subtotal: number
  tipoItem?: number
  tipoItemNombre?: string | null
  servicioId?: string | null
  tipoAfectacionIgv?: number
  tipoAfectacionIgvNombre?: string | null
  subtotalGravado?: number
  porcentajeIgvAplicado?: number
  montoIgv?: number
  total?: number
}

export type VentaDetalleResponse = DesgloseIgv &
  SituacionPago & {
    id: string
    clienteId: string
    clienteNombre: string
    clienteDocumento: string | null
    clienteTelefono: string | null
    ordenServicioId: string | null
    numeroOrdenServicio?: string | null
    estado: string
    estadoId: number
    fecha: string
    total: number
    detalles: DetalleVentaResponse[]
    activo: boolean
    comprobante?: ComprobanteResponse | null
    pagos?: PagoResponse[] | null
  } & EstadosAdicionalesVenta

export type ComprobanteResponse = DesgloseIgv & {
  id: string
  ventaId: string
  tipo: string
  serie: string | null
  numero: string | null
  estado: string
  fechaCreacion: string
  activo: boolean
  porcentajeIgv: number
  total: number
  metodoPagoPrincipal: string | null
  observaciones: string | null
  ordenServicioId: string | null
}

export type RegistrarComprobanteRequest = {
  tipo: string
  serie?: string | null
  numero?: string | null
  /** Si no se indica, el backend toma el método del pago más grande. */
  metodoPagoPrincipal?: string | null
  observaciones?: string | null
}

/**
 * Con esCotizacion en false la venta nace confirmada y descuenta stock. Con
 * ordenServicioId y sin detalles liquida la orden: toma sus ítems, no vuelve a
 * descontar stock y se queda con los adelantos ya pagados.
 */
/** Sin precio toma el de lista; con otro precio, la venta queda pendiente de Gerencia. */
export type LineaVentaRequest = { productoId: string; cantidad: number; precioUnitario?: number | null }

export type CrearVentaRequest = {
  clienteId: string
  ordenServicioId: string | null
  detalles: LineaVentaRequest[] | null
  esCotizacion: boolean
}

/** PUT /api/ventas/{id}: solo una cotización sin pagos; reemplaza todas sus líneas. */
export type ActualizarVentaRequest = {
  detalles: LineaVentaRequest[]
  observaciones: string | null
}

/** GET /api/metodos-pago */
export type MetodoPagoResponse = {
  id: string
  codigo: string
  nombre: string
  activo: boolean
}

/** POST /api/ventas/{id}/pagos y /api/ordenes-servicio/{id}/pagos */
export type RegistrarPagoRequest = {
  monto: number
  metodoPagoId: string
  referencia: string | null
  esAnticipo: boolean
  observaciones: string | null
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

export type DashboardResumenResponse = {
  fechaDesde: string | null
  fechaHasta: string | null
  ordenesServicio: {
    abierta: number
    diagnostico: number
    aprobada: number
    enProceso: number
    lista: number
    entregada: number
    cancelada: number
    total: number
  }
  ventas: {
    confirmadas: number
    cotizaciones: number
    anuladas: number
    montoConfirmadas: number
    ticketPromedio: number
  }
  inventario: {
    productosConStockBajo: number
    productosSinStock: number
    valorEstimadoInventario: number
  }
  crmActivos: {
    clientesActivos: number
    vehiculosActivos: number
  }
}

export type OrdenServicioReporteResponse = {
  id: string
  vehiculoId: string
  vehiculoPlaca: string
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
  /** Horas entre apertura y cierre; null si sigue abierta. */
  tiempoAtencionHoras: number | null
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

export type FaqRequest = {
  categoria: string
  pregunta: string
  respuesta: string
  palabrasClave: string | null
  orden: number
  activo?: boolean
}

export type ConsultaChatbotRequest = {
  mensaje: string
  nombreContacto: string | null
  telefonoContacto: string | null
  canal: string | null
}

export type ConsultaChatbotResponse = {
  resueltoPorFaq: boolean
  faq: FaqResponse | null
  sugerencias: FaqResponse[]
  requiereAgente: boolean
  /** Solo si respondió una pregunta frecuente: sin respuesta no se registra consulta. */
  consultaId: string | null
  mensajeRespuesta: string
}

export type SolicitarAgenteRequest = {
  consultaId: string | null
  nombreContacto: string | null
  telefonoContacto: string
  motivo: string
  canal: string | null
}

export type SolicitudAgenteResponse = {
  consultaId: string
  estado: string
  mensaje: string
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

export type ResolverConsultaRequest = {
  estado: number
  notasAgente: string | null
}

// ---------------------------------------------------------------------------
// D7 — FOTOS DE ORDEN DE SERVICIO
// ---------------------------------------------------------------------------
export type EtapaFotoOrdenServicio = 'Ingreso' | 'Diagnostico' | 'Reparacion' | 'Entrega'

export type FotoOrdenServicioResponse = {
  id: string
  ordenServicioId: string
  nombreArchivoOriginal: string
  urlRelativa: string
  contentType: string
  tamanioBytes: number
  etapa: number
  etapaNombre: string
  usuarioId: string
  usuarioNombre: string | null
  observacion: string | null
  fechaCreacion: string
}

// ---------------------------------------------------------------------------
// D7 — FORMATO OFICIAL DE ATENCIÓN
// ---------------------------------------------------------------------------
export type FormatoAtencionTallerDto = {
  nombreTaller: string
  razonSocial: string
  ruc: string | null
  direccion: string | null
  telefono: string | null
  email: string | null
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

export type FormatoAtencionTrabajoDto = {
  motivoFalla: string | null
  diagnostico: string | null
  solucion: string | null
  observaciones: string | null
  tecnicoResponsable: string | null
  tecnicoEmail: string | null
}

export type FormatoAtencionItemDto = {
  id: string
  tipoItem: number
  tipoItemNombre: string
  descripcion: string
  cantidad: number
  precioUnitario: number
  total: number
  afectacionIgv: string
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

// ---------------------------------------------------------------------------
// D7 — YAMAHA MOCK
// ---------------------------------------------------------------------------
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


/** Enums EstadoCajaChica y TipoMovimientoCaja del backend. */
export const ESTADO_CAJA = { abierta: 0, cerrada: 1 } as const
export const TIPO_MOVIMIENTO_CAJA = { ingreso: 0, egreso: 1 } as const

export type MovimientoCajaResponse = {
  id: string
  cajaChicaId: string
  tipo: number
  tipoDescripcion: string
  monto: number
  concepto: string
  referencia: string | null
  fecha: string
  usuarioId: string | null
  usuarioNombre: string | null
  /** Solo en los ingresos que vienen de un cobro: el backend los registra solo. */
  pagoId: string | null
  metodoPagoId: string | null
  metodoPagoNombre: string | null
}

/**
 * GET /api/caja-chica/actual/resumen-metodos y /{id}/resumen-metodos: solo ingresos.
 * Sin caja abierta suma los ingresos del día. `porMetodo` usa el nombre de cada método.
 */
export type ResumenMetodosPagoCajaResponse = {
  cajaChicaId: string | null
  totalEfectivo: number
  totalYapePlin: number
  totalTarjeta: number
  totalTransferencia: number
  porMetodo: Record<string, number>
  totalGeneral: number
}

type DatosCaja = {
  id: string
  montoApertura: number
  montoCierre: number | null
  saldoCalculado: number
  fechaApertura: string
  fechaCierre: string | null
  estado: number
  estadoDescripcion: string
  observacionesApertura: string | null
  observacionesCierre: string | null
  usuarioAperturaId: string | null
  usuarioAperturaNombre: string | null
  usuarioCierreId: string | null
  usuarioCierreNombre: string | null
  /** Todos los métodos. El saldo solo cuenta efectivo: inicial + ingresos en efectivo − egresos. */
  totalIngresos: number
  totalEgresos: number
  totalIngresosEfectivo?: number
  /** Yape, Plin, tarjeta o transferencia: se registran, pero no entran al cajón. */
  totalIngresosOtrosMetodos?: number
}

/** GET /api/caja-chica/historial */
export type CajaChicaResponse = DatosCaja & {
  cantidadMovimientos: number
  fechaCreacion: string
}

export type CajaChicaDetalleResponse = DatosCaja & {
  movimientos: MovimientoCajaResponse[]
}

/** GET /api/caja-chica/actual */
export type EstadoCajaActualResponse = {
  tieneCajaAbierta: boolean
  caja: CajaChicaDetalleResponse | null
}

export type AperturaCajaRequest = {
  montoApertura: number
  observaciones: string | null
}

export type CierreCajaRequest = {
  observaciones: string | null
}

/** POST /api/caja-chica/ingresos y /egresos: el tipo lo pone la ruta. */
export type RegistrarMovimientoCajaRequest = {
  tipo: number
  monto: number
  concepto: string
  referencia: string | null
}

/** GET /api/configuracion/tipo-cambio: el vigente es el valor de venta. */
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

/** PUT /api/configuracion/tipo-cambio: dólar a soles por defecto. */
export type RegistrarTipoCambioRequest = {
  valorCompra: number
  valorVenta: number
  observaciones: string | null
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

/** GET /api/auditoria: el detalle viene como JSON en texto. */
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

// ---------------------------------------------------------------------------
// Citas / agenda del taller
// ---------------------------------------------------------------------------
/**
 * Enum EstadoCita del backend. Viaja como texto (JsonStringEnumConverter en el
 * enum), no como número. Cancelada, Completada y NoAsistio son finales.
 */
export const ESTADO_CITA = {
  pendiente: 'Pendiente',
  confirmada: 'Confirmada',
  enTaller: 'EnTaller',
  completada: 'Completada',
  cancelada: 'Cancelada',
  noAsistio: 'NoAsistio',
} as const

export type EstadoCita = (typeof ESTADO_CITA)[keyof typeof ESTADO_CITA]

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

export type CrearCitaRequest = {
  clienteId: string | null
  vehiculoId: string
  fechaHoraProgramada: string
  duracionMinutos: number | null
  motivo: string
  observaciones: string | null
}

/** PUT /api/citas/{id}: la fecha se cambia con Reprogramar, que deja la nota en el historial. */
export type ActualizarCitaRequest = {
  vehiculoId: string
  fechaHoraProgramada: string
  duracionMinutos: number | null
  motivo: string
  observaciones: string | null
}

export type ReprogramarCitaRequest = {
  nuevaFechaHoraProgramada: string
  nuevaDuracionMinutos: number | null
  motivoReprogramacion: string | null
}

// ---------------------------------------------------------------------------
// Pedidos de Lima: repuestos que un cliente encarga y llegan de Lima
// ---------------------------------------------------------------------------
/**
 * Enum EstadoPedidoLima del backend, en orden: no se retrocede. Viaja como texto
 * (JsonStringEnumConverter en el enum), no como número.
 */
export const ESTADO_PEDIDO_LIMA = {
  pendiente: 'Pendiente',
  confirmado: 'Confirmado',
  enPreparacion: 'EnPreparacion',
  enTransito: 'EnTransito',
  recibido: 'Recibido',
  entregado: 'Entregado',
  cancelado: 'Cancelado',
} as const

export type EstadoPedidoLima = (typeof ESTADO_PEDIDO_LIMA)[keyof typeof ESTADO_PEDIDO_LIMA]

export type DetallePedidoLimaResponse = {
  id: string
  productoId: string
  productoCodigo: string
  productoNombre: string
  cantidad: number
  precioUnitario: number
  costoUnitarioHistorico: number
  /** Viaja como texto: «Gravado», «Exonerado» o «Inafecto». */
  tipoAfectacionIgv: string
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
  /** Adelantos y pagos: cada uno entra solo a la caja abierta. */
  totalPagado?: number
  saldo?: number
  estadoPago?: string
  estadoAprobacionGerenciaId?: number
  estadoAprobacionGerencia?: string
  pagos?: PagoResponse[] | null
}

/** PUT /api/pedidos-lima/{id}/detalles/{detalleId}: otro precio que el de lista pide aprobación de Gerencia. */
export type ActualizarPrecioDetallePedidoLimaRequest = {
  precioUnitario: number
  cantidad: number | null
}

export type CrearPedidoLimaRequest = {
  clienteId: string
  empresaTransporte: string | null
  numeroGuia: string | null
  fechaEstimadaLlegada: string | null
  observaciones: string | null
  detalles: { productoId: string; cantidad: number }[]
}

export type ActualizarPedidoLimaRequest = {
  empresaTransporte: string | null
  numeroGuia: string | null
  fechaEstimadaLlegada: string | null
  fechaLlegada: string | null
  fechaEntrega: string | null
  observaciones: string | null
}

// ---------------------------------------------------------------------------
// Rentabilidad (solo con reportes.ver_financieros)
// ---------------------------------------------------------------------------
export type RentabilidadReporteResponse = {
  fechaDesde: string | null
  fechaHasta: string | null
  resumen: {
    ingresosTotalesSinIgv: number
    costoTotalHistorico: number
    utilidadBrutaTotal: number
    margenPorcentualGlobal: number
  }
  desglosePorTipo: {
    tipoItem: string
    cantidadItems: number
    ingresoNeto: number
    costoHistoricoRegistrado: number
    utilidadBruta: number
    margenPorcentual: number
  }[]
  detalleOperaciones: {
    documentoTipo: string
    numeroDocumento: string
    operacionId: string
    fecha: string
    clienteNombre: string
    ingresoNeto: number
    costoHistoricoRegistrado: number
    utilidadBruta: number
    margenPorcentual: number
  }[]
  rankingRepuestos: {
    productoId: string
    codigo: string
    descripcion: string
    unidadesVendidas: number
    ingresoNeto: number
    costoHistorico: number
    utilidadBruta: number
    margenPorcentual: number
  }[]
}
