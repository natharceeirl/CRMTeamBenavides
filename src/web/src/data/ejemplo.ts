// Datos de ejemplo para los wireframes. No son datos reales de Team Benavides.

export const hoy = 'Martes 15 de septiembre'

export const usuarioActual = { nombre: 'Milagros Torres', rol: 'Asesora de servicio' }

// Estados propuestos en el mapa funcional; pendientes de validar con el cliente.
export type EstadoOrden =
  | 'recepcion'
  | 'diagnostico'
  | 'esperando_aprobacion'
  | 'en_reparacion'
  | 'esperando_repuesto'
  | 'control_calidad'
  | 'lista_entrega'
  | 'entregada'
  | 'anulada'

export const estadosOrden: Record<EstadoOrden, string> = {
  recepcion: 'Recepción',
  diagnostico: 'Diagnóstico',
  esperando_aprobacion: 'Esperando aprobación',
  en_reparacion: 'En reparación',
  esperando_repuesto: 'Esperando repuesto',
  control_calidad: 'Control de calidad',
  lista_entrega: 'Lista para entrega',
  entregada: 'Entregada',
  anulada: 'Anulada',
}

export const estadosEnTaller: EstadoOrden[] = [
  'recepcion',
  'diagnostico',
  'esperando_aprobacion',
  'en_reparacion',
  'control_calidad',
]

export type TipoUnidad = 'motocicleta' | 'cuatrimoto' | 'moto_acuatica' | 'linea_fuerza'

export const tiposUnidad: Record<TipoUnidad, string> = {
  motocicleta: 'Motocicleta',
  cuatrimoto: 'Cuatrimoto',
  moto_acuatica: 'Moto acuática',
  linea_fuerza: 'Línea de fuerza',
}

export type Cliente = {
  id: string
  tipoDocumento: 'DNI' | 'RUC' | 'CE'
  documento: string
  nombre: string
  telefono: string
  correo?: string
  direccion?: string
}

export type Unidad = {
  id: string
  clienteId: string
  tipo: TipoUnidad
  marca: string
  modelo: string
  anio: number
  serie: string
  numeroMotor: string
  placa?: string
  medidor: number
  unidadMedidor: 'km' | 'h'
}

export type LineaOrden = {
  concepto: string
  tipo: 'Mano de obra' | 'Repuesto'
  cantidad: number
  unidad?: string
  precioUnitario: number
}

export type Orden = {
  numero: string
  unidadId: string
  clienteId: string
  motivo: string
  tecnico: string
  estado: EstadoOrden
  ingreso: string
  entrega: string
  total: number
  lineas?: LineaOrden[]
  bitacora?: { evento: string; fecha: string }[]
  nota?: string
}

export type Producto = {
  codigo: string
  codigoYamaha: string
  nombre: string
  categoria: string
  unidad: string
  stock: number
  stockMinimo: number
  precio: number
}

export type Documento = {
  numero: string
  tipo: 'Cotización' | 'Venta'
  clienteId: string
  fecha: string
  estado: 'Enviada' | 'Aceptada' | 'Vencida' | 'Pagada' | 'Anulada'
  total: number
}

export type Usuario = { nombre: string; correo: string; rol: string; activo: boolean }

export const clientes: Cliente[] = [
  { id: 'c1', tipoDocumento: 'DNI', documento: '45872310', nombre: 'Luis Quispe Mamani', telefono: '959 214 380', correo: 'luis.quispe@ejemplo.pe', direccion: 'Cayma, Arequipa' },
  { id: 'c2', tipoDocumento: 'DNI', documento: '70125564', nombre: 'Carla Zegarra Paredes', telefono: '951 663 207', direccion: 'Yanahuara, Arequipa' },
  { id: 'c3', tipoDocumento: 'RUC', documento: '20601234567', nombre: 'Agroindustrias del Sur S.A.C.', telefono: '054 281 440', correo: 'compras@ejemplo.pe', direccion: 'Cerro Colorado, Arequipa' },
  { id: 'c4', tipoDocumento: 'DNI', documento: '42190856', nombre: 'Jorge Villanueva Cáceres', telefono: '974 552 118', direccion: 'Paucarpata, Arequipa' },
  { id: 'c5', tipoDocumento: 'RUC', documento: '20458812390', nombre: 'Náutica Mollendo E.I.R.L.', telefono: '054 532 671', direccion: 'Mollendo, Islay' },
  { id: 'c6', tipoDocumento: 'DNI', documento: '29745103', nombre: 'Rosa Apaza Huanca', telefono: '958 330 942', direccion: 'Socabaya, Arequipa' },
]

export const unidades: Unidad[] = [
  { id: 'u1', clienteId: 'c1', tipo: 'motocicleta', marca: 'Yamaha', modelo: 'MT-03', anio: 2023, serie: 'ME1RH0710P0012345', numeroMotor: 'H405E-012345', placa: '4821-3A', medidor: 12480, unidadMedidor: 'km' },
  { id: 'u2', clienteId: 'c3', tipo: 'cuatrimoto', marca: 'Yamaha', modelo: 'Grizzly 700', anio: 2022, serie: '5Y4AM76Y2NA104522', numeroMotor: 'M701E-104522', medidor: 3210, unidadMedidor: 'km' },
  { id: 'u3', clienteId: 'c5', tipo: 'moto_acuatica', marca: 'Yamaha', modelo: 'WaveRunner VX Cruiser', anio: 2021, serie: 'YAMA3456L121', numeroMotor: '6EX-1034221', medidor: 146, unidadMedidor: 'h' },
  { id: 'u4', clienteId: 'c3', tipo: 'linea_fuerza', marca: 'Yamaha', modelo: 'EF2000iS', anio: 2022, serie: '7C4-1128830', numeroMotor: 'MZ80-1128830', medidor: 820, unidadMedidor: 'h' },
  { id: 'u5', clienteId: 'c2', tipo: 'motocicleta', marca: 'Yamaha', modelo: 'NMAX 155', anio: 2024, serie: 'MH3SG6310RJ048812', numeroMotor: 'G3L8E-048812', placa: '7310-9B', medidor: 5640, unidadMedidor: 'km' },
  { id: 'u6', clienteId: 'c4', tipo: 'motocicleta', marca: 'Yamaha', modelo: 'XTZ 250', anio: 2020, serie: '9C6DG2510L0028841', numeroMotor: 'G3F2E-028841', placa: '2965-4C', medidor: 38210, unidadMedidor: 'km' },
  { id: 'u7', clienteId: 'c6', tipo: 'motocicleta', marca: 'Yamaha', modelo: 'FZ25', anio: 2022, serie: 'MH3RG4610NK031177', numeroMotor: 'G3E4E-031177', placa: '5518-7D', medidor: 21030, unidadMedidor: 'km' },
]

export const ordenes: Orden[] = [
  {
    numero: 'OT-000482',
    unidadId: 'u1',
    clienteId: 'c1',
    motivo: 'Mantenimiento de 12 000 km y ruido en frenos',
    tecnico: 'Jorge Huamán',
    estado: 'esperando_aprobacion',
    ingreso: '15/09 08:20',
    entrega: '16/09 17:00',
    total: 486,
    lineas: [
      { concepto: 'Mantenimiento de 12 000 km', tipo: 'Mano de obra', cantidad: 1, precioUnitario: 120 },
      { concepto: 'Aceite Yamalube 10W-40 1 L', tipo: 'Repuesto', cantidad: 3, unidad: 'L', precioUnitario: 42 },
      { concepto: 'Filtro de aceite MT-03 / R3', tipo: 'Repuesto', cantidad: 1, precioUnitario: 38 },
      { concepto: 'Pastillas de freno delanteras', tipo: 'Repuesto', cantidad: 1, unidad: 'jgo.', precioUnitario: 165 },
      { concepto: 'Revisión de frenos', tipo: 'Mano de obra', cantidad: 1, precioUnitario: 37 },
    ],
    bitacora: [
      { evento: 'Presupuesto enviado al cliente', fecha: '15/09 11:40' },
      { evento: 'Diagnóstico registrado', fecha: '15/09 10:15' },
      { evento: 'Técnico asignado', fecha: '15/09 08:35' },
      { evento: 'Unidad recibida', fecha: '15/09 08:20' },
    ],
    nota: 'El cliente pidió que lo llamen antes de cambiar las pastillas. Revisar también la tensión de la cadena.',
  },
  { numero: 'OT-000481', unidadId: 'u5', clienteId: 'c2', motivo: 'Cambio de pastillas y revisión de CVT', tecnico: 'Ricardo Ccori', estado: 'en_reparacion', ingreso: '14/09 16:10', entrega: '15/09 18:00', total: 312 },
  { numero: 'OT-000480', unidadId: 'u2', clienteId: 'c3', motivo: 'Mantenimiento de 3 000 km', tecnico: 'Jorge Huamán', estado: 'esperando_repuesto', ingreso: '13/09 09:00', entrega: '17/09 12:00', total: 845 },
  { numero: 'OT-000479', unidadId: 'u3', clienteId: 'c5', motivo: 'Servicio de 150 horas', tecnico: 'Ricardo Ccori', estado: 'control_calidad', ingreso: '12/09 10:30', entrega: '15/09 16:00', total: 1320 },
  { numero: 'OT-000478', unidadId: 'u6', clienteId: 'c4', motivo: 'Cambio de kit de arrastre', tecnico: 'Jorge Huamán', estado: 'lista_entrega', ingreso: '12/09 15:45', entrega: '15/09 10:00', total: 578 },
  { numero: 'OT-000477', unidadId: 'u4', clienteId: 'c3', motivo: 'Mantenimiento de 800 horas', tecnico: 'Ricardo Ccori', estado: 'diagnostico', ingreso: '15/09 09:10', entrega: '18/09 12:00', total: 0 },
  { numero: 'OT-000476', unidadId: 'u7', clienteId: 'c6', motivo: 'Revisión del sistema eléctrico', tecnico: 'Jorge Huamán', estado: 'recepcion', ingreso: '15/09 10:40', entrega: '16/09 18:00', total: 0 },
  { numero: 'OT-000475', unidadId: 'u5', clienteId: 'c2', motivo: 'Mantenimiento de 5 000 km', tecnico: 'Ricardo Ccori', estado: 'entregada', ingreso: '10/09 08:50', entrega: '11/09 17:30', total: 265 },
  { numero: 'OT-000474', unidadId: 'u6', clienteId: 'c4', motivo: 'Diagnóstico de arranque', tecnico: 'Jorge Huamán', estado: 'anulada', ingreso: '09/09 11:20', entrega: '—', total: 0 },
]

export const productos: Producto[] = [
  { codigo: 'REP-0001', codigoYamaha: '90793-AT414', nombre: 'Aceite Yamalube 10W-40 1 L', categoria: 'Lubricantes', unidad: 'Botella', stock: 48, stockMinimo: 20, precio: 42 },
  { codigo: 'REP-0002', codigoYamaha: '1WD-E3440-00', nombre: 'Filtro de aceite MT-03 / R3', categoria: 'Filtros', unidad: 'Unidad', stock: 6, stockMinimo: 8, precio: 38 },
  { codigo: 'REP-0003', codigoYamaha: 'B04-W0045-00', nombre: 'Pastillas de freno delanteras', categoria: 'Frenos', unidad: 'Juego', stock: 3, stockMinimo: 4, precio: 165 },
  { codigo: 'REP-0004', codigoYamaha: '94702-00292', nombre: 'Bujía CPR8EA-9', categoria: 'Encendido', unidad: 'Unidad', stock: 25, stockMinimo: 10, precio: 28 },
  { codigo: 'REP-0005', codigoYamaha: '1WD-F5450-00', nombre: 'Kit de arrastre MT-03', categoria: 'Transmisión', unidad: 'Kit', stock: 2, stockMinimo: 2, precio: 410 },
  { codigo: 'REP-0006', codigoYamaha: '5GH-14451-00', nombre: 'Filtro de aire Grizzly 700', categoria: 'Filtros', unidad: 'Unidad', stock: 0, stockMinimo: 2, precio: 96 },
  { codigo: 'REP-0007', codigoYamaha: 'B6H-E7641-00', nombre: 'Correa CVT NMAX 155', categoria: 'Transmisión', unidad: 'Unidad', stock: 5, stockMinimo: 3, precio: 185 },
  { codigo: 'REP-0008', codigoYamaha: '90790-BS218', nombre: 'Aceite Yamalube náutico 4T 1 L', categoria: 'Lubricantes', unidad: 'Botella', stock: 12, stockMinimo: 6, precio: 55 },
]

export const documentosVenta: Documento[] = [
  { numero: 'COT-000118', tipo: 'Cotización', clienteId: 'c3', fecha: '15/09', estado: 'Enviada', total: 1240 },
  { numero: 'VEN-000532', tipo: 'Venta', clienteId: 'c6', fecha: '15/09', estado: 'Pagada', total: 84 },
  { numero: 'VEN-000531', tipo: 'Venta', clienteId: 'c1', fecha: '15/09', estado: 'Pagada', total: 126 },
  { numero: 'COT-000117', tipo: 'Cotización', clienteId: 'c5', fecha: '14/09', estado: 'Aceptada', total: 2380 },
  { numero: 'COT-000116', tipo: 'Cotización', clienteId: 'c4', fecha: '12/09', estado: 'Vencida', total: 410 },
  { numero: 'VEN-000530', tipo: 'Venta', clienteId: 'c2', fecha: '11/09', estado: 'Pagada', total: 265 },
  { numero: 'VEN-000529', tipo: 'Venta', clienteId: 'c3', fecha: '10/09', estado: 'Anulada', total: 96 },
]

export const usuarios: Usuario[] = [
  { nombre: 'Carlos Mendoza', correo: 'cmendoza@ejemplo.pe', rol: 'Administrador', activo: true },
  { nombre: 'Sofía Linares', correo: 'slinares@ejemplo.pe', rol: 'Gerencia', activo: true },
  { nombre: 'Milagros Torres', correo: 'mtorres@ejemplo.pe', rol: 'Asesor de servicio', activo: true },
  { nombre: 'Jorge Huamán', correo: 'jhuaman@ejemplo.pe', rol: 'Técnico', activo: true },
  { nombre: 'Ricardo Ccori', correo: 'rccori@ejemplo.pe', rol: 'Técnico', activo: true },
  { nombre: 'Diego Chávez', correo: 'dchavez@ejemplo.pe', rol: 'Almacén', activo: true },
  { nombre: 'Andrea Rojas', correo: 'arojas@ejemplo.pe', rol: 'Vendedor / caja', activo: false },
]

export type NivelPermiso = 'Todo' | 'Ver' | '—'

export const roles = ['Administrador', 'Gerencia', 'Asesor de servicio', 'Técnico', 'Almacén', 'Vendedor / caja'] as const

// Propuesta de matriz; la definitiva la entrega el cliente.
export const matrizPermisos: { modulo: string; permisos: NivelPermiso[] }[] = [
  { modulo: 'Tablero', permisos: ['Todo', 'Ver', 'Ver', 'Ver', 'Ver', 'Ver'] },
  { modulo: 'Órdenes de servicio', permisos: ['Todo', 'Ver', 'Todo', 'Todo', 'Ver', '—'] },
  { modulo: 'Clientes y unidades', permisos: ['Todo', 'Ver', 'Todo', 'Ver', '—', 'Todo'] },
  { modulo: 'Repuestos', permisos: ['Todo', 'Ver', 'Ver', 'Ver', 'Todo', 'Ver'] },
  { modulo: 'Ventas y comprobantes', permisos: ['Todo', 'Ver', 'Ver', '—', '—', 'Todo'] },
  { modulo: 'Reportes', permisos: ['Todo', 'Todo', '—', '—', '—', '—'] },
  { modulo: 'Usuarios y configuración', permisos: ['Todo', '—', '—', '—', '—', '—'] },
]

export const ventasPorDia = [
  { dia: '09/09', total: 1850 },
  { dia: '10/09', total: 2240 },
  { dia: '11/09', total: 1325 },
  { dia: '12/09', total: 2980 },
  { dia: '13/09', total: 1460 },
  { dia: '14/09', total: 2105 },
  { dia: '15/09', total: 210 },
]

export const serviciosFrecuentes = [
  { servicio: 'Mantenimiento periódico', cantidad: 18 },
  { servicio: 'Frenos', cantidad: 9 },
  { servicio: 'Transmisión y kit de arrastre', cantidad: 6 },
  { servicio: 'Sistema eléctrico', cantidad: 4 },
  { servicio: 'Servicio náutico por horas', cantidad: 3 },
]

export const buscarCliente = (id: string) => clientes.find((c) => c.id === id)
export const buscarUnidad = (id: string) => unidades.find((u) => u.id === id)
export const nombreUnidad = (u: Unidad) => `${u.marca} ${u.modelo} ${u.anio}`
export const identificadorUnidad = (u: Unidad) => u.placa ?? u.serie
