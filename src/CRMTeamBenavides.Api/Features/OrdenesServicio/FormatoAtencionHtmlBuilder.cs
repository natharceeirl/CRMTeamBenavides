using System.Net;
using System.Text;

namespace CRMTeamBenavides.Api.Features.OrdenesServicio;

public static class FormatoAtencionHtmlBuilder
{
    public static string BuildHtml(FormatoAtencionResponse data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>Orden de Servicio - {WebUtility.HtmlEncode(data.Orden.NumeroOrden)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; color: #1f2937; background: #f3f4f6; padding: 20px; font-size: 13px; line-height: 1.4; }");
        sb.AppendLine("    .container { max-width: 800px; margin: 0 auto; background: #ffffff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); }");
        sb.AppendLine("    .no-print { margin-bottom: 20px; text-align: right; }");
        sb.AppendLine("    .btn-print { background: #2563eb; color: #ffffff; border: none; padding: 8px 18px; border-radius: 6px; font-size: 14px; font-weight: 600; cursor: pointer; display: inline-flex; align-items: center; gap: 8px; }");
        sb.AppendLine("    .btn-print:hover { background: #1d4ed8; }");
        sb.AppendLine("    .header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #e5e7eb; padding-bottom: 16px; margin-bottom: 16px; }");
        sb.AppendLine("    .taller-info h1 { font-size: 20px; color: #111827; font-weight: 800; margin-bottom: 4px; }");
        sb.AppendLine("    .taller-info p { color: #4b5563; font-size: 12px; }");
        sb.AppendLine("    .orden-badge { text-align: right; border: 2px solid #2563eb; border-radius: 8px; padding: 10px 16px; background: #eff6ff; }");
        sb.AppendLine("    .orden-badge .titulo { font-size: 11px; text-transform: uppercase; font-weight: 700; color: #1e40af; letter-spacing: 0.5px; }");
        sb.AppendLine("    .orden-badge .numero { font-size: 20px; font-weight: 800; color: #1e3a8a; margin: 2px 0; }");
        sb.AppendLine("    .orden-badge .estado { font-size: 12px; font-weight: 600; color: #2563eb; }");
        sb.AppendLine("    .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 16px; }");
        sb.AppendLine("    .card { border: 1px solid #e5e7eb; border-radius: 6px; padding: 12px 14px; background: #fafafa; }");
        sb.AppendLine("    .card-title { font-size: 12px; font-weight: 700; text-transform: uppercase; color: #374151; border-bottom: 1px solid #e5e7eb; padding-bottom: 6px; margin-bottom: 8px; }");
        sb.AppendLine("    .data-row { display: flex; justify-content: space-between; margin-bottom: 4px; font-size: 12px; }");
        sb.AppendLine("    .data-label { color: #6b7280; font-weight: 500; }");
        sb.AppendLine("    .data-val { color: #111827; font-weight: 600; text-align: right; }");
        sb.AppendLine("    .section-title { font-size: 13px; font-weight: 700; text-transform: uppercase; color: #1f2937; margin: 16px 0 8px 0; border-left: 4px solid #2563eb; padding-left: 8px; }");
        sb.AppendLine("    .card-full { border: 1px solid #e5e7eb; border-radius: 6px; padding: 12px 14px; background: #fafafa; margin-bottom: 16px; font-size: 12px; }");
        sb.AppendLine("    .card-full p { margin-bottom: 6px; }");
        sb.AppendLine("    .card-full p:last-child { margin-bottom: 0; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; margin-bottom: 16px; font-size: 12px; }");
        sb.AppendLine("    th { background: #f3f4f6; color: #374151; font-weight: 700; text-align: left; padding: 8px 10px; border-top: 1px solid #e5e7eb; border-bottom: 2px solid #e5e7eb; }");
        sb.AppendLine("    td { padding: 8px 10px; border-bottom: 1px solid #e5e7eb; }");
        sb.AppendLine("    .text-right { text-align: right; }");
        sb.AppendLine("    .text-center { text-align: center; }");
        sb.AppendLine("    .totales-box { display: flex; justify-content: flex-end; margin-bottom: 24px; }");
        sb.AppendLine("    .totales-table { width: 280px; font-size: 12px; }");
        sb.AppendLine("    .totales-table td { padding: 4px 8px; border: none; }");
        sb.AppendLine("    .totales-table .total-row { font-size: 14px; font-weight: 800; border-top: 2px solid #111827; border-bottom: 2px solid #111827; color: #111827; }");
        sb.AppendLine("    .firmas-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 40px; margin-top: 40px; padding-top: 16px; }");
        sb.AppendLine("    .firma-box { border-top: 1px dashed #9ca3af; text-align: center; padding-top: 8px; font-size: 11px; color: #4b5563; }");
        sb.AppendLine("    .firma-nombre { font-weight: 700; color: #111827; margin-top: 2px; }");
        sb.AppendLine("    .footer { text-align: center; margin-top: 30px; font-size: 11px; color: #9ca3af; border-top: 1px solid #e5e7eb; padding-top: 10px; }");
        sb.AppendLine("    @media print {");
        sb.AppendLine("      body { background: #ffffff; padding: 0; font-size: 11px; }");
        sb.AppendLine("      .container { max-width: 100%; box-shadow: none; padding: 0; border: none; }");
        sb.AppendLine("      .no-print { display: none !important; }");
        sb.AppendLine("      @page { margin: 1.2cm; }");
        sb.AppendLine("    }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("  <div class=\"no-print container\">");
        sb.AppendLine("    <button class=\"btn-print\" onclick=\"window.print()\">🖨️ Imprimir / Guardar PDF</button>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class=\"container\">");

        // Header
        sb.AppendLine("    <div class=\"header\">");
        sb.AppendLine("      <div class=\"taller-info\">");
        sb.AppendLine($"        <h1>{WebUtility.HtmlEncode(data.Empresa.NombreTaller)}</h1>");
        sb.AppendLine($"        <p><strong>Razón Social:</strong> {WebUtility.HtmlEncode(data.Empresa.RazonSocial)}</p>");
        if (!string.IsNullOrWhiteSpace(data.Empresa.Ruc))
            sb.AppendLine($"        <p><strong>RUC:</strong> {WebUtility.HtmlEncode(data.Empresa.Ruc)}</p>");
        if (!string.IsNullOrWhiteSpace(data.Empresa.Direccion))
            sb.AppendLine($"        <p><strong>Dirección:</strong> {WebUtility.HtmlEncode(data.Empresa.Direccion)}</p>");
        if (!string.IsNullOrWhiteSpace(data.Empresa.Telefono) || !string.IsNullOrWhiteSpace(data.Empresa.Email))
            sb.AppendLine($"        <p><strong>Contacto:</strong> {WebUtility.HtmlEncode(data.Empresa.Telefono ?? "")} | {WebUtility.HtmlEncode(data.Empresa.Email ?? "")}</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"orden-badge\">");
        sb.AppendLine("        <div class=\"titulo\">Formato Oficial de Atención - Orden de Servicio</div>");
        sb.AppendLine($"        <div class=\"numero\">{WebUtility.HtmlEncode(data.Orden.NumeroOrden)}</div>");
        sb.AppendLine($"        <div class=\"estado\">Estado: {WebUtility.HtmlEncode(data.Orden.EstadoNombre)}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Grid Cliente y Unidad
        sb.AppendLine("    <div class=\"grid-2\">");
        // Cliente
        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <div class=\"card-title\">Datos del Cliente</div>");
        sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Nombre:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Cliente.NombreCompleto)}</span></div>");
        if (!string.IsNullOrWhiteSpace(data.Cliente.RazonSocial))
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Razón Social:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Cliente.RazonSocial)}</span></div>");
        if (!string.IsNullOrWhiteSpace(data.Cliente.NumeroDocumento))
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">{WebUtility.HtmlEncode(data.Cliente.TipoDocumento ?? "Documento")}:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Cliente.NumeroDocumento)}</span></div>");
        if (!string.IsNullOrWhiteSpace(data.Cliente.Telefono))
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Teléfono:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Cliente.Telefono)}</span></div>");
        if (!string.IsNullOrWhiteSpace(data.Cliente.Direccion))
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Dirección:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Cliente.Direccion)}</span></div>");
        sb.AppendLine("      </div>");

        // Unidad
        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <div class=\"card-title\">Datos de la Unidad</div>");
        sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Tipo:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Unidad.TipoUnidad)}</span></div>");
        sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Marca / Modelo:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Unidad.Marca)} {WebUtility.HtmlEncode(data.Unidad.Modelo)} {(data.Unidad.Anio.HasValue ? "(" + data.Unidad.Anio.Value + ")" : "")}</span></div>");
        if (!string.IsNullOrWhiteSpace(data.Unidad.Placa))
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Placa:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Unidad.Placa)}</span></div>");
        if (!string.IsNullOrWhiteSpace(data.Unidad.NumeroSerieVIN))
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">VIN / Serie:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Unidad.NumeroSerieVIN)}</span></div>");
        if (!string.IsNullOrWhiteSpace(data.Unidad.NumeroMotor))
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Motor:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Unidad.NumeroMotor)}</span></div>");
        var sufijoMedidor = data.Unidad.TipoMedidor == "Horas" ? "hrs" : "km";
        if (data.Unidad.LecturaIngreso.HasValue)
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Lectura Ingreso:</span><span class=\"data-val\">{data.Unidad.LecturaIngreso.Value:N0} {sufijoMedidor}</span></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Fechas y Clasificación
        sb.AppendLine("    <div class=\"grid-2\">");
        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <div class=\"card-title\">Fechas y Atención</div>");
        sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Fecha de Ingreso:</span><span class=\"data-val\">{data.Orden.FechaIngreso:dd/MM/yyyy HH:mm}</span></div>");
        if (data.Orden.FechaEstimadaEntrega.HasValue)
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Estimada Entrega:</span><span class=\"data-val\">{data.Orden.FechaEstimadaEntrega.Value:dd/MM/yyyy}</span></div>");
        if (data.Orden.FechaSalida.HasValue)
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Fecha de Salida:</span><span class=\"data-val\">{data.Orden.FechaSalida.Value:dd/MM/yyyy HH:mm}</span></div>");
        sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Tipo Atención:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Orden.TipoAtencion)} ({WebUtility.HtmlEncode(data.Orden.ModalidadAtencion)})</span></div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <div class=\"card-title\">Responsable Técnico</div>");
        sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Técnico Asignado:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Trabajo.TecnicoResponsable ?? "Sin asignar")}</span></div>");
        if (!string.IsNullOrWhiteSpace(data.Trabajo.TecnicoEmail))
            sb.AppendLine($"        <div class=\"data-row\"><span class=\"data-label\">Email Técnico:</span><span class=\"data-val\">{WebUtility.HtmlEncode(data.Trabajo.TecnicoEmail)}</span></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Trabajo / Diagnóstico
        sb.AppendLine("    <div class=\"section-title\">Diagnóstico y Motivo de Ingreso</div>");
        sb.AppendLine("    <div class=\"card-full\">");
        if (!string.IsNullOrWhiteSpace(data.Trabajo.MotivoFalla))
            sb.AppendLine($"      <p><strong>Motivo declarado / Falla reportada:</strong> {WebUtility.HtmlEncode(data.Trabajo.MotivoFalla)}</p>");
        if (!string.IsNullOrWhiteSpace(data.Trabajo.Diagnostico))
            sb.AppendLine($"      <p><strong>Diagnóstico técnico:</strong> {WebUtility.HtmlEncode(data.Trabajo.Diagnostico)}</p>");
        if (!string.IsNullOrWhiteSpace(data.Trabajo.Solucion))
            sb.AppendLine($"      <p><strong>Trabajo realizado / Solución:</strong> {WebUtility.HtmlEncode(data.Trabajo.Solucion)}</p>");
        if (!string.IsNullOrWhiteSpace(data.Trabajo.Observaciones))
            sb.AppendLine($"      <p><strong>Observaciones generales:</strong> {WebUtility.HtmlEncode(data.Trabajo.Observaciones)}</p>");
        if (string.IsNullOrWhiteSpace(data.Trabajo.MotivoFalla) && string.IsNullOrWhiteSpace(data.Trabajo.Diagnostico))
            sb.AppendLine("      <p><em>Sin observaciones adicionales registradas.</em></p>");
        sb.AppendLine("    </div>");

        // Detalle de Ítems
        sb.AppendLine("    <div class=\"section-title\">Detalle de Trabajos, Repuestos y Servicios</div>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <thead>");
        sb.AppendLine("        <tr>");
        sb.AppendLine("          <th style=\"width: 15%;\">Tipo</th>");
        sb.AppendLine("          <th style=\"width: 45%;\">Descripción</th>");
        sb.AppendLine("          <th style=\"width: 10%;\" class=\"text-center\">Cant.</th>");
        sb.AppendLine("          <th style=\"width: 15%;\" class=\"text-right\">P. Unit. (S/)</th>");
        sb.AppendLine("          <th style=\"width: 15%;\" class=\"text-right\">Total (S/)</th>");
        sb.AppendLine("        </tr>");
        sb.AppendLine("      </thead>");
        sb.AppendLine("      <tbody>");

        if (data.Items.Count == 0)
        {
            sb.AppendLine("        <tr><td colspan=\"5\" class=\"text-center\"><em>No se han registrado ítems o repuestos en esta orden.</em></td></tr>");
        }
        else
        {
            foreach (var item in data.Items)
            {
                sb.AppendLine("        <tr>");
                sb.AppendLine($"          <td>{WebUtility.HtmlEncode(item.TipoItemNombre)}</td>");
                sb.AppendLine($"          <td>{WebUtility.HtmlEncode(item.Descripcion)}</td>");
                sb.AppendLine($"          <td class=\"text-center\">{item.Cantidad}</td>");
                sb.AppendLine($"          <td class=\"text-right\">{item.PrecioUnitario:N2}</td>");
                sb.AppendLine($"          <td class=\"text-right\">{item.Total:N2}</td>");
                sb.AppendLine("        </tr>");
            }
        }

        sb.AppendLine("      </tbody>");
        sb.AppendLine("    </table>");

        // Resumen Financiero
        sb.AppendLine("    <div class=\"totales-box\">");
        sb.AppendLine("      <table class=\"totales-table\">");
        if (data.Financiero.SubtotalGravado > 0)
            sb.AppendLine($"        <tr><td>Op. Gravada:</td><td class=\"text-right\">S/ {data.Financiero.SubtotalGravado:N2}</td></tr>");
        if (data.Financiero.SubtotalExonerado > 0)
            sb.AppendLine($"        <tr><td>Op. Exonerada:</td><td class=\"text-right\">S/ {data.Financiero.SubtotalExonerado:N2}</td></tr>");
        if (data.Financiero.SubtotalInafecto > 0)
            sb.AppendLine($"        <tr><td>Op. Inafecta:</td><td class=\"text-right\">S/ {data.Financiero.SubtotalInafecto:N2}</td></tr>");
        sb.AppendLine($"        <tr><td>IGV ({data.Financiero.PorcentajeIgv:N0}%):</td><td class=\"text-right\">S/ {data.Financiero.MontoIgv:N2}</td></tr>");
        sb.AppendLine($"        <tr class=\"total-row\"><td>TOTAL:</td><td class=\"text-right\">S/ {data.Financiero.Total:N2}</td></tr>");
        if (data.Financiero.TotalPagado > 0)
            sb.AppendLine($"        <tr><td style=\"color: #059669; font-weight: 600;\">Pagado / Adelanto:</td><td class=\"text-right\" style=\"color: #059669; font-weight: 600;\">S/ {data.Financiero.TotalPagado:N2}</td></tr>");
        sb.AppendLine($"        <tr><td style=\"color: #dc2626; font-weight: 700;\">SALDO PENDIENTE:</td><td class=\"text-right\" style=\"color: #dc2626; font-weight: 700;\">S/ {data.Financiero.SaldoPendiente:N2}</td></tr>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");

        // Firmas
        sb.AppendLine("    <div class=\"firmas-grid\">");
        sb.AppendLine("      <div class=\"firma-box\">");
        sb.AppendLine("        <div class=\"firma-nombre\">Firma y DNI del Cliente</div>");
        sb.AppendLine($"        <div>{WebUtility.HtmlEncode(data.Cliente.NombreCompleto)}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"firma-box\">");
        sb.AppendLine("        <div class=\"firma-nombre\">Firma y Sello del Taller</div>");
        sb.AppendLine($"        <div>{WebUtility.HtmlEncode(data.Trabajo.TecnicoResponsable ?? data.Empresa.NombreTaller)}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Footer
        sb.AppendLine("    <div class=\"footer\">");
        sb.AppendLine($"      Documento generado el {data.FechaEmision:dd/MM/yyyy HH:mm:ss} UTC · {WebUtility.HtmlEncode(data.Empresa.NombreTaller)} · Sistema CRM Team Benavides");
        sb.AppendLine("    </div>");

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
