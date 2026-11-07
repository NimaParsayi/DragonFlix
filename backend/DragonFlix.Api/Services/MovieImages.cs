using System.Text.RegularExpressions;

namespace DragonFlix.Api.Services;

/// <summary>
/// ذخیرهٔ کاور و بک‌دراپ فیلم‌ها روی دیسک سرور (wwwroot/uploads/movies). فقط فایل‌هایی که خودمان ساخته‌ایم
/// (نام تصادفی داخل همین پوشه) حذف می‌شوند؛ آدرس‌های بیرونی هرگز دست نمی‌خورند.
/// </summary>
public partial class MovieImages(IWebHostEnvironment env)
{
    public const string Prefix = "/uploads/movies/";

    [GeneratedRegex(@"^/uploads/movies/[a-f0-9]{32}\.(jpg|png|webp)$")]
    private static partial Regex Managed();

    private string Dir => Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads", "movies");

    public static bool IsManaged(string? url) => url is not null && Managed().IsMatch(url);

    /// <summary>نوع تصویر را از روی محتوای فایل (نه نام یا Content-Type) تشخیص می‌دهد.</summary>
    private static string? Detect(byte[] b)
    {
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "jpg";
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "png";
        if (b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "webp";
        return null;
    }

    public async Task<string> SaveAsync(byte[] bytes)
    {
        var ext = Detect(bytes) ?? throw new InvalidDataException("فقط تصویر JPG، PNG یا WEBP پذیرفته می‌شود.");
        Directory.CreateDirectory(Dir);
        var name = $"{Guid.NewGuid():N}.{ext}";
        await File.WriteAllBytesAsync(Path.Combine(Dir, name), bytes);
        return Prefix + name;
    }

    public void DeleteIfManaged(string? url)
    {
        if (!IsManaged(url)) return;
        try { File.Delete(Path.Combine(Dir, Path.GetFileName(url!))); } catch (IOException) { /* بعداً دستی پاک می‌شود */ }
    }
}
