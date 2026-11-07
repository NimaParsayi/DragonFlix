namespace DragonFlix.Api.Services;

/// <summary>
/// ذخیرهٔ تصویر رسید پرداخت. برخلاف کاور فیلم‌ها، رسیدها داخل wwwroot نیستند (عمومی نمی‌شوند)
/// و فقط از طریق API با دسترسی صاحب رزرو یا ادمین خوانده می‌شوند.
/// </summary>
public class ReceiptStore(IWebHostEnvironment env)
{
    public const int MaxBytes = 6 * 1024 * 1024;
    private string Dir => Path.Combine(env.ContentRootPath, "App_Data", "receipts");

    private static (string Ext, string Mime)? Detect(byte[] b)
    {
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("jpg", "image/jpeg");
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ("png", "image/png");
        if (b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ("webp", "image/webp");
        return null;
    }

    public async Task<string> SaveAsync(byte[] bytes)
    {
        var t = Detect(bytes) ?? throw new InvalidDataException("فقط تصویر JPG، PNG یا WEBP پذیرفته می‌شود.");
        Directory.CreateDirectory(Dir);
        var name = $"{Guid.NewGuid():N}.{t.Ext}";
        await File.WriteAllBytesAsync(Path.Combine(Dir, name), bytes);
        return name;
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadAsync(string name)
    {
        if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name)) return null; // جلوگیری از پیمایش مسیر
        var path = Path.Combine(Dir, name);
        if (!File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path);
        return (bytes, Detect(bytes)?.Mime ?? "application/octet-stream");
    }
}
