using System.Globalization;
using System.Text.Json;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Shared.Helpers;

/// <summary>
/// Parsed shape of the trudesk overview stats response
/// (<c>GET /tickets/stats/:timespan</c>). All counters are nullable —
/// the server reads them from a warm-up cache, so individual fields can
/// be missing right after a backend restart.
/// </summary>
public sealed class OverviewStats
{
    public int? TicketCount { get; init; }
    public int? ClosedCount { get; init; }
    public double? AvgResponseHours { get; init; }
    public string? MostAssignee { get; init; }
    public string? MostRequester { get; init; }
    public IReadOnlyList<StatsGraphPoint> GraphData { get; init; } = [];
}

/// <summary>One day on the ticket-volume graph.</summary>
public readonly record struct StatsGraphPoint(DateOnly Date, int Count);

/// <summary>
/// Parsed shape of the per-assignee workload response
/// (<c>GET /tickets/stats/assignee/:user</c>).
/// </summary>
public sealed record AssigneeWorkload(int TicketCount, int ClosedCount, double AvgResponseHours)
{
    public int OpenCount => Math.Max(0, TicketCount - ClosedCount);
}

/// <summary>One slice of the status distribution donut.</summary>
public sealed record StatusSlice(string Name, int Count, string Color);

/// <summary>One assignee's row in the workload breakdown.</summary>
public sealed record WorkloadEntry(string Name, AssigneeWorkload Stats);

/// <summary>
/// Pure parsing/aggregation helpers for the statistics page. No I/O,
/// no Blazor dependencies — fully unit-testable.
/// </summary>
public static class StatsParser
{
    private const string FallbackColor = "#9E9E9E";

    /// <summary>
    /// Parses the overview stats JSON. Handles both the v2 shape
    /// (<c>{ success, graphData, ticketCount, ... }</c>) and the v1 shape
    /// (<c>{ data, ticketCount, ... }</c> — graph points under "data").
    /// Returns an empty <see cref="OverviewStats"/> for malformed input.
    /// </summary>
    public static OverviewStats ParseOverview(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new OverviewStats();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new OverviewStats();

            return new OverviewStats
            {
                TicketCount = ReadInt(root, "ticketCount"),
                ClosedCount = ReadInt(root, "closedCount"),
                AvgResponseHours = ReadDouble(root, "ticketAvg"),
                MostAssignee = ReadStatName(root, "mostAssignee"),
                MostRequester = ReadStatName(root, "mostRequester"),
                GraphData = ReadGraphData(root)
            };
        }
        catch (JsonException)
        {
            return new OverviewStats();
        }
    }

    /// <summary>
    /// Parses the per-assignee workload JSON. Counters are read from the
    /// root object and, as a fallback, from a <c>data</c> wrapper.
    /// Returns zeroed counts for malformed input.
    /// </summary>
    public static AssigneeWorkload ParseAssigneeStats(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AssigneeWorkload(0, 0, 0);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new AssigneeWorkload(0, 0, 0);

            // v2 sendApiSuccess merges the payload into the root object,
            // but stay tolerant of a { data: {...} } wrapper as well.
            var source = root;
            if (!root.TryGetProperty("ticketCount", out _) &&
                root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
            {
                source = dataEl;
            }

            return new AssigneeWorkload(
                ReadInt(source, "ticketCount") ?? 0,
                ReadInt(source, "closedCount") ?? 0,
                ReadDouble(source, "avgResponse") ?? 0);
        }
        catch (JsonException)
        {
            return new AssigneeWorkload(0, 0, 0);
        }
    }

    /// <summary>
    /// Parses the group-scoped workload response
    /// (<c>GET /tickets/stats/workload</c> → <c>{ workload: [{ name, ticketCount, closedCount, avgResponse }] }</c>).
    /// Returns an empty list for malformed input. Rows are returned in
    /// the order the server sent them (already sorted by ticket count).
    /// </summary>
    public static List<WorkloadEntry> ParseWorkload(string? json)
    {
        var result = new List<WorkloadEntry>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return result;
            if (!root.TryGetProperty("workload", out var arr) || arr.ValueKind != JsonValueKind.Array) return result;

            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var name = item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                var stats = new AssigneeWorkload(
                    ReadInt(item, "ticketCount") ?? 0,
                    ReadInt(item, "closedCount") ?? 0,
                    ReadDouble(item, "avgResponse") ?? 0);
                result.Add(new WorkloadEntry(string.IsNullOrWhiteSpace(name) ? "?" : name, stats));
            }
        }
        catch (JsonException)
        {
            // Malformed payload → empty list (caller shows "no data").
        }

        return result;
    }

    /// <summary>
    /// Aggregates daily graph points into chart buckets: weekly for
    /// timespans up to ~90 days, monthly beyond that. Returns parallel
    /// label/value arrays ready for a bar chart. Empty input → empty arrays.
    /// </summary>
    public static (string[] Labels, double[] Values) BuildVolumeSeries(IReadOnlyList<StatsGraphPoint> points, int timespan)
    {
        if (points.Count == 0) return ([], []);

        var ordered = points.OrderBy(p => p.Date).ToList();

        if (timespan > 90)
        {
            var monthly = ordered
                .GroupBy(p => new { p.Date.Year, p.Date.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToList();
            return (
                monthly.Select(g => $"{g.Key.Month:00}.{g.Key.Year % 100:00}").ToArray(),
                monthly.Select(g => (double)g.Sum(p => p.Count)).ToArray());
        }

        var first = ordered[0].Date;
        var weekly = ordered
            .GroupBy(p => (p.Date.DayNumber - first.DayNumber) / 7)
            .OrderBy(g => g.Key)
            .ToList();
        return (
            weekly.Select(g => first.AddDays(g.Key * 7).ToString("dd.MM.", CultureInfo.InvariantCulture)).ToArray(),
            weekly.Select(g => (double)g.Sum(p => p.Count)).ToArray());
    }

    /// <summary>
    /// Groups tickets by status name (descending by count). Colors come
    /// from the trudesk status <c>HtmlColor</c>; tickets without a status
    /// land in the <paramref name="unknownLabel"/> bucket.
    /// </summary>
    public static List<StatusSlice> ComputeStatusDistribution(IEnumerable<Ticket> tickets, string unknownLabel)
    {
        return tickets
            .Where(t => !t.Deleted)
            .GroupBy(t => t.Status?.Name ?? unknownLabel)
            .Select(g => new StatusSlice(
                g.Key,
                g.Count(),
                g.Select(t => t.Status?.HtmlColor).FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? FallbackColor))
            .OrderByDescending(s => s.Count)
            .ToList();
    }

    /// <summary>Counts non-deleted tickets whose status is not resolved.</summary>
    public static int CountOpen(IEnumerable<Ticket> tickets) =>
        tickets.Count(t => !t.Deleted && t.Status?.IsResolved != true);

    /// <summary>Counts non-deleted tickets created on/after <paramref name="since"/>.</summary>
    public static int CountCreatedSince(IEnumerable<Ticket> tickets, DateTime since) =>
        tickets.Count(t => !t.Deleted && t.Date >= since);

    /// <summary>Counts non-deleted tickets closed on/after <paramref name="since"/>.</summary>
    public static int CountClosedSince(IEnumerable<Ticket> tickets, DateTime since) =>
        tickets.Count(t => !t.Deleted && t.ClosedDate.HasValue && t.ClosedDate.Value >= since);

    private static int? ReadInt(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.TryGetInt32(out var i) ? i : (int)el.GetDouble();
        return null;
    }

    private static double? ReadDouble(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDouble();
        return null;
    }

    private static string? ReadStatName(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object) return null;
        if (el.TryGetProperty("fullname", out var fn) && fn.ValueKind == JsonValueKind.String) return fn.GetString();
        if (el.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) return n.GetString();
        return null;
    }

    private static List<StatsGraphPoint> ReadGraphData(JsonElement root)
    {
        var result = new List<StatsGraphPoint>();

        // v2 names the array "graphData"; v1 puts it under "data".
        JsonElement arr = default;
        var found = root.TryGetProperty("graphData", out arr) && arr.ValueKind == JsonValueKind.Array;
        if (!found)
            found = root.TryGetProperty("data", out arr) && arr.ValueKind == JsonValueKind.Array;
        if (!found) return result;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("date", out var dateEl) || dateEl.ValueKind != JsonValueKind.String) continue;
            if (!DateOnly.TryParseExact(dateEl.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;

            var count = 0;
            if (item.TryGetProperty("value", out var valEl) && valEl.ValueKind == JsonValueKind.Number)
                count = valEl.TryGetInt32(out var i) ? i : (int)valEl.GetDouble();

            result.Add(new StatsGraphPoint(date, count));
        }

        return result;
    }
}
