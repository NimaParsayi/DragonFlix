using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DragonFlix.Api.Controllers;
using DragonFlix.Api.Data;
using DragonFlix.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DragonFlix.Api.Services.Telegram;

/// <summary>پردازش آپدیت‌های ربات: فرمان‌ها، دکمه‌های اینلاین ادمین (تأیید/رد پرداخت) و تنظیم اعلان‌ها داخل خود تلگرام.</summary>
public class BotUpdateHandler(AppDbContext db, TelegramApi tg, AppSettings settings, NotificationService notify, PaymentService payments,
    BotSetup botSetup, ReceiptStore receipts, BackupService backup, IOptionsMonitor<TelegramOptions> opt, ILogger<BotUpdateHandler> log)
{
    private static string E(string? s) => TelegramApi.Escape(s);
    private static readonly string[] RejectReasons = ["رسید ناخوانا یا نامعتبر است", "مبلغ رسید با مبلغ رزرو مطابقت ندارد", "واریزی به حساب ما انجام نشده است"];

    public async Task HandleAsync(JsonElement update)
    {
        if (update.TryGetProperty("message", out var msg)) await OnMessageAsync(msg);
        else if (update.TryGetProperty("callback_query", out var cb)) await OnCallbackAsync(cb);
        else if (update.TryGetProperty("my_chat_member", out var m)) await OnMyChatMemberAsync(m);
    }

    // ===================== کاربر =====================
    private async Task<User> UpsertAsync(JsonElement from)
    {
        var id = from.GetProperty("id").GetInt64();
        string S(string n) => from.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        var tgUser = new TgUser(id, S("first_name"), S("last_name"), S("username"), S("language_code"), true);

        var u = await db.Users.FirstOrDefaultAsync(x => x.TelegramId == id);
        if (u is null)
        {
            u = new User { TelegramId = id, Email = $"tg{id}@telegram.local", PasswordHash = "", Role = Roles.User };
            db.Users.Add(u);
        }
        u.FullName = tgUser.DisplayName; u.Username = tgUser.Username; u.LanguageCode = tgUser.LanguageCode;
        u.BotBlocked = false; // وقتی پیام می‌دهد یعنی ربات را مسدود نکرده
        if (opt.CurrentValue.AdminIds.Contains(id)) u.Role = Roles.Admin;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); u = await db.Users.FirstAsync(x => x.TelegramId == id); }
        return u;
    }

    private bool IsAdmin(User u) => u.Role == Roles.Admin;

    private async Task OnMyChatMemberAsync(JsonElement m)
    {
        if (m.GetProperty("chat").GetProperty("type").GetString() != "private") return;
        var id = m.GetProperty("chat").GetProperty("id").GetInt64();
        var status = m.GetProperty("new_chat_member").GetProperty("status").GetString();
        await db.Users.Where(u => u.TelegramId == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.BotBlocked, status is "kicked" or "left"));
    }

    // ===================== پیام‌ها =====================
    private async Task OnMessageAsync(JsonElement msg)
    {
        if (msg.GetProperty("chat").GetProperty("type").GetString() != "private" || !msg.TryGetProperty("from", out var from)) return;
        var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();
        var user = await UpsertAsync(from);
        var text = msg.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";

        if (!text.StartsWith('/'))
        {
            await tg.SendMessageAsync(chatId, "برای استفاده از دراگون فلیکس دکمهٔ زیر رو بزن 👇\nراهنما: /help", await OpenMarkupAsync("🎬 باز کردن دراگون فلیکس", "/"));
            return;
        }

        var parts = text.Split(' ', 2, StringSplitOptions.TrimEntries);
        var cmd = parts[0].TrimStart('/').Split('@')[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : "";

        switch (cmd)
        {
            case "start": await OnStartAsync(chatId, user, arg); break;
            case "movies": await tg.SendMessageAsync(chatId, "🎬 فیلم‌های در حال اکران رو ببین و بلیتت رو بگیر:", await OpenMarkupAsync("🎬 فیلم‌ها", "/")); break;
            case "tickets": await SendTicketsAsync(chatId, user); break;
            case "settings": await SendSettingsAsync(chatId, user); break;
            case "help": await SendHelpAsync(chatId, user); break;
            case "pending" when IsAdmin(user): await SendPendingAsync(chatId); break;
            case "stats" when IsAdmin(user): await SendStatsAsync(chatId); break;
            case "backup" when IsAdmin(user):
                await tg.SendMessageAsync(chatId, backup.Start("manual", chatId)
                    ? "💾 دارم پشتیبان رو آماده می‌کنم؛ تا چند لحظه دیگه همین‌جا می‌فرستمش."
                    : "⏳ یه پشتیبان‌گیری همین الان در حال انجامه؛ چند لحظه بعد دوباره امتحان کن.");
                break;
            default: await SendHelpAsync(chatId, user); break;
        }
    }

    private async Task<object?> OpenMarkupAsync(string text, string path, string? style = "primary")
        => await notify.WebButtonAsync(text, path, style) is { } b ? Kb.Markup([b]) : null;

    private async Task OnStartAsync(long chatId, User user, string payload)
    {
        // لینک راه‌اندازی ادمین: t.me/Bot?start=admin_<code>
        if (payload.StartsWith("admin_"))
        {
            var code = payload["admin_".Length..];
            var saved = await settings.GetAsync(AppSettings.AdminSetupCode);
            if (saved != "" && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(saved), Encoding.UTF8.GetBytes(code)))
            {
                user.Role = Roles.Admin;
                await db.SaveChangesAsync();
                await settings.SetAsync(AppSettings.AdminSetupCode, null); // یک‌بار مصرف
                try { await botSetup.ApplyCommandsAsync(); } catch (TelegramApiException e) { log.LogDebug("admin commands: {Msg}", e.Message); }
                await tg.SendMessageAsync(chatId, "👑 <b>حالا ادمین شدی.</b>\n\nاز این به بعد رسیدهای پرداخت همین‌جا برات میاد و با دکمه‌ها تأیید یا ردشون می‌کنی. پنل مدیریت رو هم می‌تونی باز کنی.",
                    await OpenMarkupAsync("🖥 پنل مدیریت", "/admin"));
            }
            else await tg.SendMessageAsync(chatId, "این لینک منقضی شده یا درست نیست.");
            return;
        }

        try { await botSetup.ApplyMenuForChatAsync(chatId); } catch (TelegramApiException e) { log.LogDebug("menu: {Msg}", e.Message); }

        var path = "/";
        if (payload.StartsWith("movie_") && int.TryParse(payload["movie_".Length..], out var mid))
        {
            path = $"/movies/{mid}";
            // لینک اشتراک‌گذاری فیلم: کارت فیلم با پوستر و دکمهٔ رزرو
            var m = await db.Movies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mid);
            if (m is not null)
            {
                var genres = string.Join(" · ", m.Genre.Split(',', StringSplitOptions.RemoveEmptyEntries).Take(3));
                var desc = m.Description.Length > 300 ? m.Description[..300] + "…" : m.Description;
                var card = $"🎬 <b>{E(m.Title)}</b>\n" + (m.SpecialNote != "" ? $"✨ <b>{E(m.SpecialNote)}</b>\n" : "") +
                           $"{E(genres)} · {PersianFormat.Digits(m.DurationMinutes.ToString())} دقیقه" +
                           (m.AgeRating switch { "all" => " · همه سنین", "" => "", var a => $" · +{PersianFormat.Digits(a)}" }) +
                           (desc != "" ? $"\n\n<blockquote expandable>{E(desc)}</blockquote>" : "");
                var mrows = new List<object[]>();
                if (await notify.WebButtonAsync("🎟 انتخاب سانس و رزرو", path, "primary") is { } rb) mrows.Add([rb]);
                mrows.Add([Kb.Cb("🔔 دنبال کردن", $"fw:{mid}", Kb.Neutral)]);
                await notify.EnqueueAsync(user, "share", card, Kb.Markup(mrows.ToArray()), media: m.PosterUrl != "" ? "poster" : "", bookingId: mid);
                return;
            }
        }

        var name = E(user.FullName);
        var text =
            $"🎬 <b>{name} جان، به دراگون فلیکس خوش اومدی!</b>\n" +
            "بلیت سینما رو بدون صف و بدون دردسر رزرو کن.\n\n" +
            "<blockquote>" +
            "۱. فیلم و سانس دلخواهت رو انتخاب کن\n" +
            "۲. صندلیت رو روی نقشهٔ سالن بزن\n" +
            "۳. کارت‌به‌کارت پرداخت کن\n" +
            "۴. بلیت QRدارت رو همین‌جا بگیر" +
            "</blockquote>\n" +
            "یادآوری سانس و خبر فیلم‌های جدید هم برات می‌فرستیم 🔔\n\n" +
            "👇 برای شروع، دکمهٔ زیر رو بزن.";

        var rows = new List<object[]>();
        if (await notify.WebButtonAsync("🎬 باز کردن دراگون فلیکس", path, "primary") is { } open) rows.Add([open]);
        rows.Add([Kb.Cb("🔔 تنظیم اعلان‌ها", "st:open")]);
        if (rows.Count == 1 && !await HasAppUrlAsync())
            text += "\n\n⚠️ مینی‌اپ هنوز راه نیفتاده" + (IsAdmin(user) ? " (آدرس HTTPS رو توی پنل ← تنظیمات ثبت کن)." : "؛ یکم دیگه دوباره امتحان کن.");
        await tg.SendMessageAsync(chatId, text, Kb.Markup(rows.ToArray()), effectId: "5046509860389126442");
    }

    private async Task<bool> HasAppUrlAsync() => await notify.AppUrlAsync() is not null;

    private async Task SendHelpAsync(long chatId, User user)
    {
        var text = "📖 <b>راهنما</b>\n\n" +
                   "/movies — فیلم‌های در حال اکران\n/tickets — بلیت‌های من\n/settings — انتخاب اعلان‌ها\n\n" +
                   "<blockquote expandable>💳 پرداخت کارت‌به‌کارته: بعد از انتخاب صندلی، مبلغ رو به کارتی که نشون داده می‌شه واریز کن و رسیدش رو بفرست. " +
                   "بعد از تأیید مدیر، بلیت QRدارت همین‌جا برات میاد.</blockquote>" +
                   (IsAdmin(user) ? "\n\n👑 <b>ادمین:</b>\n/pending — رسیدهای در انتظار\n/stats — آمار امروز\n/backup — دریافت پشتیبان" : "");
        await tg.SendMessageAsync(chatId, text, await OpenMarkupAsync("🎬 باز کردن دراگون فلیکس", "/"));
    }

    private async Task SendTicketsAsync(long chatId, User user)
    {
        var from = DateTime.Now.AddHours(-3);
        var list = await db.Bookings.Where(b => b.UserId == user.Id && b.Status == BookingStatus.Confirmed && b.Showtime!.StartsAt > from)
            .OrderBy(b => b.Showtime!.StartsAt).Take(6)
            .Select(b => new { b.Id, b.SeatsText, b.Showtime!.StartsAt, Movie = b.Showtime.Movie!.Title }).ToListAsync();
        if (list.Count == 0)
        {
            await tg.SendMessageAsync(chatId, "🎟 هنوز بلیت فعالی نداری.", await OpenMarkupAsync("🎬 رزرو بلیت", "/"));
            return;
        }
        var lines = string.Join("\n", list.Select(b => $"🎟 <b>{E(b.Movie)}</b> — {PersianFormat.Date(b.StartsAt)} ⏰ {PersianFormat.Time(b.StartsAt)}"));
        var rows = list.Select(b => new[] { Kb.Cb($"🎟 {b.Movie} · {PersianFormat.Time(b.StartsAt)}", $"tk:{b.Id}") }).ToList();
        if (await notify.WebButtonAsync("📂 همهٔ رزروها", "/my-bookings", Kb.Neutral) is { } all) rows.Add([all]);
        await tg.SendMessageAsync(chatId, $"<b>بلیت‌های پیش‌رو</b>\n\n{lines}\n\nروی هر بلیت بزنی دوباره برات فرستاده می‌شه:", Kb.Markup(rows.ToArray()));
    }

    // ===================== تنظیم اعلان‌ها داخل خود ربات =====================
    private static (string Text, object Markup) SettingsView(User u, string? webUrl)
    {
        var on = NotificationCategories.EnabledOptional(u);
        var sb = new StringBuilder("🔔 <b>تنظیم اعلان‌ها</b>\n\n<b>ضروری</b> (همیشه ارسال می‌شود):\n");
        foreach (var c in NotificationCategories.All.Where(c => c.Mandatory)) sb.Append($"🔒 {c.Title}\n");
        sb.Append("\n<b>دلخواه</b> (با دکمه‌ها روشن/خاموش کن):\n");
        var rows = new List<object[]>();
        foreach (var c in NotificationCategories.All.Where(c => !c.Mandatory))
        {
            var enabled = on.Contains(c.Key);
            sb.Append($"{(enabled ? "✅" : "⬜️")} {c.Title}\n");
            rows.Add([Kb.Cb($"{(enabled ? "✅" : "⬜️")} {c.Title}", $"pf:{c.Key}", enabled ? Kb.Primary : Kb.Neutral)]);
        }
        if (webUrl is not null) rows.Add([Kb.Web("⚙️ حساب من", webUrl + "/me")]);
        return (sb.ToString(), Kb.Markup(rows.ToArray()));
    }

    private async Task SendSettingsAsync(long chatId, User user)
    {
        var (text, markup) = SettingsView(user, await notify.AppUrlAsync());
        await tg.SendMessageAsync(chatId, text, markup);
    }

    // ===================== ادمین =====================
    private async Task SendPendingAsync(long chatId)
    {
        var ids = await db.Bookings.Where(b => b.Status == BookingStatus.AwaitingReview).OrderBy(b => b.SubmittedAt).Take(6).Select(b => b.Id).ToListAsync();
        if (ids.Count == 0) { await tg.SendMessageAsync(chatId, "✅ رسیدی منتظر بررسی نیست."); return; }
        foreach (var id in ids)
        {
            var b = await payments.LoadAsync(id);
            if (b is null) continue;
            var markup = Kb.Markup([[Kb.Cb("✅ تأیید پرداخت", $"ap:{id}", "success"), Kb.Cb("❌ رد", $"rj:{id}", "danger")]]);
            var text = Messages.AdminReceipt(b, b.User!);
            if (b.ReceiptFile != "" && await receipts.ReadAsync(b.ReceiptFile) is { } r)
                await tg.SendPhotoAsync(chatId, r.Bytes, text, markup);
            else await tg.SendMessageAsync(chatId, text, markup);
        }
    }

    private async Task SendStatsAsync(long chatId)
    {
        var today = DateTime.Now.Date;
        var confirmedToday = await db.Bookings.CountAsync(b => b.Status == BookingStatus.Confirmed && b.CreatedAt >= today);
        var revenue = await db.Bookings.Where(b => b.Status == BookingStatus.Confirmed && b.CreatedAt >= today).SumAsync(b => (double?)b.TotalPrice) ?? 0;
        var pending = await db.Bookings.CountAsync(b => b.Status == BookingStatus.AwaitingReview);
        var unpaid = await db.Bookings.CountAsync(b => b.Status == BookingStatus.PendingPayment);
        await tg.SendMessageAsync(chatId,
            $"📊 <b>آمار امروز</b>\n\n🎟 رزرو تأییدشده: <b>{PersianFormat.Digits(confirmedToday.ToString())}</b>\n💰 فروش: <b>{PersianFormat.Money((decimal)revenue)}</b>\n" +
            $"🧾 رسید در انتظار: <b>{PersianFormat.Digits(pending.ToString())}</b>\n⏳ منتظر پرداخت کاربر: {PersianFormat.Digits(unpaid.ToString())}");
    }

    // ===================== دکمه‌های اینلاین =====================
    private async Task OnCallbackAsync(JsonElement cb)
    {
        var cbId = cb.GetProperty("id").GetString()!;
        var user = await UpsertAsync(cb.GetProperty("from"));
        var data = cb.TryGetProperty("data", out var d) ? d.GetString() ?? "" : "";
        cb.TryGetProperty("message", out var msg);
        var hasMsg = msg.ValueKind == JsonValueKind.Object;
        var p = data.Split(':');

        try
        {
            switch (p[0])
            {
                case "ap" or "rj" or "rr" or "rb" when !IsAdmin(user):
                    await tg.AnswerCallbackAsync(cbId, "این دکمه فقط برای ادمینه.", alert: true); return;

                case "ap":
                {
                    var r = await payments.ApproveAsync(int.Parse(p[1]), user.Id);
                    await tg.AnswerCallbackAsync(cbId, r.Message, alert: !r.Ok);
                    if (hasMsg && r.Booking is not null) await EditAsync(msg, Messages.AdminReceipt(r.Booking, r.Booking.User!) + (r.Ok ? $"\n\n✅ <b>تأیید شد</b> — {E(user.FullName)}" : $"\n\n⚠️ {E(r.Message)}"), null);
                    return;
                }
                case "rj":
                {
                    var id = p[1];
                    var rows = RejectReasons.Select((t, i) => new[] { Kb.Cb(t, $"rr:{id}:{i}", Kb.Danger) }).ToList();
                    rows.Add([Kb.Cb("↩️ بازگشت", $"rb:{id}", Kb.Neutral)]);
                    await tg.AnswerCallbackAsync(cbId, "دلیل رد رو انتخاب کن");
                    if (hasMsg) await EditMarkupAsync(msg, Kb.Markup(rows.ToArray()));
                    return;
                }
                case "rb":
                    await tg.AnswerCallbackAsync(cbId);
                    if (hasMsg) await EditMarkupAsync(msg, Kb.Markup([[Kb.Cb("✅ تأیید پرداخت", $"ap:{p[1]}", "success"), Kb.Cb("❌ رد", $"rj:{p[1]}", "danger")]]));
                    return;
                case "rr":
                {
                    var reason = RejectReasons[Math.Clamp(int.Parse(p[2]), 0, RejectReasons.Length - 1)];
                    var r = await payments.RejectAsync(int.Parse(p[1]), user.Id, reason);
                    await tg.AnswerCallbackAsync(cbId, r.Message, alert: !r.Ok);
                    if (hasMsg && r.Booking is not null) await EditAsync(msg, Messages.AdminReceipt(r.Booking, r.Booking.User!) + (r.Ok ? $"\n\n❌ <b>رد شد</b> ({E(reason)}) — {E(user.FullName)}" : $"\n\n⚠️ {E(r.Message)}"), null);
                    return;
                }

                case "fw": // دنبال‌کردن/لغو دنبال‌کردن فیلم
                {
                    var mid = int.Parse(p[1]);
                    var existing = await db.MovieFollows.FirstOrDefaultAsync(f => f.UserId == user.Id && f.MovieId == mid);
                    if (existing is null)
                    {
                        if (!await db.Movies.AnyAsync(m => m.Id == mid)) { await tg.AnswerCallbackAsync(cbId, "فیلم پیدا نشد."); return; }
                        db.MovieFollows.Add(new MovieFollow { UserId = user.Id, MovieId = mid, CreatedAt = DateTime.UtcNow });
                        await db.SaveChangesAsync();
                        await tg.AnswerCallbackAsync(cbId, "🔔 دنبالش کردی؛ سانس‌های جدید رو خبر می‌دیم.");
                    }
                    else { db.MovieFollows.Remove(existing); await db.SaveChangesAsync(); await tg.AnswerCallbackAsync(cbId, "🔕 دیگه این فیلم رو دنبال نمی‌کنی."); }
                    return;
                }

                case "ls" when p.Length > 1 && p[1] == "pending" && IsAdmin(user): // دکمهٔ یادآوری رسیدهای مانده
                    await tg.AnswerCallbackAsync(cbId);
                    await SendPendingAsync(user.TelegramId!.Value);
                    return;
                case "st": // باز کردن تنظیمات اعلان
                    await tg.AnswerCallbackAsync(cbId);
                    if (hasMsg) await SendSettingsAsync(msg.GetProperty("chat").GetProperty("id").GetInt64(), user);
                    return;
                case "pf": // روشن/خاموش کردن یک اعلان دلخواه
                {
                    var key = p[1];
                    if (NotificationCategories.Find(key) is not { Mandatory: false }) { await tg.AnswerCallbackAsync(cbId, "این اعلان ضروریه و خاموش نمی‌شه."); return; }
                    var on = NotificationCategories.EnabledOptional(user);
                    if (!on.Add(key)) on.Remove(key);
                    user.NotifyPrefs = string.Join(",", on);
                    await db.SaveChangesAsync();
                    await tg.AnswerCallbackAsync(cbId, on.Contains(key) ? "✅ روشن شد" : "⬜️ خاموش شد");
                    if (hasMsg) { var (t, mk) = SettingsView(user, await notify.AppUrlAsync()); await EditAsync(msg, t, mk); }
                    return;
                }

                case "pdf" or "tk": // ارسال دوبارهٔ بلیت به صاحبش
                {
                    var b = await LoadTicketAsync(int.Parse(p[1]), user.Id);
                    if (b is null || b.Status != BookingStatus.Confirmed) { await tg.AnswerCallbackAsync(cbId, "این بلیت پیدا نشد.", alert: true); return; }
                    await tg.AnswerCallbackAsync(cbId, p[0] == "pdf" ? "📄 دارم PDF رو می‌فرستم…" : "🎟 دارم بلیت رو می‌فرستم…");
                    var chat = user.TelegramId!.Value;
                    var data2 = BookingsController.ToTicket(b);
                    if (p[0] == "pdf") await tg.SendDocumentAsync(chat, TicketPdf.Render(data2), $"dragonflix-ticket-{b.Code}.pdf", $"📄 بلیت <code>{E(Messages.FormatCode(b.Code))}</code>");
                    else await tg.SendPhotoAsync(chat, TicketImage.Render(data2, await PosterSource.LoadAsync(data2.PosterUrl)), Messages.TicketCaption(b), Kb.Markup([[Kb.Cb("📄 دریافت PDF", $"pdf:{b.Id}")]]));
                    return;
                }
                default:
                    await tg.AnswerCallbackAsync(cbId);
                    return;
            }
        }
        catch (Exception e) when (e is FormatException or IndexOutOfRangeException)
        {
            await tg.AnswerCallbackAsync(cbId, "درخواست نامعتبره.");
        }
    }

    private Task<Booking?> LoadTicketAsync(int id, int userId) => db.Bookings
        .Include(x => x.User).Include(x => x.Seats).ThenInclude(s => s.SeatRow)
        .Include(x => x.Showtime).ThenInclude(s => s!.Movie)
        .Include(x => x.Showtime).ThenInclude(s => s!.Hall).ThenInclude(h => h!.Cinema)
        .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

    // ---------- ویرایش پیام (متن یا کپشن عکس) ----------
    private async Task EditAsync(JsonElement msg, string html, object? markup)
    {
        var chat = msg.GetProperty("chat").GetProperty("id").GetInt64();
        var mid = msg.GetProperty("message_id").GetInt32();
        var isPhoto = msg.TryGetProperty("photo", out _);
        var empty = new { InlineKeyboard = Array.Empty<object>() };
        try
        {
            if (isPhoto) await tg.CallAsync("editMessageCaption", new { ChatId = chat, MessageId = mid, Caption = html, ParseMode = "HTML", ReplyMarkup = markup ?? empty });
            else await tg.CallAsync("editMessageText", new { ChatId = chat, MessageId = mid, Text = html, ParseMode = "HTML", ReplyMarkup = markup ?? empty, LinkPreviewOptions = new { IsDisabled = true } });
        }
        catch (TelegramApiException e) when (e.Message.Contains("not modified", StringComparison.OrdinalIgnoreCase)) { }
    }

    private async Task EditMarkupAsync(JsonElement msg, object markup)
    {
        try
        {
            await tg.CallAsync("editMessageReplyMarkup", new { ChatId = msg.GetProperty("chat").GetProperty("id").GetInt64(), MessageId = msg.GetProperty("message_id").GetInt32(), ReplyMarkup = markup });
        }
        catch (TelegramApiException e) when (e.Message.Contains("not modified", StringComparison.OrdinalIgnoreCase)) { }
    }
}
