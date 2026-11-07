using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace DragonFlix.Api.Services;

public class TmdbOptions
{
    /// <summary>کلید v3 (api_key) یا توکن v4 (Read Access Token که با eyJ شروع می‌شود).</summary>
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3";
    public string ImageBaseUrl { get; set; } = "https://image.tmdb.org/t/p";
    public string Language { get; set; } = "fa-IR";
}

public class TmdbException(string message, int status = 503) : Exception(message)
{
    public int Status { get; } = status;
}

public record TmdbSearchItem(int TmdbId, string Title, string OriginalTitle, int? Year, string Overview, string? PosterPath);

public record TmdbMovieInfo(int TmdbId, string Title, string OriginalTitle, string Overview, bool OverviewFallback,
    List<string> Genres, int DurationMinutes, int? Year, string? PosterPath, string? BackdropPath, string AgeRating);

/// <summary>
/// کلاینت ساده برای TMDB. همهٔ خطاها (نبودن کلید، قطع بودن شبکه، فیلترشدن) به TmdbException با پیام فارسی تبدیل می‌شوند
/// تا بقیهٔ سیستم بتواند به ورود دستی برگردد.
/// </summary>
public partial class TmdbClient(HttpClient http, IOptions<TmdbOptions> options)
{
    private const int MaxImageBytes = 10 * 1024 * 1024;
    private TmdbOptions O => options.Value;

    public bool Configured => !string.IsNullOrWhiteSpace(O.ApiKey);
    public string ImageBase => O.ImageBaseUrl.TrimEnd('/');

    // شناسهٔ ژانر TMDB → نام فارسی یکدست در سایت
    private static readonly Dictionary<int, string> Genres = new()
    {
        [28] = "اکشن", [12] = "ماجراجویی", [16] = "انیمیشن", [35] = "کمدی", [80] = "جنایی", [99] = "مستند",
        [18] = "درام", [10751] = "خانوادگی", [14] = "فانتزی", [36] = "تاریخی", [27] = "ترسناک", [10402] = "موسیقی",
        [9648] = "معمایی", [10749] = "عاشقانه", [878] = "علمی‌تخیلی", [10770] = "تلویزیونی", [53] = "هیجان‌انگیز",
        [10752] = "جنگی", [37] = "وسترن",
    };

    [GeneratedRegex(@"^/[A-Za-z0-9_-]+\.(jpg|jpeg|png)$")]
    private static partial Regex ImagePath();

    public async Task<List<TmdbSearchItem>> SearchAsync(string query)
    {
        using var doc = await Get("/search/movie", O.Language, ("query", query), ("include_adult", "false"), ("page", "1"));
        var list = new List<TmdbSearchItem>();
        if (!doc.RootElement.TryGetProperty("results", out var results)) return list;
        foreach (var r in results.EnumerateArray().Take(10))
            list.Add(new TmdbSearchItem(r.GetProperty("id").GetInt32(), Str(r, "title"), Str(r, "original_title"),
                Year(Str(r, "release_date")), Str(r, "overview"), NullIfEmpty(Str(r, "poster_path"))));
        return list;
    }

    public async Task<TmdbMovieInfo> DetailsAsync(int id)
    {
        using var doc = await Get($"/movie/{id}", O.Language, ("append_to_response", "release_dates"));
        var d = doc.RootElement;
        var overview = Str(d, "overview");
        var fallback = false;
        if (string.IsNullOrWhiteSpace(overview))
        {
            // خلاصهٔ فارسی ندارد → انگلیسی (کاربر می‌تواند ویرایش کند)
            try { using var en = await Get($"/movie/{id}", "en-US"); overview = Str(en.RootElement, "overview"); fallback = overview != ""; }
            catch (TmdbException) { /* بدون خلاصه ادامه می‌دهیم */ }
        }

        // حداکثر ۳ ژانر اول، با نام فارسی یکدست سایت
        var genres = new List<string>();
        if (d.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array)
            foreach (var x in g.EnumerateArray().Take(3))
                genres.Add(Genres.TryGetValue(x.GetProperty("id").GetInt32(), out var fa) ? fa : Str(x, "name"));

        var runtime = d.TryGetProperty("runtime", out var rt) && rt.ValueKind == JsonValueKind.Number ? rt.GetInt32() : 0;
        return new TmdbMovieInfo(id, Str(d, "title"), Str(d, "original_title"), overview, fallback, genres, runtime,
            Year(Str(d, "release_date")), NullIfEmpty(Str(d, "poster_path")), NullIfEmpty(Str(d, "backdrop_path")), AgeFrom(d));
    }

    /// <summary>
    /// رده سنی تقریبی از روی رتبه‌بندی آمریکا (اگر نبود، اولین رتبهٔ موجود). نگاشت: G→همه، PG→۷+، PG-13→۱۲+، TV-14→۱۵+، R و NC-17→۱۸+.
    /// فقط پیشنهاد است و ادمین می‌تواند آن را تغییر دهد.
    /// </summary>
    private static string AgeFrom(JsonElement d)
    {
        if (!d.TryGetProperty("release_dates", out var rd) || !rd.TryGetProperty("results", out var results)) return "";
        string? us = null, other = null;
        foreach (var c in results.EnumerateArray())
        {
            if (!c.TryGetProperty("release_dates", out var list)) continue;
            var cert = list.EnumerateArray().Select(x => Str(x, "certification")).FirstOrDefault(x => x != "");
            if (cert is null) continue;
            if (Str(c, "iso_3166_1") == "US") us = cert; else other ??= cert;
        }
        return (us ?? other ?? "").Trim().ToUpperInvariant() switch
        {
            "G" or "TV-G" or "TV-Y" or "U" or "0" => "all",
            "PG" or "TV-PG" or "TV-Y7" or "7" => "7",
            "PG-13" or "12" or "12A" or "13" => "12",
            "TV-14" or "15" or "16" => "15",
            "R" or "NC-17" or "TV-MA" or "18" or "X" => "18",
            _ => "",
        };
    }

    /// <summary>تصویر را از CDN تی‌ام‌دی‌بی می‌گیرد (size مثلاً w500 یا w1280).</summary>
    public async Task<byte[]> DownloadImageAsync(string path, string size)
    {
        if (!ImagePath().IsMatch(path)) throw new TmdbException("مسیر تصویر نامعتبر است.", 400);
        try
        {
            using var res = await http.GetAsync($"{ImageBase}/{size}{path}", HttpCompletionOption.ResponseHeadersRead);
            if (!res.IsSuccessStatusCode) throw new TmdbException($"دانلود تصویر ناموفق بود ({(int)res.StatusCode}).", 502);
            if (res.Content.Headers.ContentLength > MaxImageBytes) throw new TmdbException("حجم تصویر بیش از حد مجاز است.", 502);
            var bytes = await res.Content.ReadAsByteArrayAsync();
            if (bytes.Length > MaxImageBytes) throw new TmdbException("حجم تصویر بیش از حد مجاز است.", 502);
            return bytes;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new TmdbException("دانلود تصویر از TMDB ممکن نشد؛ دسترسی سرور به TMDB را بررسی کنید.");
        }
    }

    private async Task<JsonDocument> Get(string path, string language, params (string Key, string Value)[] query)
    {
        if (!Configured) throw new TmdbException("کلید API سرویس TMDB تنظیم نشده است.");
        var key = O.ApiKey.Trim();
        var bearer = key.StartsWith("eyJ", StringComparison.Ordinal);

        var qs = new List<string> { $"language={Uri.EscapeDataString(language)}" };
        qs.AddRange(query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"));
        if (!bearer) qs.Add($"api_key={Uri.EscapeDataString(key)}");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{O.BaseUrl.TrimEnd('/')}{path}?{string.Join('&', qs)}");
        if (bearer) req.Headers.Authorization = new("Bearer", key);

        try
        {
            using var res = await http.SendAsync(req);
            if ((int)res.StatusCode == 401) throw new TmdbException("کلید API سرویس TMDB نامعتبر است.", 502);
            if ((int)res.StatusCode == 404) throw new TmdbException("موردی در TMDB پیدا نشد.", 404);
            if (!res.IsSuccessStatusCode) throw new TmdbException($"سرویس TMDB خطا داد ({(int)res.StatusCode}).", 502);
            return await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new TmdbException("ارتباط با TMDB برقرار نشد؛ اینترنت یا دسترسی سرور به TMDB را بررسی کنید.");
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    private static int? Year(string date) => date.Length >= 4 && int.TryParse(date.AsSpan(0, 4), out var y) ? y : null;
}
