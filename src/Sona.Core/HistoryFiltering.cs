using System.Globalization;

namespace Sona.Core;

public enum HistoryRange { Today, Last7Days, Last30Days, All, SpecificDay }
public readonly record struct HistoryFilter(HistoryRange Range, DateOnly? Day = null);

/// <summary>Calendar ranges for recent history. Explicitly saved drafts stay separate.</summary>
public static class HistoryFiltering
{
    public static DateOnly LocalDay(DateTimeOffset timestamp, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, zone).DateTime);

    public static bool Matches(DateTimeOffset timestamp, HistoryFilter filter, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (filter.Range == HistoryRange.All) return true;
        var date = LocalDay(timestamp, zone); var today = LocalDay(now, zone);
        return filter.Range switch
        {
            HistoryRange.Today => date == today,
            HistoryRange.Last7Days => date >= today.AddDays(-6) && date <= today,
            HistoryRange.Last30Days => date >= today.AddDays(-29) && date <= today,
            HistoryRange.SpecificDay => filter.Day.HasValue && date == filter.Day.Value,
            _ => false
        };
    }

    public static List<HistoryItem> Visible(IEnumerable<HistoryItem> entries, HistoryFilter filter, string search, DateTimeOffset now, TimeZoneInfo zone) =>
        HistoryStore.Search(entries.Where(item => item.Saved || Matches(item.CreatedAt, filter, now, zone)), search)
            .OrderByDescending(item => item.Saved).ThenByDescending(item => item.CreatedAt).ToList();

    public static Guid[] ClearableIds(IEnumerable<HistoryItem> entries, HistoryFilter filter, string search, DateTimeOffset now, TimeZoneInfo zone) =>
        Visible(entries, filter, search, now, zone).Where(item => !item.Saved).Select(item => item.Id).Distinct().ToArray();

    public static string GroupLabel(HistoryItem item, TimeZoneInfo zone, CultureInfo? culture = null) => item.Saved
        ? "Saved drafts"
        : LocalDay(item.CreatedAt, zone).ToDateTime(TimeOnly.MinValue).ToString("dddd, MMMM d, yyyy", culture ?? CultureInfo.CurrentCulture);

    public static string RangeLabel(HistoryFilter filter, CultureInfo? culture = null) => filter.Range switch
    {
        HistoryRange.Today => "Today",
        HistoryRange.Last7Days => "Last 7 days",
        HistoryRange.Last30Days => "Last 30 days",
        HistoryRange.SpecificDay => filter.Day?.ToString("MMM d, yyyy", culture ?? CultureInfo.CurrentCulture) ?? "Choose a day",
        _ => "All recent history"
    };
}
