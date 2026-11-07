using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DragonFlix.Api.Services;

/// <summary>تصویر کارت بانکی (شماره، دارنده و لوگوی بانک تشخیص‌داده‌شده از BIN) که بالای راهنمای پرداخت می‌رود.</summary>
public static class CardImage
{
    private const float W = 480, H = 290;

    private const string Bg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 480 290" preserveAspectRatio="none">
          <defs>
            <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stop-color="#1e4fd6"/><stop offset="0.55" stop-color="#14306f"/><stop offset="1" stop-color="#0b1730"/>
            </linearGradient>
            <radialGradient id="r" cx="0.85" cy="0.1" r="0.7">
              <stop offset="0" stop-color="#60a5fa" stop-opacity="0.45"/><stop offset="1" stop-color="#60a5fa" stop-opacity="0"/>
            </radialGradient>
          </defs>
          <rect width="480" height="290" fill="url(#g)"/>
          <rect width="480" height="290" fill="url(#r)"/>
          <circle cx="60" cy="300" r="150" fill="#ffffff" fill-opacity="0.05"/>
          <circle cx="440" cy="250" r="110" fill="#ffffff" fill-opacity="0.05"/>
        </svg>
        """;

    private const string Chip = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 36">
          <defs><linearGradient id="c" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#f3d98b"/><stop offset="1" stop-color="#c9a24a"/></linearGradient></defs>
          <rect width="48" height="36" rx="7" fill="url(#c)"/>
          <path d="M0 12h48M0 24h48M16 0v36M32 0v36" stroke="#8a6a1f" stroke-opacity="0.55" stroke-width="1.2" fill="none"/>
        </svg>
        """;

    /// <summary>PNG کارت؛ اگر شمارهٔ ۱۶ رقمی نباشد null.</summary>
    public static byte[]? Render(string cardNumber, string? holder, string? fallbackBank = null)
    {
        var digits = new string((cardNumber ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != 16) return null;

        var bank = BankInfo.Detect(digits);
        var bankName = bank?.Name ?? (string.IsNullOrWhiteSpace(fallbackBank) ? "" : fallbackBank.Trim());
        var logo = BankInfo.LogoPath(digits);
        var groups = Enumerable.Range(0, 4).Select(i => PersianFormat.Digits(digits.Substring(i * 4, 4))).ToArray();

        var doc = Document.Create(d => d.Page(p =>
        {
            p.Size(W, H, Unit.Point);
            p.Margin(0);
            p.DefaultTextStyle(t => t.FontFamily("Vazirmatn").FontColor("#ffffff"));
            p.Content().Layers(l =>
            {
                l.Layer().Svg(Bg);
                l.PrimaryLayer().Padding(26).Column(c =>
                {
                    // بالا: تراشه در چپ، نام و لوگوی بانک در راست
                    c.Item().Height(50).Row(r =>
                    {
                        r.ConstantItem(56).AlignMiddle().Svg(Chip);
                        r.RelativeItem().AlignMiddle().AlignRight().PaddingRight(10).Text(bankName).FontSize(13).SemiBold().FontColor("#dbeafe");
                        if (logo is not null)
                            r.ConstantItem(50).Height(50).Background("#ffffff").CornerRadius(13).Padding(4).Image(logo).FitArea();
                    });

                    // شماره: ۴ گروه، چپ به راست مثل کارت واقعی
                    c.Item().PaddingTop(46).Row(r =>
                    {
                        r.RelativeItem().ContentFromLeftToRight().Row(rr =>
                        {
                            rr.Spacing(14);
                            foreach (var g in groups)
                                rr.AutoItem().Text(g).FontSize(27).Bold().LetterSpacing(0.04f);
                        });
                    });

                    // پایین: دارندهٔ کارت
                    c.Item().PaddingTop(34).Column(h =>
                    {
                        h.Item().AlignRight().Text("به نام").FontSize(9.5f).FontColor("#93c5fd");
                        h.Item().AlignRight().Text(string.IsNullOrWhiteSpace(holder) ? "—" : holder.Trim()).FontSize(17).SemiBold();
                    });
                });
            });
        }));

        return doc.GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 216 }).First();
    }
}
