using DragonFlix.Api.Models;

namespace DragonFlix.Api.Services;

public record NotifyCategory(string Key, string Title, string Description, bool Mandatory, bool DefaultOn);

/// <summary>
/// دسته‌های اعلان. دستهٔ ضروری همیشه ارسال می‌شود و کاربر نمی‌تواند خاموشش کند (مرتبط با پول و بلیت)؛
/// بقیه را کاربر در «حساب من» روشن/خاموش می‌کند.
/// </summary>
public static class NotificationCategories
{
    public static readonly NotifyCategory[] All =
    [
        new("payment", "پرداخت و رسید", "ثبت رسید، تأیید یا ردِ پرداخت و هشدار پایان مهلت", true, true),
        new("ticket", "صدور بلیت", "فرستادن بلیت بعد از تأیید پرداخت", true, true),
        new("booking_change", "تغییر سانس رزروشده", "اگه زمان یا برنامهٔ سانسی که رزرو کردی عوض بشه", true, true),
        new("reminder", "یادآوری پیش از سانس", "سه ساعت قبل از شروع فیلمی که بلیتش رو داری", false, true),
        new("new_movie", "اکران فیلم جدید", "وقتی فیلم تازه‌ای اکران می‌شه", false, true),
        new("new_showtimes", "سانس جدید و تمدید", "وقتی برای فیلم‌هایی که دنبال کردی یا دیدی سانس تازه اضافه می‌شه", false, true),
        new("promo", "تخفیف و پیشنهاد ویژه", "خبر تخفیف‌ها و پیشنهادهای ویژه", false, false),
    ];

    public static NotifyCategory? Find(string key) => All.FirstOrDefault(c => c.Key == key);

    private static HashSet<string> Defaults() => All.Where(c => !c.Mandatory && c.DefaultOn).Select(c => c.Key).ToHashSet();

    /// <summary>دسته‌های دلخواهِ روشنِ این کاربر.</summary>
    public static HashSet<string> EnabledOptional(User u) =>
        u.NotifyPrefs is null ? Defaults()
        : u.NotifyPrefs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => Find(k) is { Mandatory: false }).ToHashSet();

    /// <summary>آیا این دسته به این کاربر ارسال شود؟ دسته‌های ضروری و ناشناخته (مثل admin) همیشه بله.</summary>
    public static bool Allows(User u, string category)
    {
        var c = Find(category);
        return c is null || c.Mandatory || EnabledOptional(u).Contains(category);
    }
}
