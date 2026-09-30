import { colores, fuentes } from '../theme/tokens'
import { escaparHtml } from './impresion'

/**
 * Esqueleto y estilos de las hojas que se imprimen desde la web (comprobante,
 * formato de atención): la misma identidad que la web, con los colores de
 * los tokens y las fuentes de la marca. Trae su botón de imprimir, que no sale
 * en el papel.
 */
export function documentoImprimible(titulo: string, cuerpo: string): string {
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
  h1, h2, h3 { font-family: ${fuentes.titulos}; margin: 0; letter-spacing: -0.02em; }
  h1 { font-size: 22px; }
  h2 { font-size: 20px; text-align: right; }
  h3 { font-size: 15px; margin-bottom: 6px; }
  .cabecera { display: flex; justify-content: space-between; gap: 24px; padding-bottom: 16px; border-bottom: 2px solid ${colores.texto}; }
  .sec { color: ${colores.textoSecundario}; font-size: 12px; }
  .derecha { text-align: right; }
  .sin-salto { white-space: nowrap; }
  .bloque { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin: 20px 0; }
  .seccion { margin: 20px 0; }
  .etiqueta { font-size: 11px; font-weight: 600; letter-spacing: 0.12em; text-transform: uppercase; color: ${colores.textoSecundario}; }
  table { width: 100%; border-collapse: collapse; }
  th { text-align: left; font-size: 11px; letter-spacing: 0.12em; text-transform: uppercase; color: ${colores.textoSecundario}; border-bottom: 2px solid ${colores.divisor}; padding: 8px 6px; }
  td { padding: 8px 6px; border-bottom: 1px solid ${colores.divisorSuave}; vertical-align: top; }
  table.datos { border-top: 2px solid ${colores.divisor}; }
  table.datos td { padding: 6px; }
  table.datos td:first-child { width: 38%; color: ${colores.textoSecundario}; }
  .num { text-align: right; white-space: nowrap; font-variant-numeric: tabular-nums; }
  th.num { text-align: right; }
  .totales { margin-left: auto; width: 300px; margin-top: 16px; }
  .linea { display: flex; justify-content: space-between; padding: 3px 0; font-variant-numeric: tabular-nums; }
  .linea.total { border-top: 2px solid ${colores.texto}; margin-top: 6px; padding-top: 8px; font-family: ${fuentes.titulos}; font-size: 18px; font-weight: 700; }
  .linea.saldo { font-weight: 600; }
  .firmas { display: grid; grid-template-columns: 1fr 1fr; gap: 56px; margin-top: 64px; break-inside: avoid; }
  .firma { border-top: 1px solid ${colores.texto}; padding-top: 8px; text-align: center; }
  .nota { margin-top: 28px; font-size: 12px; color: ${colores.textoSecundario}; }
  .anulado { position: absolute; top: 40%; left: 0; right: 0; text-align: center; font: 700 72px ${fuentes.titulos}; color: ${colores.acento600}; opacity: 0.18; transform: rotate(-18deg); pointer-events: none; }
  .acciones { max-width: 800px; margin: 24px auto 0; text-align: right; }
  .acciones button { font: 600 14px ${fuentes.texto}; color: ${colores.blanco}; background: ${colores.acento600}; border: 0; padding: 10px 18px; cursor: pointer; }
  @media print {
    body { background: ${colores.blanco}; }
    .hoja { margin: 0; padding: 0; max-width: none; }
    .acciones { display: none; }
    tr, .seccion { break-inside: avoid; }
  }
</style>
</head>
<body>
<div class="acciones"><button type="button" onclick="window.print()">Imprimir o guardar PDF</button></div>
<main class="hoja">
${cuerpo}
</main>
</body>
</html>`
}

/** Una línea del bloque de totales: etiqueta a la izquierda, monto a la derecha. */
export const lineaTotal = (etiqueta: string, valor: string, clase = '') =>
  `<div class="linea ${clase}"><span>${escaparHtml(etiqueta)}</span><span>${escaparHtml(valor)}</span></div>`

/** Una fila de una tabla de datos; si no hay valor, no se imprime la fila. */
export const filaDato = (etiqueta: string, valor: string | number | null | undefined) =>
  valor === null || valor === undefined || valor === ''
    ? ''
    : `<tr><td>${escaparHtml(etiqueta)}</td><td>${escaparHtml(valor)}</td></tr>`
