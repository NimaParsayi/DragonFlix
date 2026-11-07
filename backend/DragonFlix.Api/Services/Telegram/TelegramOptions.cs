namespace DragonFlix.Api.Services.Telegram;

public class TelegramOptions
{
    /// <summary>توکن ربات از BotFather. در appsettings.Local.json (خارج از گیت) یا متغیر محیطی Telegram__BotToken بگذارید.</summary>
    public string BotToken { get; set; } = "";
    /// <summary>آدرس HTTPS مینی‌اپ (مثلاً https://xxxx.ngrok-free.app). از پنل ادمین هم قابل تنظیم است.</summary>
    public string WebAppUrl { get; set; } = "";
    /// <summary>شناسهٔ عددی تلگرام ادمین‌ها (اختیاری؛ راه ساده‌تر لینک راه‌اندازی ادمین است).</summary>
    public long[] AdminIds { get; set; } = [];
    public string ApiBase { get; set; } = "https://api.telegram.org";
    /// <summary>حداکثر قدمت initData برای ورود (ساعت).</summary>
    /// <summary>استفاده از ایموجی متحرک (کاستوم) پک‌های زیر؛ در صورت نبودن اجازه خودکار به ایموجی عادی برمی‌گردد.</summary>
    public bool UseCustomEmoji { get; set; } = true;
    /// <summary>نام پک‌های ایموجی (t.me/addemoji/NAME) با ویرگول جدا شود.</summary>
    public string EmojiPacks { get; set; } = "RestrictedEmoji,FinanceEmoji";
    public int InitDataMaxAgeHours { get; set; } = 24;
}
