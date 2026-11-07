using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DragonFlix.Api.Services;

public record TicketSeat(string Label, decimal Price);

public record TicketData(
    string Code, string Movie, string Cinema, string Hall, DateTime StartsAt,
    string BuyerName, decimal Total, DateTime CreatedAt, IReadOnlyList<TicketSeat> Seats, string PosterUrl = "");

public static class TicketPdf
{
    private const string Blue = "#1d4ed8";
    private const string Ink = "#0f172a";
    private const string Muted = "#64748b";
    private const string Line = "#dbe3f0";

    public static void RegisterFonts()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");
        foreach (var f in Directory.GetFiles(dir, "*.ttf"))
        {
            using var s = File.OpenRead(f);
            FontManager.RegisterFont(s);
        }
    }

    public static string FormatCode(string code) => code.Length == 8 ? $"{code[..4]}-{code[4..]}" : code;

    public static byte[] Render(TicketData t) => Build(t).GeneratePdf();

    /// <summary>همان بلیت به‌صورت تصویر PNG (برای ارسال در تلگرام).</summary>
    public static byte[] RenderPng(TicketData t) =>
        Build(t).GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 150 }).First();

    private static IDocument Build(TicketData t)
    {
        var qr = new PngByteQRCode(new QRCodeGenerator().CreateQrCode(t.Code, QRCodeGenerator.ECCLevel.M)).GetGraphic(12);

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(26);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(x => x.FontFamily("Vazirmatn").FontSize(10.5f).FontColor(Ink));

            page.Content().Column(col =>
            {
                col.Spacing(12);

                // سربرگ
                col.Item().Background(Blue).CornerRadius(8).PaddingVertical(12).PaddingHorizontal(16).Row(r =>
                {
                    r.RelativeItem().Text("دراگون‌فلیکس").Bold().FontSize(16).FontColor(Colors.White);
                    r.AutoItem().AlignMiddle().Text("بلیت سینما").FontColor("#bfdbfe");
                });

                // فیلم
                col.Item().Column(c =>
                {
                    c.Item().Text(t.Movie).Bold().FontSize(22);
                    c.Item().Text($"{t.Cinema} · {t.Hall}").FontColor(Muted);
                });

                // زمان
                col.Item().Row(r =>
                {
                    r.RelativeItem().Element(c => Info(c, "تاریخ نمایش", PersianFormat.Date(t.StartsAt)));
                    r.RelativeItem().Element(c => Info(c, "ساعت شروع", PersianFormat.Time(t.StartsAt)));
                    r.RelativeItem().Element(c => Info(c, "تعداد صندلی", PersianFormat.Digits(t.Seats.Count.ToString())));
                });

                // صندلی‌ها
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(3); });
                    table.Header(h =>
                    {
                        h.Cell().Element(Head).Text("ردیف");
                        h.Cell().Element(Head).Text("صندلی");
                        h.Cell().Element(Head).Text("قیمت");
                    });
                    foreach (var s in t.Seats)
                    {
                        var (sr, sn) = SeatFormat.Parse(s.Label);
                        table.Cell().Element(Body).Text(sr).Bold();
                        table.Cell().Element(Body).Text(PersianFormat.Digits(sn.ToString())).Bold();
                        table.Cell().Element(Body).Text(PersianFormat.Money(s.Price));
                    }
                    table.Cell().Element(Total).Text("جمع کل").Bold();
                    table.Cell().Element(Total).Text("");
                    table.Cell().Element(Total).Text(PersianFormat.Money(t.Total)).Bold().FontColor(Blue);
                });

                col.Item().LineHorizontal(1).LineColor(Line);

                // کد و QR
                col.Item().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Spacing(2);
                        c.Item().Text("کد بلیت").FontColor(Muted).FontSize(9);
                        c.Item().AlignRight().Text(FormatCode(t.Code)).FontFamily("Vazirmatn").Bold().FontSize(20).FontColor(Blue);
                        c.Item().PaddingTop(6).Text($"خریدار: {t.BuyerName}").FontColor(Muted).FontSize(9);
                        c.Item().Text($"زمان ثبت: {PersianFormat.Date(t.CreatedAt)}، {PersianFormat.Time(t.CreatedAt)}").FontColor(Muted).FontSize(9);
                    });
                    r.ConstantItem(96).Border(1).BorderColor(Line).Padding(4).Image(qr);
                });

                col.Item().Text("لطفاً حداقل ۱۵ دقیقه پیش از شروع سانس حاضر باشید و این بلیت (چاپی یا روی گوشی) را هنگام ورود به مسئول سالن نشان دهید.")
                    .FontSize(9).FontColor(Muted);
            });
        }));
    }

    private static void Info(IContainer c, string label, string value) => c.Column(x =>
    {
        x.Item().Text(label).FontColor(Muted).FontSize(9);
        x.Item().Text(value).Bold().FontSize(11.5f);
    });

    private static IContainer Head(IContainer c) =>
        c.Background("#eff6ff").PaddingVertical(5).PaddingHorizontal(8).DefaultTextStyle(x => x.SemiBold().FontColor("#1e3a8a").FontSize(9.5f));
    private static IContainer Body(IContainer c) =>
        c.BorderBottom(1).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(8);
    private static IContainer Total(IContainer c) =>
        c.PaddingVertical(7).PaddingHorizontal(8);
}
