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
}

/** Crear y actualizar cliente reciben los mismos campos. */
export type ClienteRequest = {
  nombreCompleto: string
  razonSocial: string | null
  documentoIdentidad: string | null
  telefono: string | null
  email: string | null
  direccion: string | null
  observaciones: string | null
}

export type VehiculoResponse = {
  id: string
  clienteId: string
  clienteNombre: string
  placa: string
  marca: string
  modelo: string
  anio: number | null
  kilometraje: number | null
  color: string | null
  observaciones: string | null
  activo: boolean
}

export type VehiculoRequest = {
  clienteId: string
  placa: string
  marca: string
  modelo: string
  anio: number | null
  kilometraje: number | null
  color: string | null
  observaciones: string | null
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

export type DetalleServicioResponse = {
  id: string
  productoId: string | null
  productoCodigo: string | null
  descripcion: string
  cantidad: number
  precioUnitario: number
  subtotal: number
  esRepuesto: boolean
}

export type OrdenServicioResponse = {
  id: string
  vehiculoId: string
  vehiculoPlaca: string
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
}

export type OrdenServicioDetalleResponse = OrdenServicioResponse & {
  vehiculoAnio: number | null
  vehiculoKilometraje: number | null
  vehiculoColor: string | null
  clienteTelefono: string | null
  clienteDocumentoIdentidad: string | null
  detalles: DetalleServicioResponse[]
  total: number
}

export type AperturaOrdenRequest = {
  vehiculoId: string
  tecnicoAsignadoId: string | null
  observaciones: string | null
}

export type DiagnosticoRequest = {
  diagnostico: string
  tecnicoAsignadoId: string | null
  observaciones: string | null
}

/** Repuesto: productoId y cantidad, el precio lo pone el catálogo.
 *  Mano de obra: descripcion, cantidad y precioUnitario. */
export type AgregarDetalleRequest = {
  productoId: string | null
  descripcion: string | null
  cantidad: number
  precioUnitario: number | null
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
}

export type CategoriaProductoRequest = {
  nombre: string
}

export type ProductoResponse = {
  id: string
  codigo: string
  nombre: string
  descripcion: string | null
  unidad: string
  precioVenta: number
  stockActual: number
  stockMinimo: number
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
  stockInicial: number
  stockMinimo: number
}

/** Al actualizar no se toca el stock: eso va por entradas, salidas o ajustes. */
export type ActualizarProductoRequest = {
  categoriaId: string
  codigo: string
  nombre: string
  descripcion: string | null
  unidad: string | null
  precioVenta: number
  stockMinimo: number
}

export type EntradaRequest = {
  cantidad: number
  motivo: string
}

export type SalidaRequest = {
  cantidad: number
  motivo: string
}

export type AjusteRequest = {
  nuevoStock: number
  motivo: string
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
