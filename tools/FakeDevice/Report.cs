using Skjermvette.Core.Models;

namespace FakeDevice;

/// <summary>Prints budgets in plain language.</summary>
public static class Report
{
    public static void Budget(BudgetSnapshot snapshot, IReadOnlyList<LogicalApp> catalog)
    {
        string Name(Guid id) => catalog.FirstOrDefault(a => a.Id == id)?.Name ?? id.ToString();

        var reserved = snapshot.TotalReserved > TimeSpan.Zero
            ? $" ({Duration(snapshot.TotalReserved)} set aside for other devices)"
            : "";
        Console.WriteLine($"Total:  {Duration(snapshot.TotalUsed)} used of {Duration(snapshot.TotalLimit)} · {Left(snapshot.TotalRemaining)}{reserved}");

        foreach (var app in snapshot.Apps.OrderBy(a => Name(a.AppId)))
        {
            var remaining = snapshot.Remaining(app.AppId)!.Value;
            Console.WriteLine($"  {Name(app.AppId)}: {Duration(app.Used)} used of {Duration(app.Limit)} · {Left(remaining)}");
        }

        if (snapshot.ExemptAppIds.Count > 0)
            Console.WriteLine($"  Exempt: {string.Join(", ", snapshot.ExemptAppIds.Select(Name).Order())}");
    }

    public static void Lease(Lease lease, IReadOnlyList<LogicalApp> catalog)
    {
        var apps = lease.Apps.Count == 0
            ? ""
            : " (" + string.Join(", ", lease.Apps.Select(a => $"{catalog.FirstOrDefault(c => c.Id == a.Key)?.Name ?? a.Key.ToString()} {Duration(a.Value)}")) + ")";
        Console.WriteLine($"Offline allowance: {Duration(lease.Total)}{apps}, until {lease.ExpiresAt.ToLocalTime():HH:mm}");
    }

    public static string Duration(TimeSpan span)
    {
        var minutes = (int)Math.Round(span.TotalMinutes);
        return (minutes / 60, minutes % 60) switch
        {
            (0, var m) => $"{m} min",
            (var h, 0) => $"{h} h",
            (var h, var m) => $"{h} h {m} min",
        };
    }

    private static string Left(TimeSpan remaining) => remaining <= TimeSpan.Zero ? "BLOCKED" : $"{Duration(remaining)} left";
}
