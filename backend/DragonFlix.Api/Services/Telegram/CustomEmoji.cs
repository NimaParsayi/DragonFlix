using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DragonFlix.Api.Services.Telegram;

/// <summary>
/// ایموجی‌های معمولی را با ایموجی متحرک (کاستوم) پک‌های تنظیم‌شده جایگزین می‌کند. ایموجی عادی داخل تگ می‌ماند تا
/// اگر ربات اجازهٔ ارسال ایموجی کاستوم نداشت، کاربر همان ایموجی همیشگی را ببیند. (همان روش دراگون‌موویز)
/// </summary>
public class CustomEmoji
{
    private static readonly Regex Tags = new("(<[^>]*>)", RegexOptions.Compiled);
    private volatile Dictionary<string, string> map = new();

    /// <summary>اگر تلگرام پیامی را به‌خاطر ایموجی کاستوم رد کند خودکار خاموش می‌شود.</summary>
    public volatile bool Enabled = true;
    public int Count => map.Count;

    // ایموجی‌هایی که در پک‌ها نیستند ← نزدیک‌ترین معادل
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["⚠️"] = "❗️", ["⛔"] = "🔐", ["🔒"] = "🔐", ["⬅️"] = "👉", ["↩️"] = "🔄", ["🗑"] = "❌",
        ["📋"] = "🏷", ["📸"] = "🧾", ["📅"] = "🗓", ["🔢"] = "🧮", ["🆔"] = "🪪", ["🔖"] = "🏷",
        ["📄"] = "🧾", ["💰"] = "💵", ["🔸"] = "🔹", ["⬜️"] = "🔹", ["📝"] = "🧾", ["📖"] = "🧾", ["💺"] = "🎟", ["🖥"] = "🎬", ["⚙️"] = "🔄",
    };

    public async Task LoadAsync(TelegramApi tg, string packs, ILogger log)
    {
        var result = new Dictionary<string, string>();
        foreach (var name in packs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var set = await tg.CallAsync("getStickerSet", new { Name = name }, skipEmoji: true);
                foreach (var s in set.GetProperty("stickers").EnumerateArray())
                {
                    if (!s.TryGetProperty("custom_emoji_id", out var id) || !s.TryGetProperty("emoji", out var em)) continue;
                    result.TryAdd(Normalize(em.GetString() ?? ""), id.GetString() ?? "");
                }
            }
            catch (Exception e) { log.LogWarning("Emoji pack '{Pack}' could not be loaded: {Msg}", name, e.Message); }
        }
        foreach (var (emoji, sub) in Aliases)
            if (!result.ContainsKey(Normalize(emoji)) && result.TryGetValue(Normalize(sub), out var sid)) result[Normalize(emoji)] = sid;
        map = result;
        log.LogInformation("Custom emoji loaded: {Count}", result.Count);
    }

    public string Apply(string html)
    {
        var cur = map;
        if (!Enabled || cur.Count == 0 || string.IsNullOrEmpty(html)) return html;
        var sb = new StringBuilder(html.Length + 64);
        var code = false;
        foreach (var part in Tags.Split(html))
        {
            if (part.StartsWith('<'))
            {
                var t = part.ToLowerInvariant();
                if (t.StartsWith("<code") || t.StartsWith("<pre")) code = true;
                else if (t.StartsWith("</code") || t.StartsWith("</pre")) code = false;
                sb.Append(part);
                continue;
            }
            if (code) { sb.Append(part); continue; }
            var e = StringInfo.GetTextElementEnumerator(part);
            while (e.MoveNext())
            {
                var el = (string)e.Current;
                if (cur.TryGetValue(Normalize(el), out var id)) sb.Append("<tg-emoji emoji-id=\"").Append(id).Append("\">").Append(el).Append("</tg-emoji>");
                else sb.Append(el);
            }
        }
        return sb.ToString();
    }

    /// <summary>متن دکمه‌ای که با ایموجی شروع می‌شود ← (شناسهٔ ایموجی، متن بدون ایموجی).</summary>
    public (string? Id, string Text) SplitLeading(string text)
    {
        var cur = map;
        if (!Enabled || cur.Count == 0 || string.IsNullOrEmpty(text)) return (null, text);
        var e = StringInfo.GetTextElementEnumerator(text);
        if (!e.MoveNext()) return (null, text);
        var first = (string)e.Current;
        if (!cur.TryGetValue(Normalize(first), out var id)) return (null, text);
        var rest = text[first.Length..].TrimStart();
        return rest.Length == 0 ? (null, text) : (id, rest);
    }

    /// <summary>بدنهٔ JSON درخواست را بازنویسی می‌کند (متن/کپشن HTML و دکمه‌ها). null یعنی تغییری لازم نیست.</summary>
    public JsonObject? Rewrite(JsonObject json)
    {
        var changed = false;
        if (json["parse_mode"]?.GetValue<string>()?.Equals("HTML", StringComparison.OrdinalIgnoreCase) == true)
            foreach (var key in new[] { "text", "caption" })
                if (json[key] is JsonValue v && v.TryGetValue<string>(out var s))
                {
                    var r = Apply(s);
                    if (r != s) { json[key] = r; changed = true; }
                }
        if (json["reply_markup"] is JsonObject markup && RewriteMarkup(markup)) changed = true;
        return changed ? json : null;
    }

    public bool RewriteMarkup(JsonObject markup)
    {
        var changed = false;
        foreach (var key in new[] { "inline_keyboard", "keyboard" })
        {
            if (markup[key] is not JsonArray rows) continue;
            foreach (var row in rows.OfType<JsonArray>())
            foreach (var b in row.OfType<JsonObject>())
            {
                if (b["text"] is not JsonValue tv || !tv.TryGetValue<string>(out var label)) continue;
                var (id, stripped) = SplitLeading(label);
                if (id is null) continue;
                b["icon_custom_emoji_id"] = id;
                b["text"] = stripped;
                changed = true;
            }
        }
        return changed;
    }

    private static string Normalize(string e) => e.Replace("️", "");
}
