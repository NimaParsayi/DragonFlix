using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

/// <summary>فهرست بلیت‌های فروخته‌شده با فیلتر و صفحه‌بندی (برای ادمین).</summary>
[ApiController, Route("api/admin/bookings"), Authorize(Roles = Roles.Admin)]
public class AdminBookingsController(AppDbContext db) : ControllerBase
{
    /// <param name="status">all | used (ورود ثبت شده) | valid (استفاده‌نشده و هنوز معتبر) | expired (استفاده‌نشده و سانس تمام شده)</param>
    [HttpGet]
    public async Task<IActionResult> List(string? q, int? movieId, int? cinemaId, int? showtimeId,
        string status = "all", string pay = "confirmed", int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        var now = DateTime.Now;

        var query = db.Bookings.AsQueryable();
        // وضعیت پرداخت: confirmed | awaiting | pending | rejected | expired | cancelled | all
        query = pay switch
        {
            "all" => query,
            "awaiting" => query.Where(b => b.Status == BookingStatus.AwaitingReview),
            "pending" => query.Where(b => b.Status == BookingStatus.PendingPayment),
            "rejected" => query.Where(b => b.Status == BookingStatus.Rejected),
            "expired" => query.Where(b => b.Status == BookingStatus.Expired),
            "cancelled" => query.Where(b => b.Status == BookingStatus.Cancelled),
            _ => query.Where(b => b.Status == BookingStatus.Confirmed),
        };
        if (movieId.HasValue) query = query.Where(b => b.Showtime!.MovieId == movieId);
        if (cinemaId.HasValue) query = query.Where(b => b.Showtime!.Hall!.CinemaId == cinemaId);
        if (showtimeId.HasValue) query = query.Where(b => b.ShowtimeId == showtimeId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            var code = new string(t.Select(ch => ch is >= '۰' and <= '۹' ? (char)('0' + (ch - '۰')) : ch).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            query = query.Where(b => b.User!.FullName.Contains(t) || b.User.Email.Contains(t) || (code != "" && b.Code.Contains(code)));
        }
        query = status switch
        {
            "used" => query.Where(b => b.CheckedInAt != null),
            "valid" => query.Where(b => b.CheckedInAt == null && b.Showtime!.StartsAt.AddMinutes(b.Showtime.Movie!.DurationMinutes) >= now),
            "expired" => query.Where(b => b.CheckedInAt == null && b.Showtime!.StartsAt.AddMinutes(b.Showtime.Movie!.DurationMinutes) < now),
            _ => query,
        };

        var total = await query.CountAsync();
        var summary = new
        {
            bookings = total,
            seats = await query.SumAsync(b => (int?)b.Seats.Count) ?? 0,
            revenue = await query.SumAsync(b => (double?)b.TotalPrice) ?? 0,
            checkedIn = await query.CountAsync(b => b.CheckedInAt != null),
        };

        var rows = await query.OrderByDescending(b => b.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new
            {
                b.Id, b.Code, b.TotalPrice, b.CreatedAt, b.CheckedInAt, payStatus = b.Status, b.SeatsText,
                buyer = b.User!.FullName, email = b.User.Email,
                showtimeId = b.ShowtimeId, startsAt = b.Showtime!.StartsAt,
                movieId = b.Showtime.MovieId, movie = b.Showtime.Movie!.Title, duration = b.Showtime.Movie.DurationMinutes,
                cinema = b.Showtime.Hall!.Cinema!.Name, hall = b.Showtime.Hall.Name,
                seats = b.Seats.OrderBy(s => s.SeatRow!.SortOrder).ThenBy(s => s.SeatNumber)
                    .Select(s => new { row = s.SeatRow!.Label, number = s.SeatNumber })
            }).ToListAsync();

        var items = rows.Select(r => new
        {
            r.Id, r.Code, r.TotalPrice, r.CreatedAt, r.CheckedInAt, r.payStatus, r.SeatsText, r.buyer, r.email,
            r.showtimeId, r.startsAt, r.movieId, r.movie, r.cinema, r.hall, r.seats,
            status = r.payStatus != BookingStatus.Confirmed ? "unpaid" : r.CheckedInAt != null ? "used" : r.startsAt.AddMinutes(r.duration) < now ? "expired" : "valid"
        });

        object? showtime = null;
        if (showtimeId.HasValue)
            showtime = await db.Showtimes.Where(s => s.Id == showtimeId).Select(s => new
            {
                s.Id, s.StartsAt, movie = s.Movie!.Title, cinema = s.Hall!.Cinema!.Name, hall = s.Hall.Name
            }).FirstOrDefaultAsync();

        return Ok(new { items, total, page, pageSize, pages = (int)Math.Ceiling(total / (double)pageSize), summary, showtime });
    }
}
