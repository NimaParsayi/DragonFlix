using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services.Telegram;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Services;

public record PayResult(bool Ok, string Message, Booking? Booking);

/// <summary>
/// منطق پرداخت کارت‌به‌کارت: انتقال وضعیت‌ها به‌صورت اتمیک (دو ادمین هم‌زمان نمی‌توانند یک رسید را دوبار تأیید کنند)،
/// آزادسازی صندلی‌ها و اعلان‌ها. هم کنترلرها و هم ربات تلگرام از همین‌جا استفاده می‌کنند.
/// </summary>
public class PaymentService(AppDbContext db, AppSettings settings, NotificationService notify)
{
    public Task<Booking?> LoadAsync(int id) => db.Bookings
        .Include(b => b.User)
        .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
        .Include(b => b.Showtime).ThenInclude(s => s!.Hall).ThenInclude(h => h!.Cinema)
        .AsNoTracking() // ExecuteUpdate نمونهٔ ردیابی‌شده را به‌روز نمی‌کند؛ بدون ردیابی همیشه آخرین مقدار خوانده می‌شود
        .AsSplitQuery().FirstOrDefaultAsync(b => b.Id == id);

    private Task ReleaseSeatsAsync(int bookingId) => db.BookedSeats.Where(s => s.BookingId == bookingId).ExecuteDeleteAsync();

    public static string StatusLabel(string s) => s switch
    {
        BookingStatus.PendingPayment => "منتظر پرداخت",
        BookingStatus.AwaitingReview => "در انتظار تأیید",
        BookingStatus.Confirmed => "تأیید‌شده",
        BookingStatus.Rejected => "ردشده",
        BookingStatus.Expired => "منقضی‌شده",
        BookingStatus.Cancelled => "لغوشده",
        _ => s,
    };

    // ---------- بعد از ساخت رزرو ----------
    /// <summary>راهنمای پرداخت را (با دکمه‌های کپی کارت و مبلغ) در تلگرام برای کاربر می‌فرستد.</summary>
    public async Task OnCreatedAsync(Booking b)
    {
        var b2 = await LoadAsync(b.Id);
        if (b2 is null) return;
        var p = await settings.GetPaymentAsync();
        var rows = new List<object[]> { new[] { Kb.Copy("📋 کپی شمارهٔ کارت", p.CardNumber), Kb.Copy("📋 کپی مبلغ", ((long)Math.Round(b2.TotalPrice)).ToString()) } };
        if (await notify.WebButtonAsync("💳 ثبت رسید پرداخت", $"/pay/{b2.Id}", "primary") is { } web) rows.Add([web]);
        await notify.EnqueueAsync(b2.User!, "payment", Messages.PaymentInstructions(b2, p), Kb.Markup(rows.ToArray()), media: "card", dedupeKey: $"pay-start:{b2.Id}");
    }

    // ---------- ثبت رسید توسط کاربر ----------
    public async Task OnReceiptSubmittedAsync(int bookingId)
    {
        var b = await LoadAsync(bookingId);
        if (b is null) return;
        var rows = new List<object[]> { new[] { Kb.Cb("✅ تأیید پرداخت", $"ap:{b.Id}", "success"), Kb.Cb("❌ رد", $"rj:{b.Id}", "danger") } };
        if (await notify.WebButtonAsync("🖥 بازکردن در پنل", "/admin/payments", Kb.Neutral) is { } web) rows.Add([web]);

        await notify.EnqueueAsync(b.User!, "payment", Messages.ReceiptReceived(b), dedupeKey: $"receipt-ok:{b.Id}");
        await notify.NotifyAdminsAsync(Messages.AdminReceipt(b, b.User!), Kb.Markup(rows.ToArray()),
            media: b.ReceiptFile != "" ? "receipt" : "", bookingId: b.Id, dedupeKey: $"admin-receipt:{b.Id}");
    }

    // ---------- تصمیم ادمین ----------
    public async Task<PayResult> ApproveAsync(int bookingId, int adminUserId)
    {
        var now = DateTime.Now;
        var changed = await db.Bookings
            .Where(b => b.Id == bookingId && (b.Status == BookingStatus.AwaitingReview || b.Status == BookingStatus.PendingPayment))
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BookingStatus.Confirmed).SetProperty(b => b.ConfirmedAt, now)
                .SetProperty(b => b.ReviewedAt, now).SetProperty(b => b.ReviewedByUserId, (int?)adminUserId).SetProperty(b => b.PayDeadline, (DateTime?)null));
        var b = await LoadAsync(bookingId);
        if (b is null) return new(false, "رزرو پیدا نشد.", null);
        if (changed == 0)
            return new(false, b.Status == BookingStatus.Confirmed ? "این پرداخت قبلاً تأیید شده است." : $"وضعیت رزرو «{StatusLabel(b.Status)}» است و قابل تأیید نیست.", b);

        var rows = new List<object[]>();
        if (await notify.WebButtonAsync("🎟 نمایش بلیت", $"/my-bookings?ticket={b.Id}", "primary") is { } web) rows.Add([web]);
        rows.Add([Kb.Copy("📋 کپی کد بلیت", Messages.FormatCode(b.Code), Kb.Neutral), Kb.Cb("📄 دریافت PDF", $"pdf:{b.Id}")]);
        await notify.EnqueueAsync(b.User!, "ticket", Messages.TicketCaption(b), Kb.Markup(rows.ToArray()), media: "ticket", bookingId: b.Id, effect: true, dedupeKey: $"ticket:{b.Id}");
        return new(true, "پرداخت تأیید شد و بلیت صادر شد.", b);
    }

    public async Task<PayResult> RejectAsync(int bookingId, int adminUserId, string reason)
    {
        var now = DateTime.Now;
        reason = reason.Trim();
        if (reason.Length > 200) reason = reason[..200];
        var changed = await db.Bookings
            .Where(b => b.Id == bookingId && (b.Status == BookingStatus.AwaitingReview || b.Status == BookingStatus.PendingPayment))
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BookingStatus.Rejected).SetProperty(b => b.RejectReason, reason)
                .SetProperty(b => b.ReviewedAt, now).SetProperty(b => b.ReviewedByUserId, (int?)adminUserId));
        var b = await LoadAsync(bookingId);
        if (b is null) return new(false, "رزرو پیدا نشد.", null);
        if (changed == 0) return new(false, $"وضعیت رزرو «{StatusLabel(b.Status)}» است و قابل رد نیست.", b);

        await ReleaseSeatsAsync(bookingId);
        var rows = new List<object[]>();
        if (await notify.WebButtonAsync("🎬 رزرو دوباره", $"/movies/{b.Showtime!.MovieId}") is { } web) rows.Add([web]);
        await notify.EnqueueAsync(b.User!, "payment", Messages.Rejected(b), rows.Count > 0 ? Kb.Markup(rows.ToArray()) : null, dedupeKey: $"rejected:{b.Id}");
        return new(true, "پرداخت رد شد و صندلی‌ها آزاد شدن.", b);
    }

    // ---------- انصراف کاربر ----------
    public async Task<PayResult> CancelAsync(int bookingId, int userId)
    {
        var changed = await db.Bookings
            .Where(b => b.Id == bookingId && b.UserId == userId && b.Status == BookingStatus.PendingPayment)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled));
        if (changed == 0) return new(false, "این رزرو قابل انصراف نیست.", null);
        await ReleaseSeatsAsync(bookingId);
        return new(true, "رزرو لغو شد.", null);
    }

    // ---------- انقضا و هشدار مهلت (هر نیم‌دقیقه از Scheduler) ----------
    public async Task<int> ExpireOverdueAsync()
    {
        var now = DateTime.Now;
        var ids = await db.Bookings.Where(b => b.Status == BookingStatus.PendingPayment && b.PayDeadline < now).Select(b => b.Id).ToListAsync();
        var n = 0;
        foreach (var id in ids)
        {
            var changed = await db.Bookings.Where(b => b.Id == id && b.Status == BookingStatus.PendingPayment)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Expired));
            if (changed == 0) continue;
            await ReleaseSeatsAsync(id);
            var b = await LoadAsync(id);
            if (b is not null)
            {
                var rows = new List<object[]>();
                if (await notify.WebButtonAsync("🎬 رزرو دوباره", $"/movies/{b.Showtime!.MovieId}") is { } web) rows.Add([web]);
                await notify.EnqueueAsync(b.User!, "payment", Messages.Expired(b), rows.Count > 0 ? Kb.Markup(rows.ToArray()) : null, dedupeKey: $"expired:{id}");
            }
            n++;
        }
        return n;
    }

    public async Task WarnNearDeadlineAsync()
    {
        var now = DateTime.Now;
        var soon = now.AddMinutes(5);
        var ids = await db.Bookings.Where(b => b.Status == BookingStatus.PendingPayment && b.PayDeadline > now && b.PayDeadline <= soon).Select(b => b.Id).ToListAsync();
        foreach (var id in ids)
        {
            var b = await LoadAsync(id);
            if (b is null) continue;
            var rows = new List<object[]>();
            if (await notify.WebButtonAsync("💳 ثبت رسید", $"/pay/{b.Id}", "primary") is { } web) rows.Add([web]);
            await notify.EnqueueAsync(b.User!, "payment", Messages.DeadlineWarning(b), rows.Count > 0 ? Kb.Markup(rows.ToArray()) : null, dedupeKey: $"warn:{id}");
        }
    }
}
