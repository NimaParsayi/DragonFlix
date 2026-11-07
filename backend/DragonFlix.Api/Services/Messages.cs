using DragonFlix.Api.Models;
using DragonFlix.Api.Services.Telegram;

namespace DragonFlix.Api.Services;

/// <summary>متن پیام‌های ربات (HTML تلگرام). هر چیزِ واردشده توسط کاربر Escape می‌شود.</summary>
public static class Messages
{
    private static string E(string? s) => TelegramApi.Escape(s);

    public static string Seats(Booking b) =>
        string.Join("\n", SeatFormat.Lines(b.SeatsText).Select(l => $"<b>{E(l)}</b>"));

    private static string Header(Booking b) =>
        $"🎬 <b>{E(b.Showtime!.Movie!.Title)}</b>\n" +
        $"🏛 {E(b.Showtime.Hall!.Cinema!.Name)} · {E(b.Showtime.Hall.Name)}\n" +
        $"🗓 {PersianFormat.Date(b.Showtime.StartsAt)}  ⏰ <b>{PersianFormat.Time(b.Showtime.StartsAt)}</b>\n" +
        $"💺 {Seats(b).Replace("\n", "\n💺 ")}";

    public static string PaymentInstructions(Booking b, AppSettings.PaymentInfo p) =>
        "💳 <b>رزروت ثبت شد، فقط مونده پرداخت</b>\n\n" + Header(b) + "\n\n" +
        $"💰 مبلغ پرداخت: <b>{PersianFormat.Money(b.TotalPrice)}</b>\n\n" +
        "همین مبلغ رو به کارت زیر واریز کن (روی شماره بزنی کپی می‌شه):\n" +
        $"<code>{E(FormatCard(p.CardNumber))}</code>\n" +
        (p.CardHolder != "" ? $"👤 {E(p.CardHolder)}" + (BankInfo.NameOf(p.CardNumber, p.CardBank) is { Length: > 0 } bn ? $" · {E(bn)}" : "") + "\n" : "") +
        $"\n⏳ مهلت پرداخت: تا <b>{PersianFormat.Time(b.PayDeadline!.Value)}</b>\n" +
        "بعد از واریز، عکس رسید رو توی برنامه بفرست." +
        (p.Note != "" ? $"\n\n<blockquote>{E(p.Note)}</blockquote>" : "");

    public static string DeadlineWarning(Booking b) =>
        $"⏰ <b>فقط ۵ دقیقه تا پایان مهلت پرداخت مونده!</b>\n\n{Header(b)}\n\nاگه واریز کردی، زود رسیدش رو بفرست؛ وگرنه صندلی‌ها آزاد می‌شن.";

    public static string Expired(Booking b) =>
        $"⌛️ <b>مهلت پرداخت تموم شد</b>\n\n{Header(b)}\n\nصندلی‌ها آزاد شدن. اگه هنوز می‌خوای، دوباره رزرو کن.";

    public static string ReceiptReceived(Booking b) =>
        $"✅ <b>رسیدت ثبت شد</b>\n\n{Header(b)}\n\nمدیر زود پرداختت رو بررسی می‌کنه و نتیجه همین‌جا بهت خبر داده می‌شه. صندلی‌هات تا اون موقع برات نگه داشته شده.";

    public static string Rejected(Booking b) =>
        $"❌ <b>پرداختت تأیید نشد</b>\n\n{Header(b)}\n\n" +
        (b.RejectReason != "" ? $"📝 دلیل: {E(b.RejectReason)}\n\n" : "") +
        "صندلی‌ها آزاد شدن. اگه واریز کردی با پشتیبانی تماس بگیر یا دوباره رزرو کن.";

    public static string TicketCaption(Booking b) =>
        "🎟 <b>بلیتت آماده‌ست!</b>\n\n" +
        $"🎬 <b>{E(b.Showtime!.Movie!.Title)}</b>\n" +
        $"🗓 {PersianFormat.Date(b.Showtime.StartsAt)}  ⏰ <b>{PersianFormat.Time(b.Showtime.StartsAt)}</b>\n" +
        $"🔖 کد بلیت: <code>{E(FormatCode(b.Code))}</code>\n\n" +
        "<blockquote expandable>🔸 ۱۵ دقیقه قبل از شروع سانس اونجا باش.\n" +
        "🔸 موقع ورود، QR بلیت رو به مسئول سالن نشون بده.\n" +
        "🔸 با «نمایش بلیت» می‌تونی توی برنامه ببینیش و PDF بگیری.\n" +
        "🔸 قبل از سانس هم بهت یادآوری می‌فرستیم.</blockquote>";

    public static string Reminder(Booking b) =>
        $"🍿 <b>یادت نره! سانست امروز ساعت {PersianFormat.Time(b.Showtime!.StartsAt)} شروع می‌شه</b>\n\n{Header(b)}\n🔖 <code>{E(FormatCode(b.Code))}</code>\n\nبلیتت رو آماده داشته باش و یکم زودتر برس.";

    public static string AdminReceipt(Booking b, User u) =>
        "💳 <b>رسید پرداخت جدید</b>\n\n" +
        $"👤 {E(u.FullName)}" + (u.Username != "" ? $" · @{E(u.Username)}" : "") + "\n" + Header(b) + "\n\n" +
        $"💰 <b>{PersianFormat.Money(b.TotalPrice)}</b>\n" +
        (b.TrackingCode != "" ? $"🔢 شمارهٔ پیگیری: <code>{E(b.TrackingCode)}</code>\n" : "") +
        (b.PayerCard4 != "" ? $"💳 ۴ رقم آخر کارت پرداخت‌کننده: <code>{E(b.PayerCard4)}</code>\n" : "") +
        $"🧾 رزرو #{PersianFormat.Digits(b.Id.ToString())}";

    public static string ShowtimeChanged(Booking b, DateTime oldStart) =>
        $"⚠️ <b>زمان سانست عوض شد</b>\n\nزمان قبلی: {PersianFormat.Date(oldStart)} ⏰ {PersianFormat.Time(oldStart)}\n\n{Header(b)}\n\nبلیتت همچنان معتبره.";

    public static string FormatCode(string c) => c.Length == 8 ? $"{c[..4]}-{c[4..]}" : c;
    public static string FormatCard(string n) => n.Length == 16 ? string.Join(" ", Enumerable.Range(0, 4).Select(i => n.Substring(i * 4, 4))) : n;
}
