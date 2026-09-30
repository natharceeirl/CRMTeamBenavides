import { describe, expect, it } from 'vitest'
import type { VentaDetalleResponse } from '../api/tipos'
import { tieneSaldo } from '../api/pagos'
import { htmlComprobante } from './comprobante'
import { escaparHtml } from './impresion'

const venta = (extra: Partial<VentaDetalleResponse> = {}): VentaDetalleResponse => ({
  id: 'dfe3a46b-0000-0000-0000-000000000001',
  clienteId: 'c1',
  clienteNombre: 'Luis Quispe',
  clienteDocumento: '40123456',
  clienteTelefono: '959214380',
  ordenServicioId: '4362fb56-0000-0000-0000-000000000001',
  estado: 'Confirmada',
  estadoId: 1,
  fecha: '2026-09-30T17:41:00Z',
  total: 49.56,
  subtotalGravado: 42,
  subtotalExonerado: 0,
  subtotalInafecto: 0,
  montoIgv: 7.56,
  totalPagado: 20,
  saldo: 29.56,
  estadoPago: 'Parcial',
  activo: true,
  detalles: [
    {
      id: 'd1',
      productoId: 'p1',
      productoCodigo: 'LUB-1040-1L',
      productoNombre: 'Yamalube 10W-40',
      cantidad: 1,
      precioUnitario: 42,
      subtotal: 42,
      tipoItem: 0,
      montoIgv: 7.56,
      total: 49.56,
    },
  ],
  comprobante: {
    id: 'cp1',
    ventaId: 'v1',
    tipo: 'Boleta',
    serie: 'B001',
    numero: '000001',
    estado: 'Emitido',
    fechaCreacion: '2026-09-30T17:42:00Z',
    activo: true,
    subtotalGravado: 42,
    subtotalExonerado: 0,
    subtotalInafecto: 0,
    porcentajeIgv: 18,
    montoIgv: 7.56,
    total: 49.56,
    metodoPagoPrincipal: 'Yape / Plin',
    observaciones: null,
    ordenServicioId: null,
  },
  ...extra,
})

describe('ficha del comprobante', () => {
  it('lleva el comprobante, el desglose de IGV, lo pagado y el saldo', () => {
    const html = htmlComprobante(venta(), undefined, 'OS-000024')
    expect(html).toContain('<title>Boleta B001-000001</title>')
    expect(html).toContain('IGV (18 %)')
    expect(html).toContain('S/ 49.56')
    expect(html).toContain('Saldo pendiente')
    expect(html).toContain('liquida la orden OS-000024')
    expect(html).toContain('No reemplaza al comprobante electrónico')
    expect(html).not.toContain('ANULADO')
  })

  it('marca como anulado el comprobante anulado', () => {
    const html = htmlComprobante(venta({ comprobante: { ...venta().comprobante!, estado: 'Anulado' } }), undefined)
    expect(html).toContain('ANULADO')
  })

  it('escapa lo que escriben los usuarios', () => {
    const html = htmlComprobante(venta({ clienteNombre: '<script>alert(1)</script>' }), undefined)
    expect(html).not.toContain('<script>alert(1)</script>')
    expect(html).toContain('&lt;script&gt;')
    expect(escaparHtml(`"a" & 'b'`)).toBe('&quot;a&quot; &amp; &#39;b&#39;')
  })
})

describe('cobro', () => {
  it('solo hay algo que cobrar con total y saldo mayores a cero', () => {
    expect(tieneSaldo({ total: 49.56, saldo: 29.56 })).toBe(true)
    expect(tieneSaldo({ total: 49.56, saldo: 0 })).toBe(false)
    expect(tieneSaldo({ total: 0, saldo: 0 })).toBe(false)
    expect(tieneSaldo({})).toBe(false)
  })
})

describe('hoja del formato de atención', () => {
  it('lleva los datos de la orden, los totales y las firmas, con el estilo de la marca', async () => {
    const { htmlFormatoAtencion } = await import('./formatoAtencion')
    const html = htmlFormatoAtencion({
      empresa: { nombreTaller: 'Team Benavides', razonSocial: 'Team Benavides S.R.L.', ruc: '20000000001', direccion: null, telefono: null, email: null },
      orden: {
        id: 'o1',
        numeroOrden: 'OS-000024',
        estadoId: 4,
        estadoNombre: 'Lista',
        fechaIngreso: '2026-09-30T17:40:00Z',
        fechaEstimadaEntrega: null,
        fechaSalida: null,
        tipoAtencion: 'MantenimientoPreventivo',
        modalidadAtencion: 'EnTaller',
        tipoFalla: null,
      },
      cliente: { id: 'c1', nombreCompleto: 'Luis <Quispe>', razonSocial: null, tipoDocumento: 'DNI', numeroDocumento: '40123456', telefono: null, email: null, direccion: null },
      unidad: {
        id: 'u1',
        tipoUnidad: 'MotoAcuatica',
        marca: 'Yamaha',
        modelo: 'VX',
        anio: 2022,
        placa: null,
        numeroSerieVIN: 'YAMA2231H122',
        numeroMotor: null,
        color: null,
        tipoMedidor: 'Horas',
        lecturaIngreso: 86,
        lecturaActualSalida: null,
      },
      trabajo: { motivoFalla: 'No arranca', diagnostico: null, solucion: null, observaciones: null, tecnicoResponsable: null, tecnicoEmail: null },
      items: [],
      financiero: {
        subtotalGravado: 42,
        subtotalExonerado: 0,
        subtotalInafecto: 0,
        montoIgv: 7.56,
        total: 49.56,
        totalPagado: 20,
        saldoPendiente: 29.56,
        porcentajeIgv: 18,
        moneda: 'PEN',
      },
      fechaEmision: '2026-09-30T18:00:00Z',
    })

    expect(html).toContain('<title>Orden de servicio OS-000024</title>')
    expect(html).toContain('Mantenimiento preventivo · En taller')
    expect(html).toContain('Moto acuática · Yamaha VX (2022)')
    expect(html).toContain('86 h')
    expect(html).toContain('Saldo pendiente')
    expect(html).toContain('Firma y DNI del cliente')
    expect(html).toContain('Luis &lt;Quispe&gt;')
    expect(html).toContain('Space Grotesk')
    expect(html).not.toContain('#2563eb')
  })
})
