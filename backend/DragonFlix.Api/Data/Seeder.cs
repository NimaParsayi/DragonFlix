using DragonFlix.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace DragonFlix.Api.Data;

public static class Seeder
{
    public static void Seed(AppDbContext db, IConfiguration cfg)
    {
        if (db.Users.Any()) return;

        var hasher = new PasswordHasher<User>();
        var admin = new User
        {
            Email = cfg["Seed:AdminEmail"] ?? "admin@dragonflix.test",
            FullName = "مدیر سیستم",
            Role = Roles.Admin
        };
        admin.PasswordHash = hasher.HashPassword(admin, cfg["Seed:AdminPassword"] ?? "Admin123!");
        db.Users.Add(admin);

        // به‌صورت پیش‌فرض فقط حساب ادمین ساخته می‌شود؛ برای داده‌های نمونه Seed:DemoData را true کنید.
        if (!cfg.GetValue<bool>("Seed:DemoData"))
        {
            db.SaveChanges();
            return;
        }

        static SeatRow R(string l, int order, int n, decimal p, int off = 0, string aisle = "", string disabled = "") =>
            new() { Label = l, SortOrder = order, SeatCount = n, Price = p, Offset = off, AisleAfter = aisle, DisabledSeats = disabled };

        var azadi = new Cinema
        {
            Name = "سینما آزادی", City = "تهران", Address = "خیابان ولیعصر",
            Halls =
            {
                new Hall { Name = "سالن ۱ (بزرگ)", Rows =
                {
                    R("A", 0, 10, 150_000, 1, "5"), R("B", 1, 12, 150_000, 0, "6"), R("C", 2, 12, 180_000, 0, "6"),
                    R("D", 3, 12, 180_000, 0, "6", "1,2"), R("E", 4, 12, 220_000, 0, "6"), R("F", 5, 10, 300_000, 1, "5"),
                }},
                new Hall { Name = "سالن ۲ (VIP)", Rows =
                {
                    R("A", 0, 6, 350_000, 1, "3"), R("B", 1, 6, 350_000, 1, "3"), R("C", 2, 6, 450_000, 1, "3"),
                }},
            }
        };
        var mellat = new Cinema
        {
            Name = "سینما ملت", City = "اصفهان", Address = "میدان نقش جهان",
            Halls =
            {
                new Hall { Name = "سالن اصلی", Rows =
                {
                    R("A", 0, 8, 120_000, 2, "4"), R("B", 1, 10, 120_000, 1, "5"), R("C", 2, 12, 140_000, 0, "4,8"),
                    R("D", 3, 12, 160_000, 0, "4,8"),
                }},
            }
        };
        db.Cinemas.AddRange(azadi, mellat);

        static Movie M(string t, string g, int d, string desc) =>
            new() { Title = t, Genre = g, DurationMinutes = d, Description = desc, PosterUrl = "" };
        var movies = new[]
        {
            M("اژدهای آبی", "انیمیشن", 98, "دختری کوچک و اژدهایی که از آب‌ها می‌ترسد، برای نجات دهکدهٔ ساحلی‌شان سفری شگفت‌انگیز را آغاز می‌کنند."),
            M("شب‌های شهر", "درام", 104, "داستان چند غریبه که در یک شب بارانی مسیرهایشان به هم گره می‌خورد و هیچ‌کدام مثل قبل بازنمی‌گردند."),
            M("ستارهٔ دور", "علمی‌تخیلی", 132, "تیمی از فضانوردان برای نجات زمین به سمت ستاره‌ای ناشناخته پرواز می‌کنند؛ سفری بی‌بازگشت."),
            M("تعقیب در تهران", "اکشن", 115, "یک راننده‌ی سابق مسابقات، در یک شب باید از میان ترافیک و خطر، محموله‌ای حیاتی را به مقصد برساند."),
            M("خنده در باران", "کمدی", 96, "سه دوست قدیمی برای یک عروسی به شهری کوچک می‌روند و همه‌چیز از همان ساعت اول خراب می‌شود."),
            M("عمارت مه‌آلود", "ترسناک", 101, "خانواده‌ای در عمارتی قدیمی در دل جنگل ساکن می‌شوند؛ جایی که شب‌ها کسی در راهروها قدم می‌زند."),
            M("ماجرای کوهستان", "ماجراجویی", 124, "گروهی کوهنورد در جست‌وجوی دره‌ای افسانه‌ای، با رازی قدیمی روبه‌رو می‌شوند."),
            M("نامه‌هایی به دریا", "عاشقانه", 108, "نامه‌های یک ناخدا که سال‌ها بعد به دست دختری می‌رسد و زندگی او را تغییر می‌دهد."),
        };
        db.Movies.AddRange(movies);
        db.SaveChanges();

        var halls = db.Halls.ToList();
        var today = DateTime.Today;
        var rnd = new Random(7);
        for (var d = 1; d <= 6; d++)
            foreach (var h in halls)
                foreach (var (hour, factor) in new[] { (13, 0.8m), (16, 1m), (19, 1.2m), (22, 1.3m) })
                {
                    var m = movies[rnd.Next(movies.Length)];
                    db.Showtimes.Add(new Showtime
                    {
                        MovieId = m.Id, HallId = h.Id,
                        StartsAt = today.AddDays(d).AddHours(hour), PriceFactor = factor
                    });
                }
        db.SaveChanges();
    }
}
