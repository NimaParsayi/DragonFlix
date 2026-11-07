using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

/// <summary>بررسی بلیت با کد/QR و ثبت ورود (برای مسئول ورودی سینما).</summary>
[ApiController, Route("api/admin/tickets"), Authorize(Roles = Roles.Admin)]
public class AdminTicketsController(AppDbContext db) : ControllerBase
{
    /// <summary>ورود از این مدت پیش از شروع سانس مجاز است.</summary>
    private static readonly TimeSpan EarlyWindow = TimeSpan.FromHours(2);

    [HttpGet("{code}")]
    public async Task<IActionResult> Check(string code)
    {
        var t = await Find(code);
        return t is null ? NotFound(new { message = "بلیتی با این کد پیدا نشد." }) : Ok(t);
    }

    /// <summary>ورود را ثبت می‌کند. اگر هم‌زمان در دو دستگاه اسکن شود، فقط یکی موفق می‌شود.</summary>
    [HttpPost("{code}/checkin")]
    public async Task<IActionResult> CheckIn(string code)
    {
        var t = await Find(code);
        if (t is null) return NotFound(new { message = "بلیتی با این کد پیدا نشد." });
        if (t.Status != "valid") return Conflict(new { message = StatusMessage(t.Status), ticket = t });

        var rows = await db.Bookings.Where(b => b.Id == t.Id && b.CheckedInAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.CheckedInAt, DateTime.Now));
        var fresh = await Find(code);
        if (rows == 0) return Conflict(new { message = StatusMessage("used"), ticket = fresh });
        return Ok(fresh);
    }

    /// <summary>ارقام فارسی (۰-۹) و عربی (٠-٩) را به ASCII تبدیل می‌کند.</summary>
    private static char ToAsciiDigit(char ch) => ch switch
    {
        >= '۰' and <= '۹' => (char)('0' + (ch - '۰')),
        >= '٠' and <= '٩' => (char)('0' + (ch - '٠')),
        _ => ch,
    };

    private static string StatusMessage(string s) => s switch
    {
        "used" => "این بلیت قبلاً استفاده شده است.",
        "expired" => "زمان این سانس گذشته است.",
        "early" => "ورود برای این سانس هنوز شروع نشده است.",
        "unpaid" => "پرداخت این رزرو هنوز تأیید نشده است.",
        _ => "بلیت معتبر نیست.",
    };

    private record Row(int Id, string Code, string PayStatus, decimal TotalPrice, DateTime CreatedAt, DateTime? CheckedInAt, string Buyer,
        DateTime StartsAt, int Duration, string Movie, string Cinema, string Hall, IEnumerable<object> Seats);

    private async Task<TicketInfo?> Find(string code)
    {
        var c = new string(code.Select(ToAsciiDigit).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var r = await db.Bookings.Where(x => x.Code == c).Select(x => new Row(
            x.Id, x.Code, x.Status, x.TotalPrice, x.CreatedAt, x.CheckedInAt, x.User!.FullName,
            x.Showtime!.StartsAt, x.Showtime.Movie!.DurationMinutes, x.Showtime.Movie.Title,
            x.Showtime.Hall!.Cinema!.Name, x.Showtime.Hall.Name,
            x.Seats.OrderBy(s => s.SeatRow!.SortOrder).ThenBy(s => s.SeatNumber)
                .Select(s => (object)new { row = s.SeatRow!.Label, number = s.SeatNumber }))).FirstOrDefaultAsync();
        if (r is null) return null;

        var now = DateTime.Now;
        var status = r.PayStatus != BookingStatus.Confirmed ? "unpaid"
            : r.CheckedInAt is not null ? "used"
            : now > r.StartsAt.AddMinutes(r.Duration) ? "expired"
            : now < r.StartsAt - EarlyWindow ? "early"
            : "valid";
        return new TicketInfo(r.Id, r.Code, r.TotalPrice, r.CreatedAt, r.CheckedInAt, r.Buyer, r.StartsAt,
            r.Movie, r.Cinema, r.Hall, r.Seats, status);
    }

    public record TicketInfo(int Id, string Code, decimal TotalPrice, DateTime CreatedAt, DateTime? CheckedInAt, string Buyer,
        DateTime StartsAt, string Movie, string Cinema, string Hall, IEnumerable<object> Seats, string Status);
}
