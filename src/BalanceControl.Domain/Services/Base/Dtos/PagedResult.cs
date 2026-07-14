namespace BalanceControl.Domain.Services.Base.Dtos;

public sealed class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalItems { get; init; }
    public int TotalPages => TotalItems == 0
        ? 0
        : (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasPreviousPage => TotalItems > 0 && Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Create(
        IEnumerable<T> items,
        int page,
        int pageSize,
        long totalItems)
        => new()
        {
            Items = items.ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
}
