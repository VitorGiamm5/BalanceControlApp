namespace BalanceControl.Domain.DateTimes;

public static class DateTimeProvider
{
    public static DateTime UtcNowAsUnspecified()
    {
        return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }
}
