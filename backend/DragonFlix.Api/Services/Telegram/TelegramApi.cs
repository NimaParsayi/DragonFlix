using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DragonFlix.Api.Services.Telegram;

public class TelegramApiException(int code, string description, int retryAfter = 0) : Exception(description)
{
    public int Code { get; } = code;
    public int RetryAfter { get; } = retryAfter;
    /// <summary>کاربر ربات را مسدود کرده یا چت وجود ندارد.</summary>
    public bool Unreachable => Code == 403 || (Code == 400 && Description.Contains("chat not found", StringComparison.OrdinalIgnoreCase));
    public string Description => Message;
}

/// <summary>
/// کلاینت سبک و مستقیم Bot API (بدون کتابخانهٔ واسط) تا هر قابلیت جدید تلگرام — دکمه‌های رنگی، copy_text،
/// افکت پیام، web_app و … — با همان نام فیلد قابل استفاده باشد. نام‌های PascalCase به snake_case تبدیل می‌شوند.
/// </summary>
public class TelegramApi(HttpClient http, IOptionsMonitor<TelegramOptions> options, ILogger<TelegramApi> log)
{
    public CustomEmoji Emoji { get; } = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private TelegramOptions O => options.CurrentValue;
    public bool Configured => !string.IsNullOrWhiteSpace(O.BotToken);
    public string Token => O.BotToken;
    public string? Username { get; private set; }

    private string Url(string method) => $"{O.ApiBase.TrimEnd('/')}/bot{O.BotToken}/{method}";

    public async Task<JsonElement> CallAsync(string method, object? payload = null, CancellationToken ct = default, bool skipEmoji = false)
    {
        var body = JsonSerializer.Serialize(payload ?? new { }, Json);
        if (!skipEmoji && Emoji.Enabled && Emoji.Count > 0 && System.Text.Json.Nodes.JsonNode.Parse(body) is System.Text.Json.Nodes.JsonObject obj
            && Emoji.Rewrite(obj) is { } rewritten)
        {
            try
            {
                using var fancy = new StringContent(rewritten.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
                return await SendAsync(method, fancy, ct);
            }
            catch (TelegramApiException e) when (e.Code == 400 && !e.Description.Contains("chat not found", StringComparison.OrdinalIgnoreCase))
            {
                // شاید ایموجی کاستوم مشکل بوده: درخواست اصلی را بفرست؛ اگر جواب داد یعنی ربات اجازه‌اش را ندارد.
                using var plain = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                var res = await SendAsync(method, plain, ct);
                Emoji.Enabled = false;
                log.LogWarning("Telegram rejected custom emojis ({Msg}); falling back to normal emojis.", e.Description);
                return res;
            }
        }
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        return await SendAsync(method, content, ct);
    }

    /// <summary>ارسال با فایل (عکس/سند). fields فیلدهای متنی و file بایت‌های فایل است.</summary>
    public async Task<JsonElement> CallMultipartAsync(string method, Dictionary<string, string?> fields, string fileField, string fileName, byte[] bytes, string contentType, CancellationToken ct = default)
    {
        async Task<JsonElement> Go(Dictionary<string, string?> f)
        {
            using var form = new MultipartFormDataContent();
            foreach (var (k, v) in f) if (v is not null) form.Add(new StringContent(v), k);
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, fileField, fileName);
            return await SendAsync(method, form, ct);
        }

        if (Emoji.Enabled && Emoji.Count > 0)
        {
            var fancy = new Dictionary<string, string?>(fields);
            var changed = false;
            if (fields.GetValueOrDefault("parse_mode") == "HTML" && fields.GetValueOrDefault("caption") is { } cap)
            {
                var r = Emoji.Apply(cap);
                if (r != cap) { fancy["caption"] = r; changed = true; }
            }
            if (fields.GetValueOrDefault("reply_markup") is { } rm && System.Text.Json.Nodes.JsonNode.Parse(rm) is System.Text.Json.Nodes.JsonObject mo && Emoji.RewriteMarkup(mo))
            {
                fancy["reply_markup"] = mo.ToJsonString();
                changed = true;
            }
            if (changed)
            {
                try { return await Go(fancy); }
                catch (TelegramApiException e) when (e.Code == 400 && !e.Description.Contains("chat not found", StringComparison.OrdinalIgnoreCase))
                {
                    var res = await Go(fields);
                    Emoji.Enabled = false;
                    log.LogWarning("Telegram rejected custom emojis ({Msg}); falling back to normal emojis.", e.Description);
                    return res;
                }
            }
        }
        return await Go(fields);
    }

    private async Task<JsonElement> SendAsync(string method, HttpContent content, CancellationToken ct)
    {
        HttpResponseMessage res;
        // فایل‌های بزرگ (بخش‌های پشتیبان) زمان بیشتری برای آپلود می‌خواهند
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(content is MultipartFormDataContent ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(100));
        try { res = await http.PostAsync(Url(method), content, limit.Token); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new TelegramApiException(0, "ارتباط با تلگرام برقرار نشد: " + e.Message);
        }
        using (res)
        {
            using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(limit.Token), cancellationToken: limit.Token);
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean())
                return root.TryGetProperty("result", out var r) ? r.Clone() : default;

            var code = root.TryGetProperty("error_code", out var c) ? c.GetInt32() : (int)res.StatusCode;
            var desc = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "خطای نامشخص";
            var retry = root.TryGetProperty("parameters", out var p) && p.TryGetProperty("retry_after", out var ra) ? ra.GetInt32() : 0;
            throw new TelegramApiException(code, desc, retry);
        }
    }

    public async Task<JsonElement> GetMeAsync()
    {
        var me = await CallAsync("getMe");
        Username = me.GetProperty("username").GetString();
        return me;
    }

    // ---------- میان‌برهای رایج ----------
    public Task<JsonElement> SendMessageAsync(long chatId, string html, object? markup = null, string? effectId = null, CancellationToken ct = default) =>
        CallAsync("sendMessage", new
        {
            ChatId = chatId, Text = html, ParseMode = "HTML", ReplyMarkup = markup, MessageEffectId = effectId,
            LinkPreviewOptions = new { IsDisabled = true },
        }, ct);

    public Task<JsonElement> SendPhotoAsync(long chatId, byte[] photo, string captionHtml, object? markup = null, string? effectId = null, CancellationToken ct = default) =>
        CallMultipartAsync("sendPhoto", new()
        {
            ["chat_id"] = chatId.ToString(), ["caption"] = captionHtml, ["parse_mode"] = "HTML",
            ["reply_markup"] = markup is null ? null : JsonSerializer.Serialize(markup, Json), ["message_effect_id"] = effectId,
        }, "photo", "photo.png", photo, "image/png", ct);

    public Task<JsonElement> SendDocumentAsync(long chatId, byte[] bytes, string fileName, string captionHtml, CancellationToken ct = default, string contentType = "application/pdf") =>
        CallMultipartAsync("sendDocument", new() { ["chat_id"] = chatId.ToString(), ["caption"] = captionHtml, ["parse_mode"] = "HTML" },
            "document", fileName, bytes, contentType, ct);

    public Task AnswerCallbackAsync(string id, string? text = null, bool alert = false, CancellationToken ct = default) =>
        CallAsync("answerCallbackQuery", new { CallbackQueryId = id, Text = text, ShowAlert = alert ? true : (bool?)null }, ct);

    public static string Escape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}
