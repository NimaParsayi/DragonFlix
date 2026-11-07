using DragonFlix.Api.Models;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DragonFlix.Api.Controllers.Admin;

/// <summary>جست‌وجو و دریافت مشخصات فیلم از TMDB (اختیاری؛ در نبود آن، ورود دستی کار می‌کند).</summary>
[ApiController, Route("api/admin/tmdb"), Authorize(Roles = Roles.Admin)]
public class AdminTmdbController(TmdbClient tmdb) : ControllerBase
{
    [HttpGet("status")]
    public object Status() => new { configured = tmdb.Configured, imageBase = tmdb.ImageBase };

    [HttpGet("search")]
    public async Task<IActionResult> Search(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Ok(Array.Empty<object>());
        try { return Ok(await tmdb.SearchAsync(q.Trim())); }
        catch (TmdbException e) { return StatusCode(e.Status, new { message = e.Message }); }
    }

    [HttpGet("movie/{id:int}")]
    public async Task<IActionResult> Movie(int id)
    {
        try { return Ok(await tmdb.DetailsAsync(id)); }
        catch (TmdbException e) { return StatusCode(e.Status, new { message = e.Message }); }
    }
}
