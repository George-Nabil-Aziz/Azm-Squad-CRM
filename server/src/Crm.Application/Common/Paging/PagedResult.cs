namespace Crm.Application.Common.Paging;

/// <summary>One page of a list (CLAUDE.md "Pagination"). <c>TotalCount</c> counts every match, not only this page.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>Query-string defaults and limits shared by every paged list endpoint.</summary>
public static class PagingDefaults
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
