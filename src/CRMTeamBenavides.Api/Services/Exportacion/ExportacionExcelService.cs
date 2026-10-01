using ClosedXML.Excel;

namespace CRMTeamBenavides.Api.Services.Exportacion;

public class ExportacionExcelService : IExportacionExcelService
{
    public byte[] GenerarExcelCitas(IEnumerable<CitaExcelDto> citas)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Agenda de Citas");

        // Cabeceras
        string[] headers =
        {
            "N° Cita",
            "Fecha y Hora",
            "Cliente",
            "Documento",
            "Vehículo / Modelo",
            "Placa",
            "Motivo / Servicio",
            "Estado",
            "Duración (min)",
            "Observaciones"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        int row = 2;
        foreach (var c in citas)
        {
            ws.Cell(row, 1).Value = c.NumeroCita;
            ws.Cell(row, 2).Value = c.FechaHoraProgramada.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 3).Value = c.ClienteNombre;
            ws.Cell(row, 4).Value = c.ClienteDocumento;
            ws.Cell(row, 5).Value = c.VehiculoInfo;
            ws.Cell(row, 6).Value = c.Placa;
            ws.Cell(row, 7).Value = c.Motivo;
            ws.Cell(row, 8).Value = c.Estado;
            ws.Cell(row, 9).Value = c.DuracionMinutos;
            ws.Cell(row, 10).Value = c.Observaciones ?? string.Empty;

            // Formatos de alineación y bordes
            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = ws.Cell(row, col);
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#E0E0E0");
            }

            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerarExcelPedidosLima(IEnumerable<PedidoLimaExcelDto> pedidos)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Pedidos Especiales Lima");

        // Cabeceras
        string[] headers =
        {
            "N° Pedido",
            "Fecha Registro",
            "Cliente",
            "Documento",
            "Transporte / Courier",
            "N° Guía / Tracking",
            "Cant. Ítems",
            "Subtotal Gravado (S/)",
            "IGV (S/)",
            "Total (S/)",
            "Estado",
            "F. Est. Llegada",
            "F. Llegada",
            "F. Entrega",
            "Observaciones"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        int row = 2;
        foreach (var p in pedidos)
        {
            ws.Cell(row, 1).Value = p.NumeroPedido;
            ws.Cell(row, 2).Value = p.Fecha.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 3).Value = p.ClienteNombre;
            ws.Cell(row, 4).Value = p.ClienteDocumento;
            ws.Cell(row, 5).Value = p.EmpresaTransporte;
            ws.Cell(row, 6).Value = p.NumeroGuia;
            ws.Cell(row, 7).Value = p.CantidadItems;
            
            var cSubtotal = ws.Cell(row, 8);
            cSubtotal.Value = p.SubtotalGravado;
            cSubtotal.Style.NumberFormat.Format = "S/ #,##0.00";

            var cIgv = ws.Cell(row, 9);
            cIgv.Value = p.MontoIgv;
            cIgv.Style.NumberFormat.Format = "S/ #,##0.00";

            var cTotal = ws.Cell(row, 10);
            cTotal.Value = p.Total;
            cTotal.Style.NumberFormat.Format = "S/ #,##0.00";

            ws.Cell(row, 11).Value = p.Estado;
            ws.Cell(row, 12).Value = p.FechaEstimadaLlegada.HasValue ? p.FechaEstimadaLlegada.Value.ToString("yyyy-MM-dd") : string.Empty;
            ws.Cell(row, 13).Value = p.FechaLlegada.HasValue ? p.FechaLlegada.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty;
            ws.Cell(row, 14).Value = p.FechaEntrega.HasValue ? p.FechaEntrega.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty;
            ws.Cell(row, 15).Value = p.Observaciones ?? string.Empty;

            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = ws.Cell(row, col);
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#E0E0E0");
            }

            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
