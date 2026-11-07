using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

[ApiController, Route("api/admin/movies"), Authorize(Roles = Roles.Admin)]
public class AdminMoviesController(AppDbContext db, TmdbClient tmdb, MovieImages images) : ControllerBase
{
    private const int MaxPosterBytes = 5 * 1024 * 1024;
    private const int MaxBackdropBytes = 10 * 1024 * 1024;

    [HttpGet]
    public async Task<object> List()
    {
        var now = DateTime.Now;
        return await db.Movies.OrderBy(m => m.Title).Select(m => new
        {
            m.Id, m.Title, m.OriginalTitle, m.ReleaseYear, m.TmdbId, m.Description, m.Genre, m.AgeRating, m.SpecialNote, m.DurationMinutes,
            m.PosterUrl, m.BackdropUrl,
            upcomingShowtimes = db.Showtimes.Count(s => s.MovieId == m.Id && s.StartsAt > now),
            totalShowtimes = db.Showtimes.Count(s => s.MovieId == m.Id),
            bookings = db.Bookings.Count(b => b.Showtime!.MovieId == m.Id),
            nextStartsAt = db.Showtimes.Where(s => s.MovieId == m.Id && s.StartsAt > now).Min(s => (DateTime?)s.StartsAt)
        }).ToListAsync();
    }

    [HttpPost]
    public async Task<IActionResult> Create(MovieDto dto)
    {
        var m = new Movie();
        Apply(m, dto);
        db.Movies.Add(m);
        await db.SaveChangesAsync();

        var warnings = await ImportFromTmdb(m, dto);
        await db.SaveChangesAsync(); // آدرس تصویرهای واردشده (هر کدام که موفق بودند) ذخیره شود
        return Ok(new { m.Id, warnings });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MovieDto dto)
    {
        var m = await db.Movies.FindAsync(id);
        if (m is null) return NotFound();
        var (oldPoster, oldBackdrop) = (m.PosterUrl, m.BackdropUrl);

        Apply(m, dto);
        // تصویر قبلی که با آدرس دیگری جایگزین یا خالی شده، از سرور پاک می‌شود
        if (m.PosterUrl != oldPoster) images.DeleteIfManaged(oldPoster);
        if (m.BackdropUrl != oldBackdrop) images.DeleteIfManaged(oldBackdrop);

        var warnings = await ImportFromTmdb(m, dto);
        await db.SaveChangesAsync();
        return Ok(new { warnings });
    }

    /// <summary>حذف فیلم؛ کاور و بک‌دراپ ذخیره‌شده روی سرور هم همراهش پاک می‌شوند.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await db.Movies.FindAsync(id);
        if (m is null) return NotFound();
        if (await db.Showtimes.AnyAsync(s => s.MovieId == id))
            return Conflict(new { message = "برای این فیلم سانس ثبت شده است؛ ابتدا سانس‌ها را حذف کنید." });

        var (poster, backdrop) = (m.PosterUrl, m.BackdropUrl);
        db.Movies.Remove(m);
        await db.SaveChangesAsync();
        images.DeleteIfManaged(poster);
        images.DeleteIfManaged(backdrop);
        return NoContent();
    }

    // ---------- آپلود دستی تصویر (وقتی TMDB در دسترس نیست) ----------
    [HttpPost("{id:int}/images/{kind}")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int id, string kind, IFormFile file)
    {
        if (kind is not ("poster" or "backdrop")) return NotFound();
        var m = await db.Movies.FindAsync(id);
        if (m is null) return NotFound();

        var limit = kind == "poster" ? MaxPosterBytes : MaxBackdropBytes;
        if (file is null || file.Length == 0) return BadRequest(new { message = "فایلی انتخاب نشده است." });
        if (file.Length > limit) return BadRequest(new { message = $"حجم تصویر نباید بیشتر از {limit / 1024 / 1024} مگابایت باشد." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        string url;
        try { url = await images.SaveAsync(ms.ToArray()); }
        catch (InvalidDataException e) { return BadRequest(new { message = e.Message }); }

        SetImage(m, kind, url);
        await db.SaveChangesAsync();
        return Ok(new { url });
    }

    [HttpDelete("{id:int}/images/{kind}")]
    public async Task<IActionResult> RemoveImage(int id, string kind)
    {
        if (kind is not ("poster" or "backdrop")) return NotFound();
        var m = await db.Movies.FindAsync(id);
        if (m is null) return NotFound();
        SetImage(m, kind, "");
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- کمکی‌ها ----------
    /// <summary>مقدار تصویر را عوض می‌کند و فایل مدیریت‌شدهٔ قبلی را پاک می‌کند.</summary>
    private void SetImage(Movie m, string kind, string url)
    {
        var old = kind == "poster" ? m.PosterUrl : m.BackdropUrl;
        if (kind == "poster") m.PosterUrl = url; else m.BackdropUrl = url;
        if (old != url) images.DeleteIfManaged(old);
    }

    /// <summary>فقط آدرس http(s) یا همان فایل ذخیره‌شدهٔ خودِ فیلم پذیرفته می‌شود؛ مسیر دلخواه روی سرور نه.</summary>
    private static string CleanUrl(string? incoming, string current)
    {
        var v = incoming?.Trim() ?? "";
        if (v == "") return "";
        if (v == current) return current;
        return Uri.TryCreate(v, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps) ? v : current;
    }

    private static readonly HashSet<string> AllowedAges = ["", "all", "7", "12", "15", "18"];

    /// <summary>ژانرها را تمیز می‌کند: حداکثر ۳ مورد یکتا، بدون ویرگول داخلشان.</summary>
    private static string NormalizeGenres(string? raw) =>
        string.Join(",", (raw ?? "").Split(',', '،').Select(g => g.Trim()).Where(g => g != "").Distinct().Take(3));

    private static void Apply(Movie m, MovieDto d)
    {
        m.Title = d.Title.Trim();
        m.OriginalTitle = d.OriginalTitle?.Trim() ?? "";
        m.Description = d.Description?.Trim() ?? "";
        m.Genre = NormalizeGenres(d.Genre);
        m.AgeRating = AllowedAges.Contains(d.AgeRating ?? "") ? d.AgeRating ?? "" : "";
        m.SpecialNote = d.SpecialNote?.Trim() ?? "";
        m.DurationMinutes = d.DurationMinutes;
        m.ReleaseYear = d.ReleaseYear;
        m.TmdbId = d.TmdbId;
        m.PosterUrl = CleanUrl(d.PosterUrl, m.PosterUrl);
        m.BackdropUrl = CleanUrl(d.BackdropUrl, m.BackdropUrl);
    }

    /// <summary>تصاویر انتخاب‌شده از TMDB را دانلود و روی سرور ذخیره می‌کند؛ خطا فقط هشدار است و فیلم ذخیره می‌شود.</summary>
    private async Task<List<string>> ImportFromTmdb(Movie m, MovieDto d)
    {
        var warnings = new List<string>();
        async Task One(string? path, string size, string kind, string label)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try { SetImage(m, kind, await images.SaveAsync(await tmdb.DownloadImageAsync(path, size))); }
            catch (TmdbException e) { warnings.Add($"{label}: {e.Message}"); }
            catch (InvalidDataException e) { warnings.Add($"{label}: {e.Message}"); }
        }
        await One(d.TmdbPosterPath, "w500", "poster", "کاور");
        await One(d.TmdbBackdropPath, "w1280", "backdrop", "بک‌دراپ");
        return warnings;
    }
}
