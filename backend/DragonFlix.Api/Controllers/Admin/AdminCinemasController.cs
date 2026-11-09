using DragonFlix.Api.Data;
using DragonFlix.Api.Dtos;
using DragonFlix.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Controllers.Admin;

[ApiController, Route("api/admin"), Authorize(Roles = Roles.Admin)]
public class AdminCinemasController(AppDbContext db) : ControllerBase
{
    // ---------- سینما ----------
    [HttpGet("cinemas")]
    public async Task<object> Cinemas() =>
        await db.Cinemas.OrderBy(c => c.Name).Select(c => new
        {
            c.Id, c.Name, c.City, c.Address,
            halls = c.Halls.OrderBy(h => h.Id).Select(h => new
            {
                h.Id, h.Name,
                rowCount = h.Rows.Count,
                seatCount = h.Rows.Sum(r => (int?)r.SeatCount) ?? 0
            })
        }).ToListAsync();

    [HttpPost("cinemas")]
    public async Task<IActionResult> CreateCinema(CinemaDto dto)
    {
        var c = new Cinema { Name = dto.Name.Trim(), City = dto.City.Trim(), Address = dto.Address?.Trim() ?? "" };
        db.Cinemas.Add(c);
        await db.SaveChangesAsync();
        return Ok(new { c.Id });
    }

    [HttpPut("cinemas/{id:int}")]
    public async Task<IActionResult> UpdateCinema(int id, CinemaDto dto)
    {
        var c = await db.Cinemas.FindAsync(id);
        if (c is null) return NotFound();
        c.Name = dto.Name.Trim(); c.City = dto.City.Trim(); c.Address = dto.Address?.Trim() ?? "";
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("cinemas/{id:int}")]
    public async Task<IActionResult> DeleteCinema(int id)
    {
        var c = await db.Cinemas.FindAsync(id);
        if (c is null) return NotFound();
        if (await db.Showtimes.AnyAsync(s => s.Hall!.CinemaId == id))
            return Conflict(new { message = "برای این سینما سانس ثبت شده است؛ ابتدا سانس‌ها را حذف کنید." });
        db.Cinemas.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- سالن ----------
    [HttpPost("cinemas/{cinemaId:int}/halls")]
    public async Task<IActionResult> CreateHall(int cinemaId, HallDto dto)
    {
        if (!await db.Cinemas.AnyAsync(c => c.Id == cinemaId)) return NotFound();
        var h = new Hall { CinemaId = cinemaId, Name = dto.Name.Trim() };
        db.Halls.Add(h);
        await db.SaveChangesAsync();
        return Ok(new { h.Id });
    }

    [HttpPut("halls/{id:int}")]
    public async Task<IActionResult> RenameHall(int id, HallDto dto)
    {
        var h = await db.Halls.FindAsync(id);
        if (h is null) return NotFound();
        h.Name = dto.Name.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("halls/{id:int}")]
    public async Task<IActionResult> DeleteHall(int id)
    {
        var h = await db.Halls.FindAsync(id);
        if (h is null) return NotFound();
        if (await db.Showtimes.AnyAsync(s => s.HallId == id))
            return Conflict(new { message = "برای این سالن سانس ثبت شده است؛ ابتدا سانس‌ها را حذف کنید." });
        db.Halls.Remove(h);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- طراحی چینش ردیف‌ها ----------
    [HttpGet("halls/{id:int}/layout")]
    public async Task<IActionResult> GetLayout(int id)
    {
        var h = await db.Halls.Include(x => x.Cinema).Include(x => x.Rows).FirstOrDefaultAsync(x => x.Id == id);
        if (h is null) return NotFound();
        return Ok(new
        {
            hall = new { h.Id, h.Name, cinema = h.Cinema!.Name },
            rows = h.Rows.OrderBy(r => r.SortOrder).Select(r => new RowLayoutDto(
                r.Id, r.Label, r.SeatCount, r.Price, r.Offset, CatalogController.ParseAisles(r.AisleAfter),
                CatalogController.ParseAisles(r.DisabledSeats)))
        });
    }

    /// <summary>
    /// کل چینش سالن را جایگزین می‌کند. ترتیب لیست = ترتیب ردیف‌ها از پرده به عقب سالن.
    /// ردیفی که برایش صندلی رزرو شده نه حذف می‌شود و نه تعداد صندلی‌اش به کمتر از بالاترین شمارهٔ رزروشده می‌رسد.
    /// </summary>
    [HttpPut("halls/{id:int}/layout")]
    public async Task<IActionResult> SaveLayout(int id, HallLayoutDto dto)
    {
        var hall = await db.Halls.Include(h => h.Rows).FirstOrDefaultAsync(h => h.Id == id);
        if (hall is null) return NotFound();

        var error = Validate(dto);
        if (error is not null) return BadRequest(new { message = error });

        var incomingIds = dto.Rows.Where(r => r.Id.HasValue).Select(r => r.Id!.Value).ToHashSet();
        if (incomingIds.Any(i => hall.Rows.All(r => r.Id != i)))
            return BadRequest(new { message = "ردیف نامعتبر در درخواست وجود دارد." });

        var bookedSeats = await db.BookedSeats.Where(b => b.SeatRow!.HallId == id)
            .Select(b => new { b.SeatRowId, b.SeatNumber }).Distinct().ToListAsync();
        var booked = bookedSeats.GroupBy(x => x.SeatRowId).ToDictionary(g => g.Key, g => g.Max(x => x.SeatNumber));

        foreach (var r in hall.Rows.Where(r => !incomingIds.Contains(r.Id)))
            if (booked.ContainsKey(r.Id))
                return Conflict(new { message = $"ردیف {r.Label} صندلی رزروشده دارد و قابل حذف نیست." });

        for (var i = 0; i < dto.Rows.Count; i++)
        {
            var d = dto.Rows[i];
            if (d.Id is int rid && booked.TryGetValue(rid, out var max) && d.SeatCount < max)
                return Conflict(new { message = $"در ردیف {d.Label} صندلی شمارهٔ {max} رزرو شده؛ تعداد صندلی‌ها نباید کمتر شود." });
            if (d.Id is int rid2)
            {
                var clash = (d.Disabled ?? new()).FirstOrDefault(n => bookedSeats.Any(b => b.SeatRowId == rid2 && b.SeatNumber == n));
                if (clash != 0)
                    return Conflict(new { message = $"صندلی {d.Label}{clash} رزرو شده و نمی‌تواند غیرفعال شود." });
            }
        }

        db.SeatRows.RemoveRange(hall.Rows.Where(r => !incomingIds.Contains(r.Id)));

        for (var i = 0; i < dto.Rows.Count; i++)
        {
            var d = dto.Rows[i];
            var row = d.Id.HasValue ? hall.Rows.First(r => r.Id == d.Id.Value) : new SeatRow { HallId = id };
            row.Label = d.Label.Trim();
            row.SortOrder = i;
            row.SeatCount = d.SeatCount;
            row.Price = d.Price;
            row.Offset = d.Offset;
            row.AisleAfter = string.Join(",", d.AisleAfter.Distinct().OrderBy(x => x));
            row.DisabledSeats = string.Join(",", (d.Disabled ?? new()).Distinct().OrderBy(x => x));
            if (!d.Id.HasValue) db.SeatRows.Add(row);
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(HallLayoutDto dto)
    {
        if (dto.Rows is null || dto.Rows.Count > 40) return "تعداد ردیف‌ها باید بین ۰ تا ۴۰ باشد.";
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in dto.Rows)
        {
            if (string.IsNullOrWhiteSpace(r.Label)) return "نام هر ردیف الزامی است.";
            if (r.Label.AsSpan().IndexOfAny(',', ':') >= 0) return "نام ردیف نباید شامل «،» یا «:» باشد.";
            if (!labels.Add(r.Label.Trim())) return $"نام ردیف «{r.Label}» تکراری است.";
            if (r.SeatCount is < 1 or > 60) return $"تعداد صندلی‌های ردیف {r.Label} باید بین ۱ تا ۶۰ باشد.";
            if (r.Price < 0) return $"قیمت ردیف {r.Label} نمی‌تواند منفی باشد.";
            if (r.Offset is < 0 or > 30) return $"فاصلهٔ ابتدای ردیف {r.Label} نامعتبر است.";
            if (r.AisleAfter is null || r.AisleAfter.Any(a => a < 1 || a >= r.SeatCount))
                return $"محل راهرو در ردیف {r.Label} نامعتبر است.";
            if (r.Disabled is not null && r.Disabled.Any(a => a < 1 || a > r.SeatCount))
                return $"شمارهٔ صندلی غیرفعال در ردیف {r.Label} نامعتبر است.";
        }
        return null;
    }
}
