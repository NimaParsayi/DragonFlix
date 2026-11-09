using System.Text.RegularExpressions;

namespace DragonFlix.Api.Services;

/// <summary>نمایش خوانای صندلی: «ردیف G - صندلی ۶ و ۷» به‌جای «G6,G7».</summary>
public static partial class SeatFormat
{
    [GeneratedRegex(@"^(.*?)(\d+)$")]
    private static partial Regex Rx();

    public static (string Row, int Number) Parse(string label)
    {
        label = label.Trim();
        var i = label.LastIndexOf(':');
        if (i >= 0 && int.TryParse(label[(i + 1)..], out var n0)) return (label[..i], n0);
        var m = Rx().Match(label);
        return m.Success ? (m.Groups[1].Value, int.Parse(m.Groups[2].Value)) : (label.Trim(), 0);
    }

    /// <summary>هر ردیف یک خط: «ردیف G - صندلی ۶ و ۷». ترتیب ردیف‌ها همان ترتیب ورودی است.</summary>
    public static List<string> Lines(IEnumerable<(string Row, int Number)> seats)
    {
        var groups = new List<(string Row, List<int> Nums)>();
        foreach (var (row, n) in seats)
        {
            var g = groups.FindIndex(x => x.Row == row);
            if (g < 0) groups.Add((row, [n])); else groups[g].Nums.Add(n);
        }
        return Format(groups);
    }

    public static List<string> Lines(IEnumerable<string> labels) => Lines(labels.Select(Parse));

    private static List<string> Format(List<(string Row, List<int> Nums)> groups)
    {
        return groups.Select(g =>
        {
            var nums = g.Nums.Order().Select(n => PersianFormat.Digits(n.ToString())).ToList();
            var list = nums.Count == 1 ? nums[0] : string.Join("، ", nums.Take(nums.Count - 1)) + " و " + nums[^1];
            return $"ردیف {g.Row} - {(nums.Count == 1 ? "صندلی" : "صندلی‌های")} {list}";
        }).ToList();
    }

    public static List<string> Lines(string seatsText) =>
        Lines(seatsText.Split(',', StringSplitOptions.RemoveEmptyEntries));

    public static string Text(string seatsText) => string.Join("؛ ", Lines(seatsText));
}
