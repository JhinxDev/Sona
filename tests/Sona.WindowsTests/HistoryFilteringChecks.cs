using System.Globalization;
using Sona.Core;

internal static class HistoryFilteringChecks
{
    internal static int Run()
    {
        var passed = 0;
        void Check(bool value, string label) { if (!value) throw new IOException(label); Console.WriteLine("PASS " + label); passed++; }
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var now = DateTimeOffset.Parse("2026-10-15T12:00:00-04:00", CultureInfo.InvariantCulture);
        var today = new HistoryFilter(HistoryRange.Today);
        var seven = new HistoryFilter(HistoryRange.Last7Days); var thirty = new HistoryFilter(HistoryRange.Last30Days);
        DateTimeOffset At(int days) => now.AddDays(days);
        Check(HistoryFiltering.LocalDay(DateTimeOffset.Parse("2026-10-15T02:30:00Z", CultureInfo.InvariantCulture), zone) == new DateOnly(2026, 10, 14), "Calendar grouping uses local dates rather than UTC dates");
        Check(HistoryFiltering.Matches(DateTimeOffset.Parse("2026-10-15T00:00:00-04:00", CultureInfo.InvariantCulture), today, now, zone) && HistoryFiltering.Matches(DateTimeOffset.Parse("2026-10-15T23:59:59-04:00", CultureInfo.InvariantCulture), today, now, zone), "Today includes its complete local calendar day");
        Check(!HistoryFiltering.Matches(At(-1), today, now, zone) && !HistoryFiltering.Matches(At(1), today, now, zone), "Today excludes neighboring calendar dates");
        Check(HistoryFiltering.Matches(At(-6), seven, now, zone) && !HistoryFiltering.Matches(At(-7), seven, now, zone), "Last seven days include today plus six prior local dates");
        Check(HistoryFiltering.Matches(At(-29), thirty, now, zone) && !HistoryFiltering.Matches(At(-30), thirty, now, zone), "Last thirty days include today plus twenty-nine prior local dates");
        Check(!HistoryFiltering.Matches(At(1), seven, now, zone) && !HistoryFiltering.Matches(At(1), thirty, now, zone), "Bounded recent ranges exclude future calendar dates");
        Check(HistoryFiltering.Matches(At(-7), new(HistoryRange.SpecificDay, new DateOnly(2026, 10, 8)), now, zone), "A specific day selects exactly its local date");
        Check(!HistoryFiltering.Matches(now, new(HistoryRange.SpecificDay), now, zone), "A missing specific day cannot silently become an all-history selection");
        HistoryItem Entry(int days, string text, bool saved = false) => new(Guid.NewGuid(), At(days), text, text, Saved: saved);
        var recent = Entry(0, "needle recent"); var yesterday = Entry(-1, "yesterday"); var saved = Entry(-60, "needle saved", true);
        var entries = new List<HistoryItem> { recent, yesterday, saved };
        var visible = HistoryFiltering.Visible(entries, today, "", now, zone);
        Check(visible.Count == 2 && visible[0] == saved && visible[1] == recent, "Saved drafts remain in their own leading group outside the recent date range");
        Check(HistoryFiltering.Visible(entries, today, "saved", now, zone).SequenceEqual([saved]), "Search applies to saved and recent visible rows");
        Check(HistoryFiltering.ClearableIds(entries, new(HistoryRange.All), "", now, zone).ToHashSet().SetEquals([recent.Id, yesterday.Id]), "Destructive candidates always exclude explicitly saved drafts");
        Check(HistoryFiltering.ClearableIds(entries, new(HistoryRange.All), "yesterday", now, zone).SequenceEqual([yesterday.Id]), "Destructive preview respects the visible search as well as range");
        Check(HistoryFiltering.GroupLabel(saved, zone) == "Saved drafts", "Saved records do not enter a calendar-day group");
        var sameDay = recent with { CreatedAt = DateTimeOffset.Parse("2026-10-16T02:00:00Z", CultureInfo.InvariantCulture) };
        Check(HistoryFiltering.GroupLabel(recent, zone, CultureInfo.InvariantCulture) == HistoryFiltering.GroupLabel(sameDay, zone, CultureInfo.InvariantCulture), "UTC-midnight neighbors group together on the same local calendar day");
        var afterDst = DateTimeOffset.Parse("2026-11-04T12:00:00-05:00", CultureInfo.InvariantCulture);
        Check(HistoryFiltering.Matches(DateTimeOffset.Parse("2026-10-29T00:01:00-04:00", CultureInfo.InvariantCulture), seven, afterDst, zone) && !HistoryFiltering.Matches(DateTimeOffset.Parse("2026-10-28T23:59:00-04:00", CultureInfo.InvariantCulture), seven, afterDst, zone), "The seven-day range stays on calendar boundaries across the daylight-saving change");
        Check(HistoryFiltering.Matches(At(-10000), new(HistoryRange.All), now, zone) && HistoryFiltering.Matches(At(10000), new(HistoryRange.All), now, zone), "All recent has no hidden date cutoff");
        var planned = HistoryFiltering.ClearableIds(entries, today, "", now, zone); var newer = Entry(0, "New arrival"); entries.Add(newer);
        Check(planned.SequenceEqual([recent.Id]) && !planned.Contains(newer.Id), "A confirmation snapshot never expands to records arriving later");
        return passed;
    }
}
