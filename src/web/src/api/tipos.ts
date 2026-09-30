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
  ruc: string | null
  porcentajeIgv: number
}

export type ActualizarConfiguracionEmpresaRequest = {
  nombreEmpresa: string
  ruc: string | null
  porcentajeIgv: number
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
}

export type DetalleVentaResponse = {
  id: string
  productoId: string
  productoCodigo: string
  productoNombre: string
  cantidad: number
  precioUnitario: number
  subtotal: number
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
  comprobante?: ComprobanteResponse | null
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
}

export type RegistrarComprobanteRequest = {
  tipo: string
  serie?: string | null
  numero?: string | null
}

/** Con esCotizacion en false la venta nace confirmada y descuenta stock. */
export type CrearVentaRequest = {
  clienteId: string
  ordenServicioId: string | null
  detalles: { productoId: string; cantidad: number }[]
  esCotizacion: boolean
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

