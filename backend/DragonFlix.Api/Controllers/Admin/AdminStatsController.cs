using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using DragonFlix.Api.Services.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

[ApiController, Route("api/admin/stats"), Authorize(Roles = Roles.Admin)]
public class AdminStatsController(AppDbContext db, AppSettings settings, TelegramApi tg) : ControllerBase
{
    [HttpGet]
    public async Task<object> Get()
    {
        var now = DateTime.Now;
        var today = now.Date;

        var webUrl = await settings.GetWebAppUrlAsync();
        var payCfg = await settings.GetPaymentAsync();
        var upcoming = await db.Showtimes.Where(s => s.StartsAt > now).OrderBy(s => s.StartsAt).Take(6)
            .Select(s => new
            {
                s.Id, s.StartsAt, s.HallId,
                movie = s.Movie!.Title, cinema = s.Hall!.Cinema!.Name, hall = s.Hall.Name,
                booked = db.BookedSeats.Count(b => b.ShowtimeId == s.Id)
            }).ToListAsync();
        var caps = await HallCapacity.ForHalls(db, upcoming.Select(u => u.HallId));

        return new
        {
            paymentConfigured = payCfg.Configured,
            botReady = tg.Configured,
            webAppReady = webUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
            cinemas = await db.Cinemas.CountAsync(),
            halls = await db.Halls.CountAsync(),
            hallsWithoutLayout = await db.Halls.CountAsync(h => !h.Rows.Any()),
            movies = await db.Movies.CountAsync(),
            upcomingShowtimes = await db.Showtimes.CountAsync(s => s.StartsAt > now),
            bookingsTotal = await db.Bookings.CountAsync(b => b.Status == BookingStatus.Confirmed),
            pendingReview = await db.Bookings.CountAsync(b => b.Status == BookingStatus.AwaitingReview),
            awaitingPayment = await db.Bookings.CountAsync(b => b.Status == BookingStatus.PendingPayment),
            bookingsToday = await db.Bookings.CountAsync(b => b.Status == BookingStatus.Confirmed && b.CreatedAt >= today),
            checkedInToday = await db.Bookings.CountAsync(b => b.CheckedInAt >= today),
            // SQLite جمع decimal را پشتیبانی نمی‌کند؛ برای آمار نمایشی double کافی است.
            revenueTotal = await db.Bookings.Where(b => b.Status == BookingStatus.Confirmed).SumAsync(b => (double?)b.TotalPrice) ?? 0,
            next = upcoming.Select(u => new
            {
                u.Id, u.StartsAt, u.movie, u.cinema, u.hall, u.booked, capacity = caps.GetValueOrDefault(u.HallId)
            })
        };
    }
}
