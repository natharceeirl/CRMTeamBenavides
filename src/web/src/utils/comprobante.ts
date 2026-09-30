import type { ConfiguracionEmpresaResponse, VentaDetalleResponse } from '../api/tipos'
import { nombresTipoItem } from '../api/tipos'
import { documentoImprimible, lineaTotal } from './documento'
import { escaparHtml } from './impresion'
import { fechaHoraConAnio, importe, referenciaOrden, soles } from './formato'

/**
 * Ficha imprimible del comprobante de una venta. Es un registro interno: el
 * sistema no emite comprobantes electrónicos ante SUNAT (fuera de alcance).
 */
export function htmlComprobante(
  venta: VentaDetalleResponse,
  empresa: ConfiguracionEmpresaResponse | undefined,
  /** Número de la orden que liquida la venta (OS-000024); si no se tiene, va el id corto. */
  numeroOrden?: string | null,
): string {
  const comprobante = venta.comprobante
  const titulo = comprobante
    ? `${comprobante.tipo} ${[comprobante.serie, comprobante.numero].filter(Boolean).join('-')}`.trim()
    : `Venta ${referenciaOrden(venta.id)}`
  const anulado = comprobante?.estado === 'Anulado' || venta.estado === 'Anulada'

  const filas = venta.detalles
    .map((detalle) => {
      const tipo = nombresTipoItem[detalle.tipoItem ?? 0] ?? detalle.tipoItemNombre ?? ''
      const secundario = [tipo, detalle.productoCodigo].filter(Boolean).join(' · ')
      return `<tr>
        <td>${escaparHtml(detalle.productoNombre)}<div class="sec">${escaparHtml(secundario)}</div></td>
        <td class="num">${escaparHtml(detalle.cantidad)}</td>
        <td class="num">${importe(detalle.precioUnitario)}</td>
        <td class="num">${importe(detalle.montoIgv ?? 0)}</td>
        <td class="num">${importe(detalle.total ?? detalle.subtotal)}</td>
      </tr>`
    })
    .join('')

  const porcentajeIgv = comprobante?.porcentajeIgv ?? empresa?.porcentajeIgv ?? 18
  const empresaNombre = empresa?.razonSocial || empresa?.nombreEmpresa || 'Team Benavides'
  const datosEmpresa = [
    empresa?.ruc ? `RUC ${empresa.ruc}` : null,
    empresa?.direccion,
    empresa?.telefono,
    empresa?.email,
  ].filter(Boolean)

  return documentoImprimible(
    titulo,
    `
  ${anulado ? '<div class="anulado">ANULADO</div>' : ''}
  <div class="cabecera">
    <div>
      <h1>${escaparHtml(empresaNombre)}</h1>
      <div class="sec">${datosEmpresa.map(escaparHtml).join(' · ')}</div>
    </div>
    <div>
      <h2>${escaparHtml(titulo)}</h2>
      <div class="sec derecha">${escaparHtml(fechaHoraConAnio(comprobante?.fechaCreacion ?? venta.fecha))}</div>
    </div>
  </div>

  <div class="bloque">
    <div>
      <div class="etiqueta">Cliente</div>
      <div>${escaparHtml(venta.clienteNombre)}</div>
      <div class="sec">${escaparHtml([venta.clienteDocumento, venta.clienteTelefono].filter(Boolean).join(' · '))}</div>
    </div>
    <div>
      <div class="etiqueta">Venta</div>
      <div>${escaparHtml(referenciaOrden(venta.id))}${venta.ordenServicioId ? ` · liquida la orden ${escaparHtml(numeroOrden || referenciaOrden(venta.ordenServicioId))}` : ''}</div>
      <div class="sec">${escaparHtml(comprobante?.metodoPagoPrincipal ? `Pago: ${comprobante.metodoPagoPrincipal}` : '')}</div>
    </div>
  </div>

  <table>
    <thead><tr><th>Concepto</th><th class="num">Cant.</th><th class="num">P. unit.</th><th class="num">IGV</th><th class="num">Importe</th></tr></thead>
    <tbody>${filas}</tbody>
  </table>

  <div class="totales">
    ${lineaTotal('Op. gravadas', soles(venta.subtotalGravado))}
    ${venta.subtotalExonerado > 0 ? lineaTotal('Op. exoneradas', soles(venta.subtotalExonerado)) : ''}
    ${venta.subtotalInafecto > 0 ? lineaTotal('Op. inafectas', soles(venta.subtotalInafecto)) : ''}
    ${lineaTotal(`IGV (${porcentajeIgv} %)`, soles(venta.montoIgv))}
    ${lineaTotal('Total', soles(venta.total), 'total')}
    ${lineaTotal('Pagado', soles(venta.totalPagado))}
    ${venta.saldo > 0 ? lineaTotal('Saldo pendiente', soles(venta.saldo), 'saldo') : ''}
  </div>

  ${comprobante?.observaciones ? `<p><span class="etiqueta">Observaciones</span><br>${escaparHtml(comprobante.observaciones)}</p>` : ''}
  <p class="nota">Registro interno de Team Benavides. No reemplaza al comprobante electrónico.</p>`,
  )
}
