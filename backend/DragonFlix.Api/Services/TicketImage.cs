using System.Collections.Concurrent;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DragonFlix.Api.Services;

/// <summary>پوسترِ فیلم برای بلیت تصویری: فایل‌های خودمان از دیسک، آدرس‌های بیرونی (TMDB) با کش حافظه.</summary>
public static class PosterSource
{
    private static readonly ConcurrentDictionary<string, byte[]> Cache = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static string _webRoot = "";

    public static void Init(IWebHostEnvironment env) => _webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    public static async Task<byte[]?> LoadAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Cache.TryGetValue(url, out var hit)) return hit;
        try
        {
            byte[]? bytes = null;
            if (MovieImages.IsManaged(url))
            {
                var path = Path.Combine(_webRoot, "uploads", "movies", Path.GetFileName(url));
                if (File.Exists(path)) bytes = await File.ReadAllBytesAsync(path);
            }
            else if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                bytes = await Http.GetByteArrayAsync(url);
            if (bytes is { Length: > 0 and < 6_000_000 }) { if (Cache.Count > 64) Cache.Clear(); Cache[url] = bytes; return bytes; }
        }
        catch { /* بدون پوستر ادامه می‌دهیم */ }
        return null;
    }
}

/// <summary>بلیت تیره و آبی برای تلگرام: پوستر فیلم، مشخصات سانس و QR در یک تصویر (نسخهٔ PDF چاپی جداست).</summary>
public static class TicketImage
{
    private const float W = 640, H = 300, M = 14, StubW = 140, Pad = 22;

    private static string Bg()
    {
        var divX = M + Pad + StubW + 11;
        return $$"""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {{W + 2 * M}} {{H + 2 * M}}">
          <defs>
            <linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#1b46c4"/><stop offset="0.5" stop-color="#14306f"/><stop offset="1" stop-color="#0b1730"/></linearGradient>
            <radialGradient id="r" cx="0.2" cy="0" r="0.8"><stop offset="0" stop-color="#60a5fa" stop-opacity="0.35"/><stop offset="1" stop-color="#60a5fa" stop-opacity="0"/></radialGradient>
          </defs>
          <rect width="{{W + 2 * M}}" height="{{H + 2 * M}}" fill="#0b0d12"/>
          <rect x="{{M}}" y="{{M}}" width="{{W}}" height="{{H}}" rx="22" fill="url(#g)"/>
          <rect x="{{M}}" y="{{M}}" width="{{W}}" height="{{H}}" rx="22" fill="url(#r)"/>
          <rect x="{{M + 0.5}}" y="{{M + 0.5}}" width="{{W - 1}}" height="{{H - 1}}" rx="21.5" fill="none" stroke="#ffffff" stroke-opacity="0.12"/>
          <line x1="{{divX}}" y1="{{M + 16}}" x2="{{divX}}" y2="{{M + H - 16}}" stroke="#ffffff" stroke-opacity="0.28" stroke-width="1.5" stroke-dasharray="5 5"/>
          <circle cx="{{divX}}" cy="{{M}}" r="11" fill="#0b0d12"/>
          <circle cx="{{divX}}" cy="{{M + H}}" r="11" fill="#0b0d12"/>
        </svg>
        """;
    }

    public static byte[] Render(TicketData t, byte[]? poster)
    {
        var qr = new PngByteQRCode(new QRCodeGenerator().CreateQrCode(t.Code, QRCodeGenerator.ECCLevel.M)).GetGraphic(14);
        var seatLines = SeatFormat.Lines(t.Seats.Select(s => (s.Row, s.Number)));

        var doc = Document.Create(d => d.Page(p =>
        {
            p.Size(W + 2 * M, H + 2 * M, Unit.Point);
            p.Margin(0);
            p.ContentFromRightToLeft();
            p.DefaultTextStyle(x => x.FontFamily("Vazirmatn").FontColor("#ffffff").FontSize(10.5f));
            p.Content().Layers(l =>
            {
                l.Layer().Svg(Bg());
                l.PrimaryLayer().Padding(M + Pad).Row(row =>
                {
                    row.Spacing(16);

                    // پوستر (راست)
                    row.ConstantItem(150).Height(H - 2 * Pad).AlignMiddle().Element(c =>
                    {
                        var box = c.Height(225).CornerRadius(12).Background("#1d3b7a");
                        if (poster is not null) box.Image(poster).FitUnproportionally();
                        else box.AlignCenter().AlignMiddle().Text(t.Movie[..1]).FontSize(54).Bold().FontColor("#93c5fd");
                    });

                    // مشخصات (وسط)
                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        c.Spacing(7);
                        c.Item().Text("دراگون فلیکس · بلیت سینما").FontSize(9).FontColor("#93c5fd").SemiBold();
                        c.Item().Text(t.Movie).FontSize(20).Bold().LineHeight(1.25f);
                        c.Item().Text($"{t.Cinema} · {t.Hall}").FontColor("#bfdbfe");
                        if (!string.IsNullOrWhiteSpace(t.BuyerName))
                            c.Item().Text($"نام: {t.BuyerName}").FontSize(11.5f).SemiBold().FontColor("#e0ecff");

                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.Spacing(10);
                            r.AutoItem().Element(x => Chip(x, "تاریخ", PersianFormat.Date(t.StartsAt)));
                            r.AutoItem().Element(x => Chip(x, "ساعت", PersianFormat.Time(t.StartsAt)));
                        });

                        c.Item().PaddingTop(2).Column(s =>
                        {
                            s.Spacing(4);
                            foreach (var line in seatLines.Take(4))
                                s.Item().AlignRight().Background("#2b4fa3").CornerRadius(8).PaddingHorizontal(9).PaddingVertical(2).Text(line).FontSize(10.5f).SemiBold();
                        });
                    });

                    // قسمت کنده (چپ): QR و کد
                    row.ConstantItem(StubW).AlignMiddle().Column(c =>
                    {
                        c.Spacing(8);
                        c.Item().AlignCenter().Width(112).Height(112).Background("#ffffff").CornerRadius(12).Padding(7).Image(qr).FitArea();
                        c.Item().AlignCenter().Text(TicketPdf.FormatCode(t.Code)).FontFamily("Vazirmatn").Bold().FontSize(13).LetterSpacing(0.06f);
                        c.Item().AlignCenter().Text("کد بلیت").FontSize(8.5f).FontColor("#93c5fd");
                    });
                });
            });
        }));
        return doc.GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 216 }).First();
    }

    private static void Chip(IContainer c, string label, string value) =>
        c.Column(x =>
        {
            x.Item().Text(label).FontSize(8.5f).FontColor("#93c5fd");
            x.Item().Text(value).FontSize(12).Bold();
        });
}
