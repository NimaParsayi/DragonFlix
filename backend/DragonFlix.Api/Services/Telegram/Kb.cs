using System.Text.Json;

namespace DragonFlix.Api.Services.Telegram;

/// <summary>سازندهٔ دکمه‌های اینلاین (شامل قابلیت‌های جدید: web_app، copy_text و style رنگی).</summary>
public static class Kb
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>باز کردن مینی‌اپ داخل تلگرام.</summary>
    public static object Web(string text, string url, string? style = Primary) => new { Text = text, WebApp = new { Url = url }, Style = Norm(style) };
    /// <summary>دکمهٔ کپی متن در کلیپ‌بورد کاربر (Bot API 7.11+).</summary>
    public static object Copy(string text, string copy, string? style = Primary) => new { Text = text, CopyText = new { Text = copy }, Style = Norm(style) };
    /// <summary>style: primary (آبی) | success (سبز) | danger (قرمز)</summary>
    public static object Cb(string text, string data, string? style = Primary) => new { Text = text, CallbackData = data, Style = Norm(style) };
    public static object Link(string text, string url, string? style = Primary) => new { Text = text, Url = url, Style = Norm(style) };

    // تم پروژه آبی است: همهٔ دکمه‌ها به‌طور پیش‌فرض آبی‌اند؛ فقط معنای متفاوت رنگ دیگر می‌گیرد.
    public const string Primary = "primary";   // آبی: کنش عادی
    public const string Success = "success";   // سبز: تأیید / موفقیت
    public const string Danger = "danger";     // قرمز: رد، لغو، حذف
    public const string Neutral = "none";      // بی‌رنگ: بازگشت و گزینهٔ خاموش
    private static string? Norm(string? s) => s is null or Neutral ? null : s;

    public static object Markup(params object[][] rows) => new { InlineKeyboard = rows };
    public static string ToJson(object markup) => JsonSerializer.Serialize(markup, Json);
}
