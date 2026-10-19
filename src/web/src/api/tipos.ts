// Tipos de la web para hablar con la API.
//
// Los contratos de la API (Features/**/*Contracts.cs) se generan en ./generado/contratos.ts con
// tools/GeneradorTipos (`npm run tipos:generar`) y se reexportan desde aquí: si el back cambia un
// contrato, la web deja de compilar donde haga falta. En este archivo quedan solo los tipos propios
// de la web. ASP.NET serializa en camelCase y los enums como número, salvo los marcados como texto.
export type * from './generado/contratos'
import type {
  CreateFaqRequest,
  ActualizarDetalleServicioRequest,
  ActualizarOrdenServicioRequest,
  AgregarDetalleServicioRequest,
  AperturaOrdenServicioRequest,
  CambiarEstadoOrdenServicioRequest,
  CreateCategoriaProductoRequest,
  CreateClienteRequest,
  CreateProductoRequest,
  CreateRolRequest,
  CreateUsuarioRequest,
  CreateVehiculoRequest,
  CreateVentaRequest,
  ErrorFilaImportacionDto,
  HistorialImportacionItemDto,
  LoginRequest,
  LoginResponse,
  MeResponse,
  RegistrarAjusteRequest,
  RegistrarDiagnosticoRequest,
  RegistrarEntradaRequest,
  RegistrarSalidaRequest,
  ResolverSolicitudAprobacionRequest,
  ResultadoImportacionExcelDto,
  ResumenHojaImportacionDto,
  UpdateFaqRequest,
  UpdateProductoRequest,
  UpdateUsuarioRequest,
} from './generado/contratos'

export type SolicitudLogin = LoginRequest

export type RespuestaLogin = LoginResponse

/** GET /api/auth/me */
export type UsuarioActualResponse = MeResponse

/** Crear y actualizar cliente reciben los mismos campos. */
export type ClienteRequest = CreateClienteRequest

/** Enum TipoDocumentoCliente del backend. */
export const TIPO_DOCUMENTO_CLIENTE = { dni: 0, ruc: 1, otro: 2 } as const

export type VehiculoRequest = CreateVehiculoRequest

export type CrearUsuarioRequest = CreateUsuarioRequest

export type ActualizarUsuarioRequest = UpdateUsuarioRequest

export type RolRequest = CreateRolRequest

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

/** PUT /api/ordenes-servicio/{id}/aprobacion-cliente */
export type RespuestaPresupuestoRequest = ResolverSolicitudAprobacionRequest

/**
 * POST /api/ordenes-servicio. La unidad es una registrada (vehiculoId) o una
 * nueva (unidadNueva) de un cliente registrado (clienteId) o nuevo
 * (clienteNuevo): lo nuevo se crea junto con la orden, todo o nada.
 */
export type AperturaOrdenRequest = AperturaOrdenServicioRequest

export type DiagnosticoRequest = RegistrarDiagnosticoRequest

export type ActualizarOrdenRequest = ActualizarOrdenServicioRequest

export type AgregarDetalleRequest = AgregarDetalleServicioRequest

/**
 * PUT /api/ordenes-servicio/{id}/detalles/{detalleId}: solo se envía lo que cambia.
 * Un precio distinto al de lista deja la orden pendiente de Gerencia; volver al de lista la libera.
 */
export type ActualizarDetalleRequest = ActualizarDetalleServicioRequest

export type CambiarEstadoRequest = CambiarEstadoOrdenServicioRequest

export type CategoriaProductoRequest = CreateCategoriaProductoRequest

export type CrearProductoRequest = CreateProductoRequest

/** Al actualizar no se toca el stock: eso va por entradas, salidas o ajustes. */
export type ActualizarProductoRequest = UpdateProductoRequest

export type EntradaRequest = RegistrarEntradaRequest

export type SalidaRequest = RegistrarSalidaRequest

export type AjusteRequest = RegistrarAjusteRequest

/** Sin precio toma el de lista; con otro precio, la venta queda pendiente de Gerencia. */
export type LineaVentaRequest = { productoId: string; cantidad: number; precioUnitario?: number | null }

/**
 * Con esCotizacion en false la venta nace confirmada y descuenta stock. Con
 * ordenServicioId y sin detalles liquida la orden: toma sus ítems, no vuelve a
 * descontar stock y se queda con los adelantos ya pagados.
 */
export type CrearVentaRequest = CreateVentaRequest

export type FaqRequest = CreateFaqRequest | UpdateFaqRequest

// ---------------------------------------------------------------------------
// D7 — YAMAHA MOCK
// ---------------------------------------------------------------------------
/** Enums EstadoCajaChica y TipoMovimientoCaja del backend. */
export const ESTADO_CAJA = { abierta: 0, cerrada: 1 } as const
export const TIPO_MOVIMIENTO_CAJA = { ingreso: 0, egreso: 1 } as const

/** PUT /api/configuracion/tipo-cambio: dólar a soles por defecto. */
export type RegistrarTipoCambioRequest = {
  valorCompra: number
  valorVenta: number
  observaciones: string | null
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

// ---------------------------------------------------------------------------
// Compras a proveedores: las reglas están en docs/modulo-compras.md
// ---------------------------------------------------------------------------
/** Enum TipoComprobanteCompra. Solo la factura da crédito fiscal: con otro comprobante el IGV es costo. */
export const TIPO_COMPROBANTE_COMPRA = {
  factura: 0,
  boleta: 1,
  ticket: 2,
  notaVenta: 3,
  reciboHonorarios: 4,
  otro: 5,
} as const

export const MONEDA_COMPRA = { pen: 0, usd: 1 } as const
export const ESTADO_COMPRA = { registrada: 0, anulada: 1 } as const
export const METODO_COSTEO = { promedioPonderado: 0, ultimoCosto: 1 } as const
// ---------------------------------------------------------------------------
// Notificaciones: bandeja de cada usuario (y, en la app, avisos al teléfono)
// ---------------------------------------------------------------------------
/** Enum TipoNotificacion del backend. */
export const TIPO_NOTIFICACION = {
  solicitudAprobacion: 0,
  presupuestoEnviado: 1,
  ordenLista: 2,
  pedidoLimaRecibido: 3,
  citaConfirmada: 4,
  ordenAsignada: 5,
} as const

// ---------------------------------------------------------------------------
// Importación de Excel: clientes, unidades y repuestos (Features/Importaciones)
// ---------------------------------------------------------------------------
/** Un problema del archivo. `fila` 0 es del archivo o de la hoja entera, no de una fila. */
export type ErrorFilaImportacion = ErrorFilaImportacionDto

export type ResumenHojaImportacion = ResumenHojaImportacionDto

/** Qué hacer con un documento, una unidad o un código que ya está registrado. */
export const MODO_DUPLICADOS = { error: 'ErrorSiExiste', omitir: 'OmitirExistentes' } as const
export type ModoDuplicados = (typeof MODO_DUPLICADOS)[keyof typeof MODO_DUPLICADOS]

/**
 * POST /api/importaciones/validar y /ejecutar. Con un solo error no se importa
 * nada: `exitoso` es false y los resúmenes quedan con 0 creados.
 * `catalogo` y `repuestos` traen el mismo resumen: la hoja Repuestos más la hoja
 * Catalogo de la plantilla anterior. Un error del archivo puede llegar sin `unidades` ni `repuestos`.
 */
export type ResultadoImportacionExcel = ResultadoImportacionExcelDto

/** GET /api/importaciones/historial. Solo guarda los intentos de importar, no las validaciones. */
export type HistorialImportacionItem = HistorialImportacionItemDto
