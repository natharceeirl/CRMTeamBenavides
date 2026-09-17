// Contratos del backend: src/CRMTeamBenavides.Api/Features/**/*Contracts.cs
// ASP.NET serializa las propiedades en camelCase.

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
