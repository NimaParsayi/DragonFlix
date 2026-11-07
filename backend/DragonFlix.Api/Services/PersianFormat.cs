using System.Globalization;

namespace DragonFlix.Api.Services;

/// <summary>قالب‌بندی تاریخ شمسی، ارقام فارسی و مبلغ برای رسید PDF.</summary>
public static class PersianFormat
{
    private static readonly PersianCalendar Pc = new();
    private static readonly string[] Months =
        ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];
    private static readonly string[] Days =
        ["یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه"];

    public static string Digits(string s) =>
        string.Create(s.Length, s, (span, src) =>
        {
            for (var i = 0; i < src.Length; i++)
                span[i] = src[i] is >= '0' and <= '9' ? (char)('۰' + (src[i] - '0')) : src[i];
        });

    public static string Date(DateTime d) =>
        Digits($"{Days[(int)d.DayOfWeek]} {Pc.GetDayOfMonth(d)} {Months[Pc.GetMonth(d) - 1]} {Pc.GetYear(d)}");

    public static string Time(DateTime d) => Digits(d.ToString("HH:mm", CultureInfo.InvariantCulture));

    public static string Money(decimal v) =>
        Digits(Math.Round(v).ToString("N0", CultureInfo.InvariantCulture).Replace(",", "٬")) + " تومان";
}
