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
