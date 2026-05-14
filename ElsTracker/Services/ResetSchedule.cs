namespace ElsTracker.Services;

public static class ResetSchedule
{
    // Most recent Wednesday 00:00 UTC at or before nowUtc.
    public static DateTime LastBoundaryUtc(DateTime nowUtc)
    {
        var date = nowUtc.Date;
        int delta = ((int)date.DayOfWeek - (int)DayOfWeek.Wednesday + 7) % 7;
        var thisWeeksWed = date.AddDays(-delta);
        var boundary = DateTime.SpecifyKind(thisWeeksWed, DateTimeKind.Utc);
        if (boundary > nowUtc) boundary = boundary.AddDays(-7);
        return boundary;
    }

    public static DateTime NextBoundaryUtc(DateTime nowUtc) =>
        LastBoundaryUtc(nowUtc).AddDays(7);
}
