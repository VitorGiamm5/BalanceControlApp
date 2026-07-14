namespace BalanceControl.Domain.Services.Base.Dtos;

public abstract class PagedQuery
{
    private const int MaximumPageSize = 200;
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = Math.Max(1, value);
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaximumPageSize);
    }

    public string SortBy { get; set; } = "createdDate";
    public string SortDirection { get; set; } = "desc";

    public int Skip => (Page - 1) * PageSize;
    public bool SortDescending => !string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
}
