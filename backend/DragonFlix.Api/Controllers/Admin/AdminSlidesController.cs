using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

/// <summary>مدیریت اسلایدر صفحهٔ اول: انتخاب فیلم‌ها، ترتیب و توضیح ویژهٔ هر اسلاید.</summary>
[ApiController, Route("api/admin/slides"), Authorize(Roles = Roles.Admin)]
public class AdminSlidesController(AppDbContext db) : ControllerBase
{
    private const int MaxSlides = 8;

    [HttpGet]
    public async Task<object> List()
    {
        var now = DateTime.Now;
        return await db.HomeSlides.OrderBy(s => s.SortOrder).Select(s => new
        {
            s.Id, s.MovieId, Note = s.Movie!.SpecialNote, s.Active, s.SortOrder,
            movie = new { s.Movie.Title, s.Movie.Genre, s.Movie.PosterUrl, s.Movie.BackdropUrl, s.Movie.AgeRating },
            upcomingShowtimes = db.Showtimes.Count(x => x.MovieId == s.MovieId && x.StartsAt > now),
        }).ToListAsync();
    }

    [HttpPost]
    public async Task<IActionResult> Create(SlideInputDto dto)
    {
        var movie = await db.Movies.FindAsync(dto.MovieId);
        if (movie is null) return BadRequest(new { message = "فیلم نامعتبر است." });
        if (await db.HomeSlides.AnyAsync(s => s.MovieId == dto.MovieId)) return Conflict(new { message = "این فیلم قبلاً در اسلایدر هست." });
        if (await db.HomeSlides.CountAsync() >= MaxSlides) return Conflict(new { message = $"حداکثر {MaxSlides} اسلاید مجاز است." });

        var next = (await db.HomeSlides.MaxAsync(s => (int?)s.SortOrder) ?? -1) + 1;
        // توضیح ویژه روی خود فیلم ذخیره می‌شود (در صفحهٔ فیلم و رزرو هم دیده می‌شود)
        if (dto.Note is not null) movie.SpecialNote = dto.Note.Trim();
        var s = new HomeSlide { MovieId = dto.MovieId, SortOrder = next, Active = true };
        db.HomeSlides.Add(s);
        await db.SaveChangesAsync();
        return Ok(new { s.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SlideUpdateDto dto)
    {
        var s = await db.HomeSlides.Include(x => x.Movie).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        s.Movie!.SpecialNote = dto.Note?.Trim() ?? "";
        s.Active = dto.Active;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>ترتیب جدید: لیست شناسهٔ اسلایدها به ترتیب نمایش.</summary>
    [HttpPut("order")]
    public async Task<IActionResult> Reorder(SlideOrderDto dto)
    {
        var slides = await db.HomeSlides.ToListAsync();
        for (var i = 0; i < dto.Ids.Count; i++)
        {
            var slide = slides.FirstOrDefault(s => s.Id == dto.Ids[i]);
            if (slide is not null) slide.SortOrder = i;
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await db.HomeSlides.FindAsync(id);
        if (s is null) return NotFound();
        db.HomeSlides.Remove(s);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
