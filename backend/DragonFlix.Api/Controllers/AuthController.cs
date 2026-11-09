using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using DragonFlix.Api.Services.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DragonFlix.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens, IOptionsMonitor<TelegramOptions> tg, IConfiguration cfg) : ControllerBase
{
    private readonly PasswordHasher<User> _hasher = new();

    // ورود با ایمیل و رمز به‌صورت پیش‌فرض غیرفعال است؛ فقط نشست تلگرام. (Auth:AllowPassword=true آن را برمی‌گرداند)
    private bool PasswordLoginEnabled => cfg.GetValue<bool>("Auth:AllowPassword");
    private static readonly object Disabled = new { message = "ورود با ایمیل و رمز غیرفعاله؛ برنامه رو از داخل تلگرام باز کن." };

    /// <summary>
    /// ورود با نشست خودِ تلگرام: مینی‌اپ initData را می‌فرستد، امضایش با توکن ربات بررسی می‌شود و کاربر ساخته/به‌روز می‌شود.
    /// </summary>
    [HttpPost("telegram")]
    public async Task<ActionResult<AuthResultDto>> Telegram(TelegramLoginDto dto)
    {
        var o = tg.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.BotToken)) return StatusCode(503, new { message = "ربات تلگرام هنوز تنظیم نشده است." });

        var t = TelegramAuth.Validate(dto.InitData, o.BotToken, TimeSpan.FromHours(o.InitDataMaxAgeHours), out var error);
        if (t is null) return Unauthorized(new { message = error });

        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramId == t.Id);
        if (user is null)
        {
            user = new User { TelegramId = t.Id, Email = $"tg{t.Id}@telegram.local", PasswordHash = "", Role = Roles.User };
            db.Users.Add(user);
        }
        user.FullName = t.DisplayName;
        user.Username = t.Username;
        user.LanguageCode = t.LanguageCode;
        user.LastLoginAt = DateTime.UtcNow;
        if (t.AllowsWrite) user.BotBlocked = false;
        if (o.AdminIds.Contains(t.Id)) user.Role = Roles.Admin;

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) // دو درخواست هم‌زمانِ اولین ورود
        {
            db.ChangeTracker.Clear();
            user = await db.Users.FirstAsync(u => u.TelegramId == t.Id);
        }
        return Result(user);
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResultDto>> Register(RegisterDto dto)
    {
        if (!PasswordLoginEnabled) return NotFound(Disabled);
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "این ایمیل قبلاً ثبت شده است." });

        var user = new User { Email = email, FullName = dto.FullName.Trim(), Role = Roles.User };
        user.PasswordHash = _hasher.HashPassword(user, dto.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Result(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResultDto>> Login(LoginDto dto)
    {
        if (!PasswordLoginEnabled) return NotFound(Disabled);
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || user.PasswordHash == "" || _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "ایمیل یا رمز عبور نادرست است." });
        return Result(user);
    }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        return user is null ? Unauthorized() : ToDto(user);
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.FullName, u.Role, u.Username, u.TelegramId, u.RegFirstName, u.RegLastName, u.IsRegistered);
    private AuthResultDto Result(User u) => new(tokens.Create(u), ToDto(u));
}
