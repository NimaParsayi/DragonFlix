using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace DragonFlix.Api.Controllers;

/// <summary>لوگوی بانک‌ها برای تگ img (بدون توکن)؛ فقط فایل‌های پوشهٔ Assets/BankLogos با نام‌های امن.</summary>
[ApiController, Route("api/bank-logo")]
public partial class BankLogoController : ControllerBase
{
    [GeneratedRegex("^[a-z-]{2,30}$")]
    private static partial Regex Safe();

    [HttpGet("{code}")]
    [ResponseCache(Duration = 86400)]
    public IActionResult Get(string code)
    {
        if (!Safe().IsMatch(code)) return NotFound();
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "BankLogos", code + ".jpg");
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/jpeg") : NotFound();
    }
}
