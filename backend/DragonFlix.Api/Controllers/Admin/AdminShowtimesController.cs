using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

[ApiController, Route("api/admin/showtimes"), Authorize(Roles = Roles.Admin)]
public class AdminShowtimesController(AppDbContext db, ReleaseNotifier release) : ControllerBase
{
    private static readonly TimeSpan CleaningBuffer = TimeSpan.FromMinutes(15);

    [HttpGet]
    public async Task<IEnumerable<object>> List(int? cinemaId, int? hallId, int? movieId, bool upcomingOnly = true)
    {
        var q = db.Showtimes.AsQueryable();
        if (cinemaId.HasValue) q = q.Where(s => s.Hall!.CinemaId == cinemaId);
        if (hallId.HasValue) q = q.Where(s => s.HallId == hallId);
        if (movieId.HasValue) q = q.Where(s => s.MovieId == movieId);
        if (upcomingOnly) { var now = DateTime.Now.AddHours(-3); q = q.Where(s => s.StartsAt > now); }

        var list = await q.OrderBy(s => s.StartsAt).Take(500).Select(s => new
        {
            s.Id, s.StartsAt, s.PriceFactor, s.MovieId, s.HallId,
            movie = s.Movie!.Title, hall = s.Hall!.Name, cinemaId = s.Hall.CinemaId, cinema = s.Hall.Cinema!.Name,
            bookedSeats = db.BookedSeats.Count(b => b.ShowtimeId == s.Id)
        }).ToListAsync();
        var caps = await HallCapacity.ForHalls(db, list.Select(x => x.HallId));
        return list.Select(x => new
        {
            x.Id, x.StartsAt, x.PriceFactor, x.MovieId, x.HallId, x.movie, x.hall, x.cinemaId, x.cinema,
            x.bookedSeats, capacity = caps.GetValueOrDefault(x.HallId)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(ShowtimeInputDto dto)
    {
        var s = new Showtime();
        var err = await Apply(s, dto);
        if (err is not null) return err;
        db.Showtimes.Add(s);
        await db.SaveChangesAsync();
        await release.OnShowtimeCreatedAsync(s.MovieId); // اعلان اکران جدید / سانس تازه
        return Ok(new { s.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ShowtimeInputDto dto)
    {
        var s = await db.Showtimes.FindAsync(id);
        if (s is null) return NotFound();
        if (s.HallId != dto.HallId && await db.BookedSeats.AnyAsync(b => b.ShowtimeId == id))
            return Conflict(new { message = "این سانس رزرو دارد و سالن آن قابل تغییر نیست." });
        var oldStart = s.StartsAt;
        var err = await Apply(s, dto, id);
        if (err is not null) return err;
        await db.SaveChangesAsync();
        if (s.StartsAt != oldStart) await release.OnShowtimeTimeChangedAsync(s.Id, oldStart);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await db.Showtimes.FindAsync(id);
        if (s is null) return NotFound();
        if (await db.Bookings.AnyAsync(b => b.ShowtimeId == id && BookingStatus.HoldsSeats.Contains(b.Status)))
            return Conflict(new { message = "این سانس رزرو دارد و قابل حذف نیست." });
        // رزروهای ردشده/منقضی/لغوشده صندلی نگه نمی‌دارند؛ همراه سانس پاک می‌شوند
        await db.Bookings.Where(b => b.ShowtimeId == id).ExecuteDeleteAsync();
        db.Showtimes.Remove(s);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>اعتبارسنجی و جلوگیری از تداخل دو سانس در یک سالن.</summary>
    private async Task<IActionResult?> Apply(Showtime s, ShowtimeInputDto d, int? selfId = null)
    {
        var movie = await db.Movies.FindAsync(d.MovieId);
        if (movie is null) return BadRequest(new { message = "فیلم نامعتبر است." });
        if (!await db.Halls.AnyAsync(h => h.Id == d.HallId)) return BadRequest(new { message = "سالن نامعتبر است." });

        var start = d.StartsAt;
        var end = start.AddMinutes(movie.DurationMinutes) + CleaningBuffer;

        var sameHall = await db.Showtimes.Include(x => x.Movie)
            .Where(x => x.HallId == d.HallId && x.Id != selfId
                        && x.StartsAt < end && x.StartsAt > start.AddHours(-12))
            .ToListAsync();
        var clash = sameHall.FirstOrDefault(x => x.StartsAt.AddMinutes(x.Movie!.DurationMinutes) + CleaningBuffer > start);
        if (clash is not null)
            return Conflict(new { message = $"با سانس «{clash.Movie!.Title}» ({clash.StartsAt:yyyy-MM-dd HH:mm}) در همین سالن تداخل دارد." });

        s.MovieId = d.MovieId; s.HallId = d.HallId; s.StartsAt = start; s.PriceFactor = d.PriceFactor;
        return null;
    }
}
