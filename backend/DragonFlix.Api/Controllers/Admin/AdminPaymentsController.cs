using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

/// <summary>بررسی رسیدهای کارت‌به‌کارت: لیست، تأیید (صدور بلیت) و رد (با دلیل).</summary>
[ApiController, Route("api/admin/payments"), Authorize(Roles = Roles.Admin)]
public class AdminPaymentsController(AppDbContext db, PaymentService payments) : ControllerBase
{
    /// <param name="status">awaiting (پیش‌فرض) | pending | recent (تصمیم‌گرفته‌شده‌ها) | all</param>
    [HttpGet]
    public async Task<IActionResult> List(string status = "awaiting")
    {
        var q = db.Bookings.AsQueryable();
        q = status switch
        {
            "pending" => q.Where(b => b.Status == BookingStatus.PendingPayment),
            "recent" => q.Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Rejected || b.Status == BookingStatus.Expired),
            "all" => q,
            _ => q.Where(b => b.Status == BookingStatus.AwaitingReview),
        };
        var items = await (status == "awaiting" ? q.OrderBy(b => b.SubmittedAt) : q.OrderByDescending(b => b.CreatedAt)).Take(100).Select(b => new
        {
            b.Id, b.Code, b.Status, b.TotalPrice, b.CreatedAt, b.SubmittedAt, b.ReviewedAt, b.PayDeadline,
            b.TrackingCode, b.PayerCard4, hasReceipt = b.ReceiptFile != "", b.RejectReason, b.SeatsText,
            user = new { b.User!.FullName, b.User.Username, b.User.TelegramId },
            movie = b.Showtime!.Movie!.Title, startsAt = b.Showtime.StartsAt,
            cinema = b.Showtime.Hall!.Cinema!.Name, hall = b.Showtime.Hall.Name,
        }).ToListAsync();
        return Ok(new { awaitingCount = await db.Bookings.CountAsync(b => b.Status == BookingStatus.AwaitingReview), items });
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var r = await payments.ApproveAsync(id, User.GetUserId());
        return r.Ok ? Ok(new { message = r.Message }) : Conflict(new { message = r.Message });
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, RejectDto dto)
    {
        var r = await payments.RejectAsync(id, User.GetUserId(), dto.Reason ?? "");
        return r.Ok ? Ok(new { message = r.Message }) : Conflict(new { message = r.Message });
    }
}
