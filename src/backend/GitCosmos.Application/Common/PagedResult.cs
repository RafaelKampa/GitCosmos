namespace GitCosmos.Application.Common;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new List<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRows { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalRows / PageSize);

    public PagedResult() { }

    public PagedResult(List<T> items, int totalRows, int page, int pageSize)
    {
        Items = items;
        TotalRows = totalRows;
        Page = page;
        PageSize = pageSize;
    }
}
