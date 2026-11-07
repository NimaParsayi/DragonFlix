using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace DragonFlix.Api.Services.Telegram;

public record TgUser(long Id, string FirstName, string LastName, string Username, string LanguageCode, bool AllowsWrite)
{
    public string DisplayName => (FirstName + " " + LastName).Trim() is { Length: > 0 } n ? n : (Username != "" ? "@" + Username : $"کاربر {Id}");
}

/// <summary>
/// اعتبارسنجی initData مینی‌اپ تلگرام طبق مستندات رسمی: امضای HMAC-SHA256 با کلید مشتق از توکن ربات.
/// تا وقتی امضا درست نباشد هیچ‌چیزِ داخل initData قابل اعتماد نیست.
/// </summary>
public static class TelegramAuth
{
    public static TgUser? Validate(string? initData, string botToken, TimeSpan maxAge, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(initData) || string.IsNullOrWhiteSpace(botToken)) { error = "اطلاعات ورود تلگرام ارسال نشد."; return null; }

        var query = HttpUtility.ParseQueryString(initData);
        var hash = query["hash"];
        if (string.IsNullOrEmpty(hash)) { error = "امضای تلگرام وجود ندارد."; return null; }

        var dataCheck = string.Join("\n", query.AllKeys.Where(k => k is not null and not "hash")
            .OrderBy(k => k, StringComparer.Ordinal).Select(k => $"{k}={query[k]}"));

        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var expected = Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheck))).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(hash.ToLowerInvariant())))
        { error = "امضای تلگرام معتبر نیست."; return null; }

        if (!long.TryParse(query["auth_date"], out var authDate) ||
            DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(authDate) > maxAge)
        { error = "نشست تلگرامت منقضی شده؛ برنامه رو دوباره از تلگرام باز کن."; return null; }

        try
        {
            using var doc = JsonDocument.Parse(query["user"] ?? "");
            var u = doc.RootElement;
            string S(string n) => u.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            return new TgUser(u.GetProperty("id").GetInt64(), S("first_name"), S("last_name"), S("username"), S("language_code"),
                u.TryGetProperty("allows_write_to_pm", out var w) && w.ValueKind == JsonValueKind.True);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        { error = "اطلاعات کاربر در initData نامعتبر است."; return null; }
    }
}
