using DragonFlix.Api.Data;
using Microsoft.EntityFrameworkCore;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DragonFlix.Api.Controllers.Admin;

/// <summary>پشتیبان‌گیری: وضعیت، روشن/خاموش کردن ارسال شبانه و ارسال دستی در تلگرام.</summary>
[ApiController, Route("api/admin/backup"), Authorize(Roles = Roles.Admin)]
public class AdminBackupController(BackupService backup, AppSettings settings, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<object> Status()
    {
        var (bytes, files) = backup.FilesInfo();
        var dbFile = (db.Database.GetConnectionString() ?? "").Replace("Data Source=", "").Trim();
        var dbSize = System.IO.File.Exists(dbFile) ? new FileInfo(dbFile).Length : 0;
        var now = DateTime.Now;
        return new
        {
            auto = await settings.GetAsync(BackupService.AutoKey, "1") == "1",
            running = backup.Running,
            progress = backup.Progress,
            last = await backup.LastAsync(),
            dbBytes = dbSize, filesBytes = bytes, files,
            partMb = BackupService.PartBytes / 1048576,
            nextAt = now.Date.AddDays(1),
        };
    }

    public record AutoDto(bool Enabled);

    [HttpPut("auto")]
    public async Task<IActionResult> SetAuto(AutoDto dto)
    {
        await settings.SetAsync(BackupService.AutoKey, dto.Enabled ? "1" : "0");
        return NoContent();
    }

    public record SendDto(string Scope);

    /// <summary>scope: «me» فقط برای خودِ ادمین، «all» برای همهٔ ادمین‌ها.</summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send(SendDto dto)
    {
        long? chat = null;
        if (dto.Scope != "all")
        {
            var me = await db.Users.FindAsync(User.GetUserId());
            if (me?.TelegramId is not { } tgId) return BadRequest(new { message = "حساب تلگرامت به این ادمین وصل نیست." });
            chat = tgId;
        }
        if (!backup.Start("manual", chat)) return Conflict(new { message = "یه پشتیبان‌گیری همین الان در حال انجامه؛ چند لحظه صبر کن." });
        return Accepted();
    }
}
