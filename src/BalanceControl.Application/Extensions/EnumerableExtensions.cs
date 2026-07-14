namespace BalanceControl.Application.Extensions;

public static class EnumerableExtensions
{
    public static string JoinWithLastSeparator(
        this IEnumerable<string> values,
        string separator = ", ",
        string lastSeparator = " e ")
    {
        var items = values.ToList();

        return items.Count switch
        {
            0 => string.Empty,
            1 => items[0],
            2 => string.Join(lastSeparator, items),
            _ => $"{string.Join(separator, items.SkipLast(1))}{lastSeparator}{items[^1]}"
        };
    }
}
