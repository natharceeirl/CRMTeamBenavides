/** Nombres legibles de lo que registra el backend (EventoAuditoria.Entidad). */
export const nombresEntidad: Record<string, string> = {
  CajaChica: 'Caja chica',
  Inventario: 'Inventario',
  MovimientoCajaChica: 'Movimiento de caja',
  TipoCambio: 'Tipo de cambio',
  ConfiguracionEmpresa: 'Configuración',
  OrdenServicio: 'Orden de servicio',
  Venta: 'Venta',
  Comprobante: 'Comprobante',
  Pago: 'Pago',
  Cliente: 'Cliente',
  Vehiculo: 'Unidad',
  Producto: 'Repuesto',
  Usuario: 'Usuario',
}

export const nombreEntidad = (entidad: string) => nombresEntidad[entidad] ?? entidad

/** Acciones que registra el backend (EventoAuditoria.Accion), con su nombre legible. */
export const nombresAccion: Record<string, string> = {
  Crear: 'Crear',
  Actualizar: 'Actualizar',
  CambioEstado: 'Cambio de estado',
  Anular: 'Anular',
  Ajuste: 'Ajuste de stock',
  PresupuestoCliente: 'Respuesta al presupuesto',
  SolicitarAprobacionGerencia: 'Solicitud a Gerencia',
  AprobacionGerencia: 'Decisión de Gerencia',
  Apertura: 'Apertura',
  Cierre: 'Cierre',
  IngresoCaja: 'Ingreso de caja',
  EgresoCaja: 'Egreso de caja',
}

export const nombreAccion = (accion: string) => nombresAccion[accion] ?? accion

/** «MontoApertura» → «Monto apertura». */
export function etiquetaDeCampo(clave: string): string {
  const palabras = clave.replaceAll(/([a-záéíóúñ0-9])([A-ZÁÉÍÓÚÑ])/g, '$1 $2').toLowerCase()
  return palabras.charAt(0).toUpperCase() + palabras.slice(1)
}

/**
 * El detalle llega como JSON en texto. Se muestra como pares campo y valor;
 * si no es un objeto JSON, tal cual.
 */
export function camposDelDetalle(detalle: string | null): { campo: string; valor: string }[] | string | null {
  if (!detalle?.trim()) return null
  try {
    const datos: unknown = JSON.parse(detalle)
    if (datos && typeof datos === 'object' && !Array.isArray(datos)) {
      return Object.entries(datos as Record<string, unknown>).map(([clave, valor]) => ({
        campo: etiquetaDeCampo(clave),
        valor: valor === null || valor === undefined ? '—' : typeof valor === 'object' ? JSON.stringify(valor) : String(valor),
      }))
    }
    return detalle
  } catch {
    return detalle
  }
}

const texto = (valor: unknown) =>
  valor === null || valor === undefined || valor === '' ? '—' : typeof valor === 'object' ? JSON.stringify(valor) : String(valor)

/**
 * Si el detalle trae «ValoresAnteriores» y «ValoresNuevos», devuelve solo los
 * campos que cambiaron, con su valor antes y después. Si no, null.
 */
export function cambiosDelDetalle(detalle: string | null): { campo: string; antes: string; despues: string }[] | null {
  if (!detalle?.trim()) return null
  try {
    const datos = JSON.parse(detalle) as Record<string, unknown>
    const antes = datos?.ValoresAnteriores as Record<string, unknown> | undefined
    const despues = datos?.ValoresNuevos as Record<string, unknown> | undefined
    if (!antes || !despues || typeof antes !== 'object' || typeof despues !== 'object') return null
    const campos = [...new Set([...Object.keys(antes), ...Object.keys(despues)])]
    return campos
      .filter((campo) => JSON.stringify(antes[campo] ?? null) !== JSON.stringify(despues[campo] ?? null))
      .map((campo) => ({ campo: etiquetaDeCampo(campo), antes: texto(antes[campo]), despues: texto(despues[campo]) }))
  } catch {
    return null
  }
}
