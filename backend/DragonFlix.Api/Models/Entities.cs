namespace DragonFlix.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.User;

    // --- تلگرام ---
    public long? TelegramId { get; set; }
    public string Username { get; set; } = "";
    public string LanguageCode { get; set; } = "";
    /// <summary>کاربر ربات را مسدود کرده؛ تا وقتی دوباره استارت نکند پیام نمی‌فرستیم.</summary>
    public bool BotBlocked { get; set; }
    /// <summary>فهرست اعلان‌های دلخواهِ روشن، جداشده با ویرگول. null یعنی تنظیمات پیش‌فرض.</summary>
    public string? NotifyPrefs { get; set; }
    public DateTime? LastLoginAt { get; set; }

    /// <summary>نام و نام خانوادگیِ واقعی که کاربر خودش ثبت می‌کند (روی بلیت چاپ می‌شود)؛ نام تلگرام اینجا جایگزین نمی‌شود.</summary>
    public string RegFirstName { get; set; } = "";
    public string RegLastName { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string RegisteredName => (RegFirstName + " " + RegLastName).Trim();
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsRegistered => RegFirstName != "" && RegLastName != "";
}

public static class Roles
{
    public const string User = "User";
    public const string Admin = "Admin";
}

public class Cinema
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public List<Hall> Halls { get; set; } = new();
}

/// <summary>سالن؛ هر سینما چند سالن دارد و هر سالن چینش ردیف‌های مخصوص خودش را.</summary>
public class Hall
{
    public int Id { get; set; }
    public int CinemaId { get; set; }
    public Cinema? Cinema { get; set; }
    public string Name { get; set; } = "";
    public List<SeatRow> Rows { get; set; } = new();
}

/// <summary>
/// یک ردیف صندلی. چینش با این فیلدها تعریف می‌شود:
/// SortOrder ترتیب از جلو (نزدیک پرده) به عقب، Offset تعداد خانهٔ خالی ابتدای ردیف،
/// AisleAfter شمارهٔ صندلی‌هایی که بعد از آن راهرو وجود دارد (مثلاً "3,9").
/// </summary>
public class SeatRow
{
    public int Id { get; set; }
    public int HallId { get; set; }
    public Hall? Hall { get; set; }
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
    public int SeatCount { get; set; }
    public decimal Price { get; set; }
    public int Offset { get; set; }
    public string AisleAfter { get; set; } = "";
    /// <summary>شمارهٔ صندلی‌های غیرفعال (خراب، ستون، ...) به‌صورت "2,5"؛ قابل رزرو نیستند.</summary>
    public string DisabledSeats { get; set; } = "";
}

public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>حداکثر ۳ ژانر، جداشده با ویرگول انگلیسی؛ اولی ژانر اصلی است. مثلاً "اکشن,درام,کمدی".</summary>
    public string Genre { get; set; } = "";
    public int DurationMinutes { get; set; }
    /// <summary>کاور: آدرس فایل ذخیره‌شده روی سرور (/uploads/movies/...) یا یک آدرس بیرونی.</summary>
    public string PosterUrl { get; set; } = "";
    /// <summary>تصویر پس‌زمینه (بک‌دراپ) با همان قاعدهٔ کاور.</summary>
    public string BackdropUrl { get; set; } = "";
    public string OriginalTitle { get; set; } = "";
    public int? ReleaseYear { get; set; }
    public int? TmdbId { get; set; }
    /// <summary>رده سنی: خالی (نامشخص) | all | 7 | 12 | 15 | 18</summary>
    public string AgeRating { get; set; } = "";
    /// <summary>توضیح ویژهٔ فیلم (مثلاً «با دوبله محبوب سورن»)؛ در اسلایدر، صفحهٔ فیلم و صفحهٔ رزرو نشان داده می‌شود. حداکثر ۱۲۰ کاراکتر.</summary>
    public string SpecialNote { get; set; } = "";
    /// <summary>اولین بار که سانس گرفت و به کاربران اعلام شد (اعلان «اکران فیلم جدید»).</summary>
    public DateTime? AnnouncedAt { get; set; }
}

/// <summary>یک سانس: نمایش یک فیلم در یک سالن در زمان مشخص. قیمت نهایی = قیمت ردیف × PriceFactor.</summary>
public class Showtime
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public Movie? Movie { get; set; }
    public int HallId { get; set; }
    public Hall? Hall { get; set; }
    public DateTime StartsAt { get; set; }
    public decimal PriceFactor { get; set; } = 1m;
}

public class Booking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ShowtimeId { get; set; }
    public Showtime? Showtime { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalPrice { get; set; }
    /// <summary>کد ۸ کاراکتری بلیت برای نمایش هنگام ورود (QR).</summary>
    public string Code { get; set; } = "";

    // --- پرداخت کارت‌به‌کارت ---
    public string Status { get; set; } = BookingStatus.PendingPayment;
    /// <summary>مهلت پرداخت؛ پس از آن رزرو منقضی و صندلی‌ها آزاد می‌شود.</summary>
    public DateTime? PayDeadline { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string TrackingCode { get; set; } = "";
    public string PayerCard4 { get; set; } = "";
    /// <summary>نام فایل رسید در App_Data/receipts (خصوصی؛ فقط با دسترسی مجاز خوانده می‌شود).</summary>
    public string ReceiptFile { get; set; } = "";
    public string RejectReason { get; set; } = "";
    /// <summary>نسخهٔ متنیِ صندلی‌ها (مثلاً "A1,A2") تا بعد از آزادشدن صندلی‌ها هم نمایش داده شود.</summary>
    public string SeatsText { get; set; } = "";
    /// <summary>زمان ثبت ورود در سینما؛ null یعنی هنوز استفاده نشده.</summary>
    public DateTime? CheckedInAt { get; set; }
    public List<BookedSeat> Seats { get; set; } = new();
}

public class BookedSeat
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int ShowtimeId { get; set; }
    public int SeatRowId { get; set; }
    public SeatRow? SeatRow { get; set; }
    public int SeatNumber { get; set; }
    public decimal Price { get; set; }
}

/// <summary>نگه‌داری موقت صندلی برای کاربری که در حال رزرو است («در حال رزرو»)؛ پس از ExpiresAt خودبه‌خود بی‌اثر می‌شود.</summary>
public class SeatHold
{
    public int Id { get; set; }
    public int ShowtimeId { get; set; }
    public int SeatRowId { get; set; }
    public int SeatNumber { get; set; }
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; } // UTC
}

/// <summary>یک اسلاید صفحهٔ اول: فیلمِ انتخاب‌شده توسط ادمین. توضیح ویژه روی خود فیلم (Movie.SpecialNote) است.</summary>
public class HomeSlide
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public Movie? Movie { get; set; }
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
}

public static class BookingStatus
{
    public const string PendingPayment = "PendingPayment"; // منتظر پرداخت کاربر
    public const string AwaitingReview = "AwaitingReview"; // رسید ثبت شد، منتظر تأیید مدیر
    public const string Confirmed = "Confirmed";           // پرداخت تأیید شد؛ بلیت صادر است
    public const string Rejected = "Rejected";             // مدیر رد کرد
    public const string Expired = "Expired";               // مهلت پرداخت تمام شد
    public const string Cancelled = "Cancelled";           // کاربر انصراف داد

    /// <summary>وضعیت‌هایی که صندلی را اشغال نگه می‌دارند.</summary>
    public static readonly string[] HoldsSeats = [PendingPayment, AwaitingReview, Confirmed];
}

/// <summary>تنظیمات کلید-مقدار (اطلاعات کارت، مهلت پرداخت، آدرس مینی‌اپ، کد راه‌اندازی ادمین، …).</summary>
public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>دنبال‌کردن فیلم برای دریافت اعلان سانس‌های جدید.</summary>
public class MovieFollow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int MovieId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>صف ارسال پیام تلگرام. dispatcher پیام‌های سررسیدشده را با نرخ مجاز می‌فرستد.</summary>
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Category { get; set; } = "";
    /// <summary>متن HTML تلگرام.</summary>
    public string Text { get; set; } = "";
    /// <summary>دکمه‌های اینلاین به‌صورت JSON (آرایهٔ ردیف‌ها).</summary>
    public string ButtonsJson { get; set; } = "";
    /// <summary>"" | "ticket" (تصویر بلیت) | "receipt" (عکس رسید برای مدیر)</summary>
    public string Media { get; set; } = "";
    public int? BookingId { get; set; }
    public bool Effect { get; set; }
    public string? DedupeKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime SendAfter { get; set; }
    public DateTime? SentAt { get; set; }
    public int Attempts { get; set; }
    public string Error { get; set; } = "";
}
