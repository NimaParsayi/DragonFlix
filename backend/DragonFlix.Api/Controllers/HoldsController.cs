using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers;

/// <summary>نگه‌داشتن موقت صندلی‌ها برای کاربری که در حال رزرو است (وضعیت «در حال رزرو» برای بقیه).</summary>
[ApiController, Route("api/showtimes/{id:int}/holds"), Authorize]
public class HoldsController(AppDbContext db) : ControllerBase
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private const int MaxSeats = 8;

    /// <summary>
    /// مجموعهٔ صندلی‌های نگه‌داشته‌شدهٔ کاربر را کامل جایگزین می‌کند (لیست خالی = آزادسازی).
    /// صندلی‌هایی که رزرو، غیرفعال یا در اختیار دیگری هستند در unavailable برمی‌گردند.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Put(int id, HoldRequestDto dto)
    {
        var me = await db.Users.FindAsync(User.GetUserId());
        if (me is null || !me.IsRegistered)
            return StatusCode(403, new { code = "profile_required", message = "قبل از رزرو باید نام و نام خانوادگیت رو ثبت کنی." });

        var seats = (dto.Seats ?? new()).DistinctBy(s => (s.RowId, s.Number)).ToList();
        if (seats.Count > MaxSeats)
            return BadRequest(new { message = $"حداکثر {MaxSeats} صندلی قابل انتخاب است." });

        var showtime = await db.Showtimes.Include(s => s.Hall).ThenInclude(h => h!.Rows)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (showtime is null) return NotFound();
        if (showtime.StartsAt <= DateTime.Now) return BadRequest(new { message = "زمان این سانس گذشته است." });

        var uid = User.GetUserId();
        var now = DateTime.UtcNow;

        db.SeatHolds.RemoveRange(await db.SeatHolds
            .Where(h => h.ShowtimeId == id && (h.ExpiresAt <= now || h.UserId == uid)).ToListAsync());
        await db.SaveChangesAsync();

        var rows = showtime.Hall!.Rows.ToDictionary(r => r.Id);
        var booked = (await db.BookedSeats.Where(b => b.ShowtimeId == id)
            .Select(b => new { b.SeatRowId, b.SeatNumber }).ToListAsync())
            .Select(b => (b.SeatRowId, b.SeatNumber)).ToHashSet();
        var heldByOthers = (await db.SeatHolds.Where(h => h.ShowtimeId == id)
            .Select(h => new { h.SeatRowId, h.SeatNumber }).ToListAsync())
            .Select(h => (h.SeatRowId, h.SeatNumber)).ToHashSet();

        var unavailable = new List<SeatSelectionDto>();
        var expires = now + Ttl;
        foreach (var s in seats)
        {
            var ok = rows.TryGetValue(s.RowId, out var row)
                     && s.Number >= 1 && s.Number <= row.SeatCount
                     && !CatalogController.ParseAisles(row.DisabledSeats).Contains(s.Number)
                     && !booked.Contains((s.RowId, s.Number))
                     && !heldByOthers.Contains((s.RowId, s.Number));
            if (!ok) { unavailable.Add(s); continue; }
            db.SeatHolds.Add(new SeatHold { ShowtimeId = id, SeatRowId = s.RowId, SeatNumber = s.Number, UserId = uid, ExpiresAt = expires });
        }

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // هم‌زمانی با کاربر دیگر؛ کلاینت وضعیت را دوباره می‌خواند.
            return Conflict(new { message = "برخی صندلی‌ها هم‌اکنون توسط کاربر دیگری نگه داشته شد." });
        }

        var held = seats.Count - unavailable.Count;
        return Ok(new { expiresAt = held > 0 ? expires : (DateTime?)null, unavailable });
    }
}
