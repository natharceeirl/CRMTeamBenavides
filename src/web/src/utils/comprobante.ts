import type { ConfiguracionEmpresaResponse, VentaDetalleResponse } from '../api/tipos'
import { nombresTipoItem } from '../api/tipos'
import { colores, fuentes } from '../theme/tokens'
import { escaparHtml } from './impresion'
import { fechaHora, importe, referenciaOrden, soles } from './formato'

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

  const linea = (etiqueta: string, valor: string, clase = '') =>
    `<div class="linea ${clase}"><span>${escaparHtml(etiqueta)}</span><span>${escaparHtml(valor)}</span></div>`

  const porcentajeIgv = comprobante?.porcentajeIgv ?? empresa?.porcentajeIgv ?? 18
  const empresaNombre = empresa?.razonSocial || empresa?.nombreEmpresa || 'Team Benavides'
  const datosEmpresa = [
    empresa?.ruc ? `RUC ${empresa.ruc}` : null,
    empresa?.direccion,
    empresa?.telefono,
    empresa?.email,
  ].filter(Boolean)

  return `<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escaparHtml(titulo)}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;600&family=Space+Grotesk:wght@700&display=swap" rel="stylesheet">
<style>
  * { box-sizing: border-box; }
  body { margin: 0; background: ${colores.fondo}; color: ${colores.texto}; font: 14px/1.5 ${fuentes.texto}; }
  .hoja { position: relative; max-width: 800px; margin: 24px auto; padding: 40px; background: ${colores.blanco}; }
  h1, h2 { font-family: ${fuentes.titulos}; margin: 0; letter-spacing: -0.02em; }
  h1 { font-size: 22px; }
  h2 { font-size: 20px; text-align: right; }
  .cabecera { display: flex; justify-content: space-between; gap: 24px; padding-bottom: 16px; border-bottom: 2px solid ${colores.texto}; }
  .sec { color: ${colores.textoSecundario}; font-size: 12px; }
  .bloque { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin: 20px 0; }
  .etiqueta { font-size: 11px; font-weight: 600; letter-spacing: 0.12em; text-transform: uppercase; color: ${colores.textoSecundario}; }
  table { width: 100%; border-collapse: collapse; }
  th { text-align: left; font-size: 11px; letter-spacing: 0.12em; text-transform: uppercase; color: ${colores.textoSecundario}; border-bottom: 2px solid ${colores.divisor}; padding: 8px 6px; }
  td { padding: 8px 6px; border-bottom: 1px solid ${colores.divisorSuave}; vertical-align: top; }
  .num { text-align: right; white-space: nowrap; font-variant-numeric: tabular-nums; }
  th.num { text-align: right; }
  .totales { margin-left: auto; width: 300px; margin-top: 16px; }
  .linea { display: flex; justify-content: space-between; padding: 3px 0; font-variant-numeric: tabular-nums; }
  .linea.total { border-top: 2px solid ${colores.texto}; margin-top: 6px; padding-top: 8px; font-family: ${fuentes.titulos}; font-size: 18px; font-weight: 700; }
  .linea.saldo { font-weight: 600; }
  .nota { margin-top: 28px; font-size: 12px; color: ${colores.textoSecundario}; }
  .anulado { position: absolute; top: 40%; left: 0; right: 0; text-align: center; font: 700 72px ${fuentes.titulos}; color: ${colores.acento600}; opacity: 0.18; transform: rotate(-18deg); pointer-events: none; }
  .acciones { max-width: 800px; margin: 24px auto 0; text-align: right; }
  .acciones button { font: 600 14px ${fuentes.texto}; color: ${colores.blanco}; background: ${colores.acento600}; border: 0; padding: 10px 18px; cursor: pointer; }
  @media print {
    body { background: ${colores.blanco}; }
    .hoja { margin: 0; padding: 0; max-width: none; }
    .acciones { display: none; }
  }
</style>
</head>
<body>
<div class="acciones"><button type="button" onclick="window.print()">Imprimir o guardar PDF</button></div>
<main class="hoja">
  ${anulado ? '<div class="anulado">ANULADO</div>' : ''}
  <div class="cabecera">
    <div>
      <h1>${escaparHtml(empresaNombre)}</h1>
      <div class="sec">${datosEmpresa.map(escaparHtml).join(' · ')}</div>
    </div>
    <div>
      <h2>${escaparHtml(titulo)}</h2>
      <div class="sec" style="text-align:right">${escaparHtml(fechaHora(comprobante?.fechaCreacion ?? venta.fecha))}</div>
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
    ${linea('Op. gravadas', soles(venta.subtotalGravado))}
    ${venta.subtotalExonerado > 0 ? linea('Op. exoneradas', soles(venta.subtotalExonerado)) : ''}
    ${venta.subtotalInafecto > 0 ? linea('Op. inafectas', soles(venta.subtotalInafecto)) : ''}
    ${linea(`IGV (${porcentajeIgv} %)`, soles(venta.montoIgv))}
    ${linea('Total', soles(venta.total), 'total')}
    ${linea('Pagado', soles(venta.totalPagado))}
    ${venta.saldo > 0 ? linea('Saldo pendiente', soles(venta.saldo), 'saldo') : ''}
  </div>

  ${comprobante?.observaciones ? `<p><span class="etiqueta">Observaciones</span><br>${escaparHtml(comprobante.observaciones)}</p>` : ''}
  <p class="nota">Registro interno de Team Benavides. No reemplaza al comprobante electrónico.</p>
</main>
</body>
</html>`
}
