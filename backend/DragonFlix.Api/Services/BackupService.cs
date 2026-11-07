using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using DragonFlix.Api.Services.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DragonFlix.Api.Services;

/// <summary>
/// پشتیبان‌گیری: یک اسنپ‌شات سازگار از دیتابیس SQLite + رسیدها + عکس فیلم‌ها در یک ZIP.
/// چون تلگرام فایل بالای ۵۰ مگابایت را از ربات قبول نمی‌کند، ZIP به بخش‌های ۴۵ مگابایتی (.zip.001، .002، …) بریده می‌شود
/// و برای ادمین‌ها می‌رود؛ با <c>cat name.zip.* &gt; name.zip</c> دوباره یکی می‌شود.
/// </summary>
public class BackupService(IServiceScopeFactory scopes, IWebHostEnvironment env, IOptionsMonitor<TelegramOptions> opts, ILogger<BackupService> log)
{
    public const int PartBytes = 45 * 1024 * 1024; // زیر سقف ۵۰ مگابایتیِ ارسال سند توسط ربات
    public const string AutoKey = "backup.auto";
    public const string LastKey = "backup.last";
    public const string LastAutoDayKey = "backup.lastAutoDay";

    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile string progress = "";
    public bool Running => gate.CurrentCount == 0;
    public string Progress => progress;

    public record Result(DateTime At, long Bytes, int Parts, int Recipients, string Trigger, bool Ok, string Message);

    private static readonly JsonSerializerOptions J = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private string ReceiptsDir => Path.Combine(env.ContentRootPath, "App_Data", "receipts");
    private string UploadsDir => Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");

    public async Task<Result?> LastAsync()
    {
        using var scope = scopes.CreateScope();
        var raw = await scope.ServiceProvider.GetRequiredService<AppSettings>().GetAsync(LastKey);
        return raw == "" ? null : JsonSerializer.Deserialize<Result>(raw, J);
    }

    public (long Bytes, int Files) FilesInfo()
    {
        long total = 0; var n = 0;
        foreach (var dir in new[] { ReceiptsDir, UploadsDir })
            if (Directory.Exists(dir))
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) { total += new FileInfo(f).Length; n++; }
        return (total, n);
    }

    /// <summary>شروع در پس‌زمینه. false یعنی یک پشتیبان‌گیری دیگر در حال اجراست.</summary>
    public bool Start(string trigger, long? onlyChat = null)
    {
        if (!gate.Wait(0)) return false;
        _ = Task.Run(async () =>
        {
            try { await RunAsync(trigger, onlyChat); }
            catch (Exception e) { log.LogError(e, "پشتیبان‌گیری ناموفق بود"); }
            finally { progress = ""; gate.Release(); }
        });
        return true;
    }

    private async Task RunAsync(string trigger, long? onlyChat)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var tg = sp.GetRequiredService<TelegramApi>();
        var settings = sp.GetRequiredService<AppSettings>();
        var now = DateTime.Now;
        var stamp = now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var work = Path.Combine(Path.GetTempPath(), "dragonflix-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            progress = "در حال ساخت فایل پشتیبان…";
            var zipPath = Path.Combine(work, $"dragonflix-backup-{stamp}.zip");
            await BuildZipAsync(db, zipPath, work, now);
            var size = new FileInfo(zipPath).Length;

            // گیرنده‌ها: همهٔ ادمین‌های در دسترس (یا فقط درخواست‌دهنده)
            var chats = new List<long>();
            if (onlyChat is { } one) chats.Add(one);
            else
            {
                chats.AddRange(await db.Users.Where(u => u.Role == Roles.Admin && u.TelegramId != null && !u.BotBlocked).Select(u => u.TelegramId!.Value).ToListAsync());
                chats.AddRange(opts.CurrentValue.AdminIds);
                chats = chats.Distinct().ToList();
            }

            var parts = Split(zipPath, work);
            var sent = 0;
            if (!tg.Configured || chats.Count == 0)
            {
                await Save(settings, new Result(now, size, parts.Count, 0, trigger, false, tg.Configured ? "ادمینی با تلگرام فعال پیدا نشد." : "ربات تنظیم نشده است."));
                return;
            }

            foreach (var chat in chats)
            {
                try
                {
                    progress = $"در حال ارسال برای ادمین‌ها ({sent + 1} از {chats.Count})…";
                    await SendToAsync(tg, chat, parts, stamp, size, now, trigger);
                    sent++;
                }
                catch (TelegramApiException e) { log.LogWarning("ارسال پشتیبان به {Chat} ناموفق: {Msg}", chat, e.Message); }
            }
            await Save(settings, new Result(now, size, parts.Count, sent, trigger, sent > 0, sent > 0 ? "ارسال شد." : "ارسال به هیچ ادمینی ممکن نشد."));
        }
        finally { try { Directory.Delete(work, true); } catch { /* موقت است */ } }
    }

    private async Task BuildZipAsync(AppDbContext db, string zipPath, string work, DateTime now)
    {
        // اسنپ‌شات سازگار از دیتابیس زنده (کپی ساده فایل ممکن است نیمه‌کاره باشد)
        var snap = Path.Combine(work, "dragonflix.db");
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{snap.Replace("'", "''")}'");

        await using var fs = File.Create(zipPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(snap, "dragonflix.db", CompressionLevel.Optimal);
        File.Delete(snap);
        AddDir(zip, ReceiptsDir, "receipts");
        AddDir(zip, UploadsDir, "uploads");
        var note = zip.CreateEntry("README.txt");
        await using var w = new StreamWriter(note.Open());
        await w.WriteAsync($"DragonFlix backup — {now:yyyy-MM-dd HH:mm}\n\n" +
                           "dragonflix.db  -> کنار برنامه (فایل دیتابیس)\n" +
                           "receipts/      -> App_Data/receipts\n" +
                           "uploads/       -> wwwroot/uploads\n\n" +
                           "Restore: stop the app, replace these files, start the app.\n");
    }

    private static void AddDir(ZipArchive zip, string dir, string prefix)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dir, f).Replace('\\', '/');
            // عکس‌ها از قبل فشرده‌اند
            zip.CreateEntryFromFile(f, $"{prefix}/{rel}", CompressionLevel.NoCompression);
        }
    }

    private static List<string> Split(string zipPath, string work)
    {
        var size = new FileInfo(zipPath).Length;
        if (size <= PartBytes) return [zipPath];
        var parts = new List<string>();
        using var src = File.OpenRead(zipPath);
        var buf = new byte[1024 * 1024];
        var i = 1;
        while (src.Position < src.Length)
        {
            var p = Path.Combine(work, $"{Path.GetFileName(zipPath)}.{i:000}");
            using (var o = File.Create(p))
            {
                long left = PartBytes;
                while (left > 0)
                {
                    var n = src.Read(buf, 0, (int)Math.Min(buf.Length, left));
                    if (n <= 0) break;
                    o.Write(buf, 0, n);
                    left -= n;
                }
            }
            parts.Add(p);
            i++;
        }
        File.Delete(zipPath);
        return parts;
    }

    private static async Task SendToAsync(TelegramApi tg, long chat, List<string> parts, string stamp, long size, DateTime at, string trigger)
    {
        var name = $"dragonflix-backup-{stamp}.zip";
        var head = $"💾 <b>پشتیبان دراگون‌فلیکس</b>\n🗓 {PersianFormat.Date(at)}  ⏰ {PersianFormat.Time(at)}\n" +
                   $"حجم: <b>{PersianFormat.Digits(Mb(size))}</b> مگابایت" + (trigger == "auto" ? " · خودکار" : " · دستی");
        if (parts.Count > 1)
            head += $"\n\nفایل به <b>{PersianFormat.Digits(parts.Count.ToString())} بخش</b> تقسیم شده (محدودیت تلگرام). همهٔ بخش‌ها را کنار هم بگذار و یکی کن:\n" +
                    $"<blockquote><code>cat {name}.* &gt; {name}</code>\nدر ویندوز: <code>copy /b {name}.* {name}</code></blockquote>";
        await tg.SendMessageAsync(chat, head);
        for (var i = 0; i < parts.Count; i++)
        {
            var bytes = await File.ReadAllBytesAsync(parts[i]);
            var fn = parts.Count == 1 ? name : $"{name}.{i + 1:000}";
            var cap = parts.Count == 1 ? $"💾 {fn}" : $"💾 بخش {PersianFormat.Digits((i + 1).ToString())} از {PersianFormat.Digits(parts.Count.ToString())}";
            // اینترنت ناپایدار: هر بخش تا ۴ بار با مکث امتحان می‌شود (خطای شبکه، نه خطای تلگرام)
            for (var attempt = 1; ; attempt++)
            {
                try { await tg.SendDocumentAsync(chat, bytes, fn, cap, contentType: "application/octet-stream"); break; }
                catch (TelegramApiException e) when (e.Code == 0 && attempt < 4) { await Task.Delay(TimeSpan.FromSeconds(5 * attempt)); }
            }
        }
    }

    private static string Mb(long b) => (b / 1048576d).ToString("0.#", CultureInfo.InvariantCulture);

    private static async Task Save(AppSettings s, Result r) => await s.SetAsync(LastKey, JsonSerializer.Serialize(r, J));
}
