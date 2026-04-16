using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using proyectoprogra.Models.Entities;

namespace proyectoprogra.Services
{
    public class FacturaPdfService
    {
        // ── colour palette ────────────────────────────────────────────────────
        private const string PrimaryDark  = "#0d3b26";
        private const string PrimaryMid   = "#1a6640";
        private const string AccentGreen  = "#2ecc71";
        private const string RowAlt       = "#f0faf4";
        private const string BorderColor  = "#d1fae5";
        private const string TextMuted    = "#6b7280";

        public byte[] Generate(Factura factura)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(1.6f, Unit.Centimetre);
                    page.MarginVertical(1.4f, Unit.Centimetre);
                    page.DefaultTextStyle(t =>
                        t.FontFamily("Arial").FontSize(10).FontColor("#1a1a1a"));

                    page.Content().Column(col =>
                    {
                        // ─── HEADER ───────────────────────────────────────────
                        col.Item().Background(PrimaryDark).Padding(20).Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text("RestauranteApp")
                                    .FontSize(22).Bold().FontColor(Colors.White);
                                left.Item().Text("Sistema de Gestión de Restaurante")
                                    .FontSize(9).FontColor("#a7f3d0");
                            });

                            row.ConstantItem(160).Column(right =>
                            {
                                right.Item()
                                    .Background(AccentGreen)
                                    .Padding(6)
                                    .AlignCenter()
                                    .Text("FACTURA ELECTRÓNICA")
                                    .FontSize(10).Bold().FontColor(PrimaryDark);
                                right.Item().PaddingTop(6).AlignRight()
                                    .Text(factura.NumeroFactura)
                                    .FontSize(10).Bold().FontColor(Colors.White);
                            });
                        });

                        col.Item().Height(10);

                        // ─── META INFO ────────────────────────────────────────
                        col.Item()
                            .Border(1).BorderColor(BorderColor)
                            .Padding(12)
                            .Row(row =>
                        {
                            MetaCell(row, "Número de Factura",  factura.NumeroFactura);
                            MetaCell(row, "Fecha",              factura.Fecha.ToString("dd/MM/yyyy"));
                            MetaCell(row, "Hora",               factura.Fecha.ToString("HH:mm"));
                            MetaCell(row, "N° Pedido",          $"#{factura.PedidoId}");
                            MetaCell(row, "Tipo",
                                factura.Pedido?.TipoPedido switch
                                {
                                    "dine-in"  => "Mesa / Dine-In",
                                    "takeout"  => "Para Llevar",
                                    "delivery" => "Delivery",
                                    _          => factura.Pedido?.TipoPedido ?? "—"
                                });
                        });

                        col.Item().Height(14);

                        // ─── SECTION TITLE ────────────────────────────────────
                        col.Item().Text("Detalle de Productos")
                            .FontSize(11).Bold().FontColor(PrimaryMid);
                        col.Item().Height(4);

                        // ─── ITEMS TABLE ──────────────────────────────────────
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28);   // #
                                c.RelativeColumn(4);    // Producto
                                c.RelativeColumn(1.2f); // Cant.
                                c.RelativeColumn(2);    // Precio unit.
                                c.RelativeColumn(2);    // Total línea
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(PrimaryMid)
                                    .PaddingVertical(7).PaddingHorizontal(6)
                                    .Text("#").FontSize(9).Bold().FontColor(Colors.White);

                                h.Cell().Background(PrimaryMid)
                                    .PaddingVertical(7).PaddingHorizontal(6)
                                    .Text("Producto").FontSize(9).Bold().FontColor(Colors.White);

                                h.Cell().Background(PrimaryMid)
                                    .PaddingVertical(7).PaddingHorizontal(6)
                                    .AlignCenter()
                                    .Text("Cant.").FontSize(9).Bold().FontColor(Colors.White);

                                h.Cell().Background(PrimaryMid)
                                    .PaddingVertical(7).PaddingHorizontal(6)
                                    .AlignRight()
                                    .Text("Precio Unit.").FontSize(9).Bold().FontColor(Colors.White);

                                h.Cell().Background(PrimaryMid)
                                    .PaddingVertical(7).PaddingHorizontal(6)
                                    .AlignRight()
                                    .Text("Total Línea").FontSize(9).Bold().FontColor(Colors.White);
                            });

                            int rowNum = 1;
                            foreach (var d in factura.FacturaDetalles)
                            {
                                string bg = rowNum % 2 == 0 ? RowAlt : Colors.White;

                                table.Cell()
                                    .Background(bg).BorderBottom(1).BorderColor(BorderColor)
                                    .PaddingVertical(6).PaddingHorizontal(6)
                                    .AlignCenter()
                                    .Text(rowNum.ToString()).FontSize(9).FontColor(TextMuted);

                                table.Cell()
                                    .Background(bg).BorderBottom(1).BorderColor(BorderColor)
                                    .PaddingVertical(6).PaddingHorizontal(6)
                                    .Text(d.Producto?.Nombre ?? "—").FontSize(9);

                                table.Cell()
                                    .Background(bg).BorderBottom(1).BorderColor(BorderColor)
                                    .PaddingVertical(6).PaddingHorizontal(6)
                                    .AlignCenter()
                                    .Text(d.Cantidad.ToString()).FontSize(9);

                                table.Cell()
                                    .Background(bg).BorderBottom(1).BorderColor(BorderColor)
                                    .PaddingVertical(6).PaddingHorizontal(6)
                                    .AlignRight()
                                    .Text($"₡{d.PrecioUnitario:N2}").FontSize(9);

                                table.Cell()
                                    .Background(bg).BorderBottom(1).BorderColor(BorderColor)
                                    .PaddingVertical(6).PaddingHorizontal(6)
                                    .AlignRight()
                                    .Text($"₡{d.TotalLinea:N2}").FontSize(9).Bold();

                                rowNum++;
                            }
                        });

                        col.Item().Height(16);

                        // ─── TOTALS ───────────────────────────────────────────
                        col.Item().AlignRight().Width(270).Column(tot =>
                        {
                            TotalsRow(tot, "Subtotal",          $"₡{factura.Subtotal:N2}");
                            TotalsRow(tot, "IVA (13%)",         $"₡{factura.Iva:N2}");

                            if (factura.Propina > 0)
                                TotalsRow(tot, "Propina (10%)",   $"₡{factura.Propina:N2}");

                            if (factura.CostoEmpaque > 0)
                                TotalsRow(tot, "Costo de Empaque", $"₡{factura.CostoEmpaque:N2}");

                            if (factura.CostoDelivery > 0)
                                TotalsRow(tot, "Costo de Delivery", $"₡{factura.CostoDelivery:N2}");

                            if (factura.CreditoAplicado > 0)
                                TotalsRowColored(tot,
                                    "Crédito Aplicado",
                                    $"–₡{factura.CreditoAplicado:N2}",
                                    "#059669");

                            tot.Item().BorderTop(2).BorderColor(PrimaryMid).PaddingTop(2);

                            tot.Item().Background(PrimaryDark).Padding(10).Row(r =>
                            {
                                r.RelativeItem()
                                    .Text("TOTAL A PAGAR")
                                    .FontSize(12).Bold().FontColor(Colors.White);
                                r.ConstantItem(120).AlignRight()
                                    .Text($"₡{factura.Total:N2}")
                                    .FontSize(13).Bold().FontColor(AccentGreen);
                            });
                        });

                        col.Item().Height(20);

                        // ─── TAX NOTE ─────────────────────────────────────────
                        col.Item()
                            .Background(RowAlt)
                            .Border(1).BorderColor(BorderColor)
                            .Padding(10)
                            .Column(notes =>
                        {
                            notes.Item().Text("Información Tributaria")
                                .FontSize(9).Bold().FontColor(PrimaryMid);
                            notes.Item().Height(4);
                            notes.Item()
                                .Text("• IVA calculado al 13 % sobre el subtotal de productos.")
                                .FontSize(8).FontColor(TextMuted);

                            if (factura.Propina > 0)
                                notes.Item()
                                    .Text("• Propina del 10 % aplicada automáticamente (pedido en mesa).")
                                    .FontSize(8).FontColor(TextMuted);

                            if (factura.CreditoAplicado > 0)
                                notes.Item()
                                    .Text($"• Crédito de ₡{factura.CreditoAplicado:N2} descontado del saldo disponible del cliente.")
                                    .FontSize(8).FontColor(TextMuted);
                        });
                    });

                    // ─── FOOTER ───────────────────────────────────────────────
                    page.Footer()
                        .BorderTop(1).BorderColor(BorderColor)
                        .PaddingTop(6)
                        .Row(row =>
                    {
                        row.RelativeItem()
                            .Text("RestauranteApp  •  Documento tributario electrónico generado automáticamente")
                            .FontSize(8).FontColor(TextMuted);

                        row.ConstantItem(80).AlignRight().Text(t =>
                        {
                            t.Span("Página ").FontSize(8).FontColor(TextMuted);
                            t.CurrentPageNumber().FontSize(8).FontColor(TextMuted);
                            t.Span(" de ").FontSize(8).FontColor(TextMuted);
                            t.TotalPages().FontSize(8).FontColor(TextMuted);
                        });
                    });
                });
            }).GeneratePdf();
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static void MetaCell(RowDescriptor row, string label, string value)
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text(label).FontSize(7.5f).FontColor(TextMuted);
                c.Item().Text(value).FontSize(9.5f).Bold();
            });
        }

        private static void TotalsRow(ColumnDescriptor col, string label, string value)
        {
            col.Item().PaddingBottom(3).Row(r =>
            {
                r.RelativeItem().Text(label).FontSize(9).FontColor(TextMuted);
                r.ConstantItem(120).AlignRight().Text(value).FontSize(9).Bold();
            });
        }

        private static void TotalsRowColored(
            ColumnDescriptor col,
            string label,
            string value,
            string color)
        {
            col.Item().PaddingBottom(3).Row(r =>
            {
                r.RelativeItem().Text(label).FontSize(9).FontColor(color);
                r.ConstantItem(120).AlignRight().Text(value).FontSize(9).Bold().FontColor(color);
            });
        }
    }
}
