namespace GitCosmos.Domain.Common;

public record PaginationRequest
{
    public const int DefaultPage = 0;
    public const int DefaultPageSize = 10;
    public const int MinPage = 0;
    public const int MinPageSize = 1;
    public const int MaxPageSize = 100;

    public PaginationRequest()
    {
    }

    public PaginationRequest(int? page = null, int? pageSize = null)
    {
        Page = page;
        PageSize = pageSize;
    }

    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public int GetPage() => Page ?? DefaultPage;
    public int GetPageSize() => PageSize ?? DefaultPageSize;

    public PaginationRequest Normalize() => new(GetPage(), GetPageSize());
}
