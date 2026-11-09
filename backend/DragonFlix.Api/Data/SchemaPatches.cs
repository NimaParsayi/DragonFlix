using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Data;

/// <summary>
/// EnsureCreated ستون‌ها و جدول‌های تازه را به دیتابیس موجود اضافه نمی‌کند. این وصله‌ها فقط چیزِ جاافتاده را اضافه می‌کنند
/// تا با تغییر مدل، داده‌های موجود (فیلم‌ها، رزروها، …) پاک نشوند.
/// </summary>
public static class SchemaPatches
{
    public static void Apply(AppDbContext db)
    {
        AddColumn(db, "Movies", "AgeRating", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, "Movies", "SpecialNote", "TEXT NOT NULL DEFAULT ''");

        Exec(db, """
            CREATE TABLE IF NOT EXISTS "HomeSlides" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_HomeSlides" PRIMARY KEY AUTOINCREMENT,
                "MovieId" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "Active" INTEGER NOT NULL,
                CONSTRAINT "FK_HomeSlides_Movies_MovieId" FOREIGN KEY ("MovieId") REFERENCES "Movies" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_HomeSlides_MovieId" ON "HomeSlides" ("MovieId");
            """);

        PatchTelegramAndPayments(db);

        // نسخهٔ قبلی توضیح ویژه را روی خود اسلاید نگه می‌داشت؛ آن را به فیلم منتقل می‌کنیم و ستون قدیمی را برمی‌داریم.
        if (HasColumn(db, "HomeSlides", "Note"))
        {
            Exec(db, """
                UPDATE "Movies" SET "SpecialNote" = (SELECT "Note" FROM "HomeSlides" WHERE "MovieId" = "Movies"."Id")
                WHERE "SpecialNote" = '' AND EXISTS (SELECT 1 FROM "HomeSlides" WHERE "MovieId" = "Movies"."Id" AND "Note" <> '');
                """);
            Exec(db, "ALTER TABLE \"HomeSlides\" DROP COLUMN \"Note\";");
        }
    }

    /// <summary>ستون‌ها و جدول‌های مربوط به تلگرام، پرداخت کارت‌به‌کارت و اعلان‌ها.</summary>
    private static void PatchTelegramAndPayments(AppDbContext db)
    {
        AddColumn(db, "Users", "TelegramId", "INTEGER NULL");
        AddColumn(db, "Users", "Username", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, "Users", "LanguageCode", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, "Users", "BotBlocked", "INTEGER NOT NULL DEFAULT 0");
        AddColumn(db, "Users", "NotifyPrefs", "TEXT NULL");
        AddColumn(db, "Users", "LastLoginAt", "TEXT NULL");
        AddColumn(db, "Users", "RegFirstName", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, "Users", "RegLastName", "TEXT NOT NULL DEFAULT ''");
        Exec(db, "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Users_TelegramId\" ON \"Users\" (\"TelegramId\")");

        // رزروهای قدیمی پیش از این قابلیت، پرداخت‌شده و قطعی بودند (پیش‌فرض Confirmed)
        var addedStatus = AddColumn(db, "Bookings", "Status", "TEXT NOT NULL DEFAULT 'Confirmed'");
        AddColumn(db, "Bookings", "PayDeadline", "TEXT NULL");
        AddColumn(db, "Bookings", "SubmittedAt", "TEXT NULL");
        AddColumn(db, "Bookings", "ReviewedAt", "TEXT NULL");
        AddColumn(db, "Bookings", "ConfirmedAt", "TEXT NULL");
        AddColumn(db, "Bookings", "ReviewedByUserId", "INTEGER NULL");
        AddColumn(db, "Bookings", "TrackingCode", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, "Bookings", "PayerCard4", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, "Bookings", "ReceiptFile", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, "Bookings", "RejectReason", "TEXT NOT NULL DEFAULT ''");
        var addedSeats = AddColumn(db, "Bookings", "SeatsText", "TEXT NOT NULL DEFAULT ''");
        if (addedStatus) Exec(db, "UPDATE \"Bookings\" SET \"ConfirmedAt\" = \"CreatedAt\" WHERE \"ConfirmedAt\" IS NULL");
        if (addedSeats)
            Exec(db, """
                UPDATE "Bookings" SET "SeatsText" = COALESCE((
                    SELECT group_concat(r."Label" || bs."SeatNumber", ',') FROM "BookedSeats" bs
                    JOIN "SeatRows" r ON r."Id" = bs."SeatRowId" WHERE bs."BookingId" = "Bookings"."Id"), '')
                """);

        // قالب بدون ابهام «ردیف:شماره»؛ وقتی نام ردیف عدد باشد «2»+«7» با «27» اشتباه گرفته می‌شد
        Exec(db, """
            UPDATE "Bookings" SET "SeatsText" = COALESCE((
                SELECT group_concat(r."Label" || ':' || bs."SeatNumber", ',') FROM "BookedSeats" bs
                JOIN "SeatRows" r ON r."Id" = bs."SeatRowId" WHERE bs."BookingId" = "Bookings"."Id"), "SeatsText")
            WHERE "SeatsText" NOT LIKE '%:%' AND EXISTS (SELECT 1 FROM "BookedSeats" WHERE "BookingId" = "Bookings"."Id")
            """);

        // فیلم‌هایی که از قبل سانس دارند «قبلاً اعلام‌شده» حساب می‌شوند تا اعلان اکران جدید الکی برای همه نرود
        var addedAnnounced = AddColumn(db, "Movies", "AnnouncedAt", "TEXT NULL");
        if (addedAnnounced)
            Exec(db, "UPDATE \"Movies\" SET \"AnnouncedAt\" = datetime('now') WHERE EXISTS (SELECT 1 FROM \"Showtimes\" WHERE \"MovieId\" = \"Movies\".\"Id\")");

        Exec(db, """
            CREATE TABLE IF NOT EXISTS "AppSettings" ("Key" TEXT NOT NULL CONSTRAINT "PK_AppSettings" PRIMARY KEY, "Value" TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS "MovieFollows" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MovieFollows" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL, "MovieId" INTEGER NOT NULL, "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_MovieFollows_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_MovieFollows_Movies_MovieId" FOREIGN KEY ("MovieId") REFERENCES "Movies" ("Id") ON DELETE CASCADE);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MovieFollows_UserId_MovieId" ON "MovieFollows" ("UserId", "MovieId");
            CREATE TABLE IF NOT EXISTS "Notifications" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Notifications" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL, "Category" TEXT NOT NULL, "Text" TEXT NOT NULL, "ButtonsJson" TEXT NOT NULL,
                "Media" TEXT NOT NULL, "BookingId" INTEGER NULL, "Effect" INTEGER NOT NULL, "DedupeKey" TEXT NULL,
                "CreatedAt" TEXT NOT NULL, "SendAfter" TEXT NOT NULL, "SentAt" TEXT NULL, "Attempts" INTEGER NOT NULL, "Error" TEXT NOT NULL,
                CONSTRAINT "FK_Notifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Notifications_DedupeKey" ON "Notifications" ("DedupeKey");
            CREATE INDEX IF NOT EXISTS "IX_Notifications_SentAt_SendAfter" ON "Notifications" ("SentAt", "SendAfter");
            """);
    }

    private static void Exec(AppDbContext db, string sql) => db.Database.ExecuteSqlRaw(sql);

    private static bool HasColumn(AppDbContext db, string table, string column)
    {
        var conn = db.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed) conn.Open();
        try
        {
            using var check = conn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
            return Convert.ToInt32(check.ExecuteScalar()) > 0;
        }
        finally { if (wasClosed) conn.Close(); }
    }

    /// <summary>ستون را اگر نبود اضافه می‌کند؛ true یعنی همین الان اضافه شد.</summary>
    private static bool AddColumn(AppDbContext db, string table, string column, string definition)
    {
        if (HasColumn(db, table, column)) return false;
        Exec(db, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}");
        return true;
    }
}
