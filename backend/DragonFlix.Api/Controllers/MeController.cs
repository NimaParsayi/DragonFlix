using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers;

/// <summary>حساب کاربر: پروفایل تلگرام، انتخاب اعلان‌ها و فیلم‌های دنبال‌شده.</summary>
[ApiController, Authorize]
public class MeController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/me")]
    public async Task<IActionResult> Get()
    {
        var u = await db.Users.FindAsync(User.GetUserId());
        if (u is null) return Unauthorized();
        var on = NotificationCategories.EnabledOptional(u);
        return Ok(new
        {
            user = new { u.Id, u.FullName, u.Username, u.TelegramId, u.Role, botReachable = !u.BotBlocked, firstName = u.RegFirstName, lastName = u.RegLastName, registered = u.IsRegistered },
            categories = NotificationCategories.All.Select(c => new { c.Key, c.Title, c.Description, c.Mandatory, enabled = c.Mandatory || on.Contains(c.Key) }),
        });
    }

    private static readonly System.Text.RegularExpressions.Regex NameRx = new(@"^[\p{L}\p{M}][\p{L}\p{M} \u200c'\-]*$");

    /// <summary>ثبت نام و نام خانوادگی؛ بدون آن رزرو بلیت ممکن نیست و روی بلیت چاپ می‌شود.</summary>
    [HttpPut("api/me/profile")]
    public async Task<IActionResult> SetProfile(ProfileDto dto)
    {
        static string Clean(string? s) => System.Text.RegularExpressions.Regex.Replace((s ?? "").Trim(), @"\s+", " ");
        var first = Clean(dto.FirstName); var last = Clean(dto.LastName);
        if (first.Length < 2 || last.Length < 2)
            return BadRequest(new { message = "نام و نام خانوادگی رو کامل وارد کن (حداقل ۲ حرف)." });
        if (first.Length > 30 || last.Length > 40)
            return BadRequest(new { message = "نام یا نام خانوادگی خیلی طولانیه." });
        if (!NameRx.IsMatch(first) || !NameRx.IsMatch(last))
            return BadRequest(new { message = "نام و نام خانوادگی فقط می‌تونه شامل حروف باشه." });
        var u = await db.Users.FindAsync(User.GetUserId());
        if (u is null) return Unauthorized();
        u.RegFirstName = first; u.RegLastName = last;
        await db.SaveChangesAsync();
        return Ok(new { firstName = first, lastName = last, registered = true });
    }

    /// <summary>فهرست دسته‌های دلخواهِ روشن را ذخیره می‌کند؛ دسته‌های ضروری همیشه روشن‌اند و نادیده گرفته می‌شوند.</summary>
    [HttpPut("api/me/notifications")]
    public async Task<IActionResult> SetNotifications(NotifyPrefsDto dto)
    {
        var u = await db.Users.FindAsync(User.GetUserId());
        if (u is null) return Unauthorized();
        u.NotifyPrefs = string.Join(",", dto.Enabled.Distinct().Where(k => NotificationCategories.Find(k) is { Mandatory: false }));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("api/me/follows")]
    public async Task<object> Follows()
    {
        var uid = User.GetUserId();
        return await db.MovieFollows.Where(f => f.UserId == uid).OrderByDescending(f => f.CreatedAt)
            .Join(db.Movies, f => f.MovieId, m => m.Id, (f, m) => new { m.Id, m.Title, m.PosterUrl, m.Genre }).ToListAsync();
    }

    [HttpPost("api/movies/{id:int}/follow")]
    public async Task<IActionResult> Follow(int id)
    {
        if (!await db.Movies.AnyAsync(m => m.Id == id)) return NotFound();
        var uid = User.GetUserId();
        if (!await db.MovieFollows.AnyAsync(f => f.UserId == uid && f.MovieId == id))
        {
            db.MovieFollows.Add(new MovieFollow { UserId = uid, MovieId = id, CreatedAt = DateTime.UtcNow });
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { /* هم‌زمانی؛ قبلاً ثبت شده */ }
        }
        return NoContent();
    }

    [HttpDelete("api/movies/{id:int}/follow")]
    public async Task<IActionResult> Unfollow(int id)
    {
        var uid = User.GetUserId();
        await db.MovieFollows.Where(f => f.UserId == uid && f.MovieId == id).ExecuteDeleteAsync();
        return NoContent();
    }
}
