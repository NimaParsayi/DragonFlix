using DragonFlix.Api.Controllers;
using DragonFlix.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Services;

public static class HallCapacity
{
    /// <summary>ظرفیت قابل‌فروش هر سالن = مجموع صندلی‌ها منهای صندلی‌های غیرفعال.</summary>
    public static async Task<Dictionary<int, int>> ForHalls(AppDbContext db, IEnumerable<int> hallIds)
    {
        var ids = hallIds.Distinct().ToList();
        var rows = await db.SeatRows.Where(r => ids.Contains(r.HallId))
            .Select(r => new { r.HallId, r.SeatCount, r.DisabledSeats }).ToListAsync();
        return rows.GroupBy(r => r.HallId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.SeatCount - CatalogController.ParseAisles(r.DisabledSeats).Count));
    }
}
