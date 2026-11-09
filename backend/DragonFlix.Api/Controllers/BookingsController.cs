using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers;

public class ReceiptForm
{
    public string? TrackingCode { get; set; }
    public string? PayerCard4 { get; set; }
    public IFormFile? File { get; set; }
}

[ApiController, Route("api/bookings"), Authorize]
public class BookingsController(AppDbContext db, AppSettings settings, PaymentService payments, ReceiptStore receipts) : ControllerBase
{
    private const int MaxSeatsPerBooking = 8;

    [HttpGet("mine")]
    public async Task<object> Mine()
    {
        var uid = User.GetUserId();
        var serverNow = DateTime.Now;
        var list = await db.Bookings.Where(b => b.UserId == uid)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                serverNow, b.Id, b.Code, b.CreatedAt, b.TotalPrice, b.CheckedInAt, b.Status, b.PayDeadline, b.RejectReason, b.SeatsText,
                movieId = b.Showtime!.MovieId, genre = b.Showtime.Movie!.Genre, posterUrl = b.Showtime.Movie.PosterUrl,
                showtimeId = b.ShowtimeId, startsAt = b.Showtime.StartsAt,
                movie = b.Showtime.Movie.Title,
                cinema = b.Showtime.Hall!.Cinema!.Name,
                hall = b.Showtime.Hall.Name,
                buyer = b.User!.RegFirstName + " " + b.User.RegLastName,
                seats = b.Seats.OrderBy(s => s.SeatRow!.SortOrder).ThenBy(s => s.SeatNumber)
                    .Select(s => new { row = s.SeatRow!.Label, number = s.SeatNumber, s.Price })
            }).ToListAsync();
        return list;
    }

    /// <summary>
    /// رزرو را با وضعیت «منتظر پرداخت» می‌سازد و صندلی‌ها را تا پایان مهلت برای کاربر نگه می‌دارد.
    /// بلیت فقط پس از تأیید پرداخت توسط مدیر صادر می‌شود.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateBookingDto dto)
    {
        if (dto.Seats is not { Count: > 0 })
            return BadRequest(new { message = "حداقل یه صندلی انتخاب کن." });
        if (dto.Seats.Count > MaxSeatsPerBooking)
            return BadRequest(new { message = $"حداکثر {MaxSeatsPerBooking} صندلی در هر رزرو مجاز است." });
        if (dto.Seats.Select(s => (s.RowId, s.Number)).Distinct().Count() != dto.Seats.Count)
            return BadRequest(new { message = "صندلی تکراری انتخاب شده است." });

        var me = await db.Users.FindAsync(User.GetUserId());
        if (me is null || !me.IsRegistered)
            return StatusCode(403, new { code = "profile_required", message = "قبل از رزرو باید نام و نام خانوادگیت رو ثبت کنی." });

        var pay = await settings.GetPaymentAsync();
        if (!pay.Configured)
            return Conflict(new { message = "پرداخت هنوز راه‌اندازی نشده؛ یکم بعد دوباره امتحان کن." });

        var showtime = await db.Showtimes.Include(s => s.Hall).ThenInclude(h => h!.Rows)
            .FirstOrDefaultAsync(s => s.Id == dto.ShowtimeId);
        if (showtime is null) return NotFound(new { message = "سانس پیدا نشد." });
        if (showtime.StartsAt <= DateTime.Now)
            return BadRequest(new { message = "زمان این سانس گذشته." });

        var rows = showtime.Hall!.Rows.ToDictionary(r => r.Id);
        var booking = new Booking
        {
            UserId = User.GetUserId(), ShowtimeId = showtime.Id, CreatedAt = DateTime.Now,
            Code = await NewCodeAsync(), Status = BookingStatus.PendingPayment,
            PayDeadline = DateTime.Now.AddMinutes(pay.Minutes),
        };

        foreach (var s in dto.Seats)
        {
            if (!rows.TryGetValue(s.RowId, out var row) || s.Number < 1 || s.Number > row.SeatCount)
                return BadRequest(new { message = "صندلی انتخاب‌شده در این سالن وجود ندارد." });
            if (CatalogController.ParseAisles(row.DisabledSeats).Contains(s.Number))
                return BadRequest(new { message = $"صندلی {row.Label}{s.Number} غیرفعال است." });

            booking.Seats.Add(new BookedSeat
            {
                ShowtimeId = showtime.Id, SeatRowId = row.Id, SeatNumber = s.Number,
                Price = row.Price * showtime.PriceFactor
            });
        }
        booking.TotalPrice = booking.Seats.Sum(s => s.Price);
        booking.SeatsText = string.Join(",", dto.Seats
            .Select(s => (Row: rows[s.RowId], s.Number)).OrderBy(x => x.Row.SortOrder).ThenBy(x => x.Number).Select(x => $"{x.Row.Label}:{x.Number}"));

        // بررسی سریع؛ تضمین نهایی با ایندکس یکتا روی (سانس، ردیف، شماره) است.
        var ids = dto.Seats.Select(s => s.RowId).Distinct().ToList();
        var already = await db.BookedSeats
            .Where(b => b.ShowtimeId == showtime.Id && ids.Contains(b.SeatRowId))
            .Select(b => new { b.SeatRowId, b.SeatNumber }).ToListAsync();
        if (already.Any(a => dto.Seats.Any(s => s.RowId == a.SeatRowId && s.Number == a.SeatNumber)))
            return Conflict(new { message = "بعضی از صندلی‌هایی که انتخاب کردی همین الان رزرو شدن." });

        // صندلی‌ای که کاربر دیگری هم‌اکنون در حال رزرو آن است
        var now = DateTime.UtcNow;
        var uid = User.GetUserId();
        var heldByOthers = await db.SeatHolds
            .Where(h => h.ShowtimeId == showtime.Id && h.ExpiresAt > now && h.UserId != uid && ids.Contains(h.SeatRowId))
            .Select(h => new { h.SeatRowId, h.SeatNumber }).ToListAsync();
        if (heldByOthers.Any(a => dto.Seats.Any(s => s.RowId == a.SeatRowId && s.Number == a.SeatNumber)))
            return Conflict(new { message = "بعضی از صندلی‌هایی که انتخاب کردی همین الان دست یه نفر دیگه‌ست." });

        try
        {
            db.SeatHolds.RemoveRange(await db.SeatHolds.Where(h => h.ShowtimeId == showtime.Id && h.UserId == uid).ToListAsync());
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "بعضی از صندلی‌هایی که انتخاب کردی همین الان رزرو شدن." });
        }

        await payments.OnCreatedAsync(booking);
        return Ok(new { booking.Id, booking.Code, booking.TotalPrice, booking.Status, booking.PayDeadline });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var b = await Load(id);
        if (b is null) return NotFound();
        return Ok(new
        {
            b.Id, b.Code, b.CreatedAt, b.TotalPrice, b.CheckedInAt, b.Status, b.PayDeadline, b.RejectReason, b.SeatsText,
            b.TrackingCode, hasReceipt = b.ReceiptFile != "", b.SubmittedAt,
            startsAt = b.Showtime!.StartsAt, movie = b.Showtime.Movie!.Title, movieId = b.Showtime.MovieId,
            genre = b.Showtime.Movie.Genre, posterUrl = b.Showtime.Movie.PosterUrl,
            cinema = b.Showtime.Hall!.Cinema!.Name, hall = b.Showtime.Hall.Name,
            buyer = b.User!.RegisteredName,
            seats = b.Seats.OrderBy(s => s.SeatRow!.SortOrder).ThenBy(s => s.SeatNumber)
                .Select(s => new { row = s.SeatRow!.Label, number = s.SeatNumber, s.Price })
        });
    }

    /// <summary>اطلاعات پرداخت (کارت مقصد و مهلت) برای صاحب رزرو.</summary>
    [HttpGet("{id:int}/payment")]
    public async Task<IActionResult> Payment(int id)
    {
        var b = await db.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId());
        if (b is null) return NotFound();
        var p = await settings.GetPaymentAsync();
        var open = b.Status is BookingStatus.PendingPayment or BookingStatus.AwaitingReview;
        return Ok(new
        {
            b.Status, amount = b.TotalPrice, b.PayDeadline, serverNow = DateTime.Now, b.RejectReason, b.TrackingCode, b.SubmittedAt,
            card = open ? new { number = p.CardNumber, holder = p.CardHolder, bank = BankInfo.NameOf(p.CardNumber, p.CardBank), bankLogo = BankInfo.LogoUrl(p.CardNumber) } : null,
            note = open ? p.Note : "",
        });
    }

    /// <summary>ثبت رسید کارت‌به‌کارت: شمارهٔ پیگیری و/یا عکس رسید. رزرو به «در انتظار تأیید» می‌رود و مدیر خبردار می‌شود.</summary>
    [HttpPost("{id:int}/receipt")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> SubmitReceipt(int id, [FromForm] ReceiptForm form)
    {
        var b = await db.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId());
        if (b is null) return NotFound();
        if (b.Status != BookingStatus.PendingPayment)
            return Conflict(new { message = b.Status == BookingStatus.AwaitingReview ? "رسیدت قبلاً ثبت شده و در حال بررسیه." : "این رزرو دیگر قابل پرداخت نیست." });
        if (b.PayDeadline < DateTime.Now)
            return Conflict(new { message = "مهلت پرداخت تموم شده." });

        var tracking = (form.TrackingCode ?? "").Trim();
        if (tracking.Length > 40) tracking = tracking[..40];
        var card4 = new string((form.PayerCard4 ?? "").Where(char.IsDigit).Take(4).ToArray());
        var hasFile = form.File is { Length: > 0 };
        if (!hasFile)
            return BadRequest(new { message = "عکس رسید پرداخت رو بفرست." });

        var fileName = "";
        if (hasFile)
        {
            if (form.File!.Length > ReceiptStore.MaxBytes) return BadRequest(new { message = "حجم عکس رسید نباید بیشتر از ۶ مگابایت باشه." });
            using var ms = new MemoryStream();
            await form.File.CopyToAsync(ms);
            try { fileName = await receipts.SaveAsync(ms.ToArray()); }
            catch (InvalidDataException e) { return BadRequest(new { message = e.Message }); }
        }

        // انتقال اتمیک: اگر هم‌زمان منقضی یا لغو شده باشد، رسید ثبت نمی‌شود
        var now = DateTime.Now;
        var changed = await db.Bookings.Where(x => x.Id == id && x.Status == BookingStatus.PendingPayment)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingStatus.AwaitingReview).SetProperty(x => x.SubmittedAt, now)
                .SetProperty(x => x.TrackingCode, tracking).SetProperty(x => x.PayerCard4, card4).SetProperty(x => x.ReceiptFile, fileName));
        if (changed == 0) return Conflict(new { message = "وضعیت رزرو عوض شده؛ صفحه رو دوباره باز کن." });

        await payments.OnReceiptSubmittedAsync(id);
        return NoContent();
    }

    /// <summary>عکس رسید؛ فقط صاحب رزرو یا ادمین.</summary>
    [HttpGet("{id:int}/receipt")]
    public async Task<IActionResult> Receipt(int id)
    {
        var uid = User.GetUserId();
        var isAdmin = User.IsInRole(Roles.Admin);
        var file = await db.Bookings.Where(x => x.Id == id && (isAdmin || x.UserId == uid)).Select(x => x.ReceiptFile).FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(file)) return NotFound();
        var r = await receipts.ReadAsync(file);
        return r is null ? NotFound() : File(r.Value.Bytes, r.Value.Mime);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var r = await payments.CancelAsync(id, User.GetUserId());
        return r.Ok ? NoContent() : Conflict(new { message = r.Message });
    }

    /// <summary>رسید/بلیت PDF با کد و QR؛ فقط برای بلیتِ تأییدشده، صاحب رزرو (یا ادمین).</summary>
    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id)
    {
        var b = await Load(id);
        if (b is null) return NotFound();
        if (b.Status != BookingStatus.Confirmed) return Conflict(new { message = "بلیت بعد از تأیید پرداخت صادر می‌شه." });
        return File(TicketPdf.Render(ToTicket(b)), "application/pdf", $"dragonflix-ticket-{b.Code}.pdf");
    }

    public static TicketData ToTicket(Booking b) => new(
        b.Code, b.Showtime!.Movie!.Title, b.Showtime.Hall!.Cinema!.Name, b.Showtime.Hall.Name,
        b.Showtime.StartsAt, b.User!.IsRegistered ? b.User.RegisteredName : b.User.FullName, b.TotalPrice, b.CreatedAt,
        b.Seats.OrderBy(s => s.SeatRow!.SortOrder).ThenBy(s => s.SeatNumber)
            .Select(s => new TicketSeat(s.SeatRow!.Label, s.SeatNumber, s.Price)).ToList(), b.Showtime.Movie.PosterUrl);

    private async Task<Booking?> Load(int id)
    {
        var uid = User.GetUserId();
        var isAdmin = User.IsInRole(Roles.Admin);
        return await db.Bookings
            .Include(x => x.User)
            .Include(x => x.Seats).ThenInclude(s => s.SeatRow)
            .Include(x => x.Showtime).ThenInclude(s => s!.Movie)
            .Include(x => x.Showtime).ThenInclude(s => s!.Hall).ThenInclude(h => h!.Cinema)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id && (isAdmin || x.UserId == uid));
    }

    // حروف و ارقامِ بدون ابهام (بدون O/0 و I/1)
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private async Task<string> NewCodeAsync()
    {
        while (true)
        {
            var code = string.Create(8, 0, (span, _) =>
            {
                for (var i = 0; i < span.Length; i++) span[i] = Alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(Alphabet.Length)];
            });
            if (!await db.Bookings.AnyAsync(b => b.Code == code)) return code;
        }
    }
}
