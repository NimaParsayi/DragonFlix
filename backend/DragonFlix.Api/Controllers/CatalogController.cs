using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers;

/// <summary>بخش عمومی: سینماها، فیلم‌ها، سانس‌ها و نقشهٔ صندلی هر سانس.</summary>
[ApiController, Route("api")]
public class CatalogController(AppDbContext db) : ControllerBase
{
    [HttpGet("cinemas")]
    public async Task<object> Cinemas() =>
        await db.Cinemas.OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.City, c.Address }).ToListAsync();

    [HttpGet("movies")]
    public async Task<object> Movies()
    {
        var now = DateTime.Now;
        return await db.Movies.OrderBy(m => m.Title).Select(m => new
        {
            m.Id, m.Title, m.OriginalTitle, m.ReleaseYear, m.Description, m.Genre, m.AgeRating, m.SpecialNote, m.DurationMinutes, m.PosterUrl, m.BackdropUrl,
            upcomingShowtimes = db.Showtimes.Count(s => s.MovieId == m.Id && s.StartsAt > now),
            nextStartsAt = db.Showtimes.Where(s => s.MovieId == m.Id && s.StartsAt > now).Min(s => (DateTime?)s.StartsAt)
        }).ToListAsync();
    }

    /// <summary>اسلایدهای فعال صفحهٔ اول؛ فیلمی که سانس پیش‌رو ندارد نمایش داده نمی‌شود.</summary>
    [HttpGet("slides")]
    public async Task<object> Slides()
    {
        var now = DateTime.Now;
        var rows = await db.HomeSlides.Where(s => s.Active).OrderBy(s => s.SortOrder).Select(s => new
        {
            s.Id, Note = s.Movie!.SpecialNote,
            movie = new
            {
                s.Movie.Id, s.Movie.Title, s.Movie.OriginalTitle, s.Movie.ReleaseYear, s.Movie.Description, s.Movie.Genre,
                s.Movie.AgeRating, s.Movie.DurationMinutes, s.Movie.PosterUrl, s.Movie.BackdropUrl
            },
            upcoming = db.Showtimes.Count(x => x.MovieId == s.MovieId && x.StartsAt > now),
            nextStartsAt = db.Showtimes.Where(x => x.MovieId == s.MovieId && x.StartsAt > now).Min(x => (DateTime?)x.StartsAt)
        }).ToListAsync();
        return rows.Where(r => r.upcoming > 0).Select(r => new { r.Id, r.Note, r.movie, r.nextStartsAt });
    }

    [HttpGet("movies/{id:int}")]
    public async Task<IActionResult> Movie(int id)
    {
        var m = await db.Movies.FindAsync(id);
        if (m is null) return NotFound();

        var now = DateTime.Now;
        var rows = await db.Showtimes
            .Where(s => s.MovieId == id && s.StartsAt > now)
            .OrderBy(s => s.StartsAt)
            .Select(s => new
            {
                s.Id, s.StartsAt, s.PriceFactor,
                hallId = s.HallId, hallName = s.Hall!.Name,
                cinemaId = s.Hall.CinemaId, cinemaName = s.Hall.Cinema!.Name, city = s.Hall.Cinema.City,
                minPrice = s.Hall.Rows.Min(r => (decimal?)r.Price) * s.PriceFactor,
                maxPrice = s.Hall.Rows.Max(r => (decimal?)r.Price) * s.PriceFactor,
            }).ToListAsync();

        var following = User.Identity?.IsAuthenticated == true && await db.MovieFollows.AnyAsync(f => f.MovieId == id && f.UserId == User.GetUserId());
        return Ok(new { movie = m, showtimes = rows, following });
    }

    [HttpGet("showtimes/{id:int}")]
    public async Task<IActionResult> Showtime(int id)
    {
        var s = await db.Showtimes
            .Include(x => x.Movie).Include(x => x.Hall).ThenInclude(h => h!.Cinema)
            .Include(x => x.Hall).ThenInclude(h => h!.Rows)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();

        var now = DateTime.UtcNow;
        int? uid = User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
        var holds = await db.SeatHolds.Where(h => h.ShowtimeId == id && h.ExpiresAt > now).ToListAsync();
        var mine = holds.Where(h => h.UserId == uid).ToList();
        var others = holds.Where(h => h.UserId != uid).GroupBy(h => h.SeatRowId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SeatNumber).ToList());
        var mineByRow = mine.GroupBy(h => h.SeatRowId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SeatNumber).ToList());

        var taken = (await db.BookedSeats.Where(b => b.ShowtimeId == id)
                .Select(b => new { b.SeatRowId, b.SeatNumber }).ToListAsync())
            .GroupBy(b => b.SeatRowId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SeatNumber).ToList());

        return Ok(new
        {
            id = s.Id, s.StartsAt, s.PriceFactor,
            movie = new { s.Movie!.Id, s.Movie.Title, s.Movie.DurationMinutes, s.Movie.SpecialNote },
            cinema = new { s.Hall!.Cinema!.Id, s.Hall.Cinema.Name },
            hall = new { s.Hall.Id, s.Hall.Name },
            rows = s.Hall.Rows.OrderBy(r => r.SortOrder).Select(r => new
            {
                r.Id, r.Label, r.SeatCount, r.Offset,
                aisleAfter = ParseAisles(r.AisleAfter),
                price = r.Price * s.PriceFactor,
                taken = taken.GetValueOrDefault(r.Id) ?? new List<int>(),
                disabled = ParseAisles(r.DisabledSeats),
                held = others.GetValueOrDefault(r.Id) ?? new List<int>(),
                mine = mineByRow.GetValueOrDefault(r.Id) ?? new List<int>()
            }),
            holdExpiresAt = mine.Count > 0 ? DateTime.SpecifyKind(mine.Max(h => h.ExpiresAt), DateTimeKind.Utc) : (DateTime?)null
        });
    }

    public static List<int> ParseAisles(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Select(int.Parse).ToList();
}
