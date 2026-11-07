using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using DragonFlix.Api.Services.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

/// <summary>اعلان همگانی ادمین؛ فقط برای کسانی که آن دسته را روشن کرده‌اند و ربات را مسدود نکرده‌اند.</summary>
[ApiController, Route("api/admin/broadcast"), Authorize(Roles = Roles.Admin)]
public class AdminBroadcastController(AppDbContext db, NotificationService notify) : ControllerBase
{
    private static readonly string[] Allowed = ["promo", "new_movie"];

    [HttpGet("audience")]
    public async Task<object> Audience()
    {
        var users = await db.Users.Where(u => u.TelegramId != null && !u.BotBlocked).ToListAsync();
        return new
        {
            total = users.Count,
            promo = users.Count(u => NotificationCategories.Allows(u, "promo")),
            new_movie = users.Count(u => NotificationCategories.Allows(u, "new_movie")),
        };
    }

    [HttpPost]
    public async Task<IActionResult> Send(BroadcastDto dto)
    {
        var category = Allowed.Contains(dto.Category) ? dto.Category : "promo";
        var text = dto.Text.Trim();
        if (text.Length < 3) return BadRequest(new { message = "متن اعلان را بنویسید." });

        object? markup = null;
        if (!string.IsNullOrWhiteSpace(dto.ButtonText) && await notify.WebButtonAsync(dto.ButtonText.Trim(), NormalizePath(dto.ButtonPath)) is { } web)
            markup = Kb.Markup([web]);

        var users = await db.Users.Where(u => u.TelegramId != null && !u.BotBlocked).ToListAsync();
        var title = category == "promo" ? "🎁" : "🎬";
        var queued = await notify.BroadcastAsync(users, category, $"{title} {TelegramApi.Escape(text).Replace("\n", "\n")}", markup);
        return Ok(new { queued });
    }

    private static string NormalizePath(string? p) => string.IsNullOrWhiteSpace(p) ? "/" : p.StartsWith('/') ? p : "/" + p;
}
