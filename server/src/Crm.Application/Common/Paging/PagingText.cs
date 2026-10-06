using Crm.Application.Common.Localization;

namespace Crm.Application.Common.Paging;

/// <summary>Field names of the paging query string (<c>page</c>, <c>pageSize</c>), in the request language.</summary>
public static class PagingText
{
    public static string PageField => LocalizedText.Get("Page", "الصفحة");

    public static string PageSizeField => LocalizedText.Get("Page size", "حجم الصفحة");
}
