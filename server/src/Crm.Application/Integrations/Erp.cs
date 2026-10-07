using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Localization;
using Crm.Application.Common.Paging;
using Crm.Domain.Customers;
using Crm.Domain.Integrations;

namespace Crm.Application.Integrations;

/// <summary>Body of PUT /api/customers/{id}/erp-link: the customer's id in the ERP; empty or null removes the link.</summary>
public sealed record ErpLinkRequest(string? ErpCustomerId);

public sealed record ErpLinkResponse(Guid CustomerId, string? ErpCustomerId);

/// <summary>An order of the customer in the ERP (read only).</summary>
public sealed record ErpOrder(string Id, string? Number, DateTime? Date, string? Status, decimal? Total, string? Currency);

/// <summary>An invoice of the customer in the ERP (read only).</summary>
public sealed record ErpInvoice(string Id, string? Number, DateTime? Date, DateTime? DueDate, string? Status, decimal? Total, string? Currency);

/// <summary>The recent orders and invoices of one ERP customer.</summary>
public sealed record ErpCustomerData(IReadOnlyList<ErpOrder> Orders, IReadOnlyList<ErpInvoice> Invoices);

/// <summary>
/// What GET /api/customers/{id}/erp returns. It is always a 200: when the ERP cannot be reached (<c>Available</c> false) the page
/// still loads and shows <c>Message</c>. <c>Linked</c> false = the customer has no ERP id yet.
/// </summary>
public sealed record ErpCustomerResponse(
    bool Linked, string? ErpCustomerId, bool Available, string? Message,
    IReadOnlyList<ErpOrder> Orders, IReadOnlyList<ErpInvoice> Invoices, DateTime? FetchedAt);

/// <summary>One row of the sync log (GET /api/integrations/erp/sync-logs): <c>Result</c> is "success", "failed" or "not_configured".</summary>
public sealed record ErpSyncLogResponse(
    Guid Id, Guid CustomerId, string? CustomerName, string ErpCustomerId, string Result, string? Error, DateTime CreatedAt);

/// <summary>The ERP is not set up (no base URL): reads fail with this, never at startup.</summary>
public sealed class ErpNotConfiguredException() : Exception("The ERP connection is not configured.");

/// <summary>The ERP could not be reached or answered with an error.</summary>
public sealed class ErpUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The ERP behind one interface (the product is not decided yet; <c>HttpErpClient</c> speaks a small REST contract). Throws
/// <see cref="ErpNotConfiguredException"/> or <see cref="ErpUnavailableException"/>.
/// </summary>
public interface IErpClient
{
    Task<ErpCustomerData> GetCustomerDataAsync(string erpCustomerId, int limit, CancellationToken cancellationToken);
}

/// <summary>Storage for the ERP link and the sync log (implemented in Crm.Infrastructure with EF Core).</summary>
public interface IErpRepository
{
    /// <summary>The tracked customer (not deleted), or null.</summary>
    Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>True when another customer (also a deleted one) already has this ERP id.</summary>
    Task<bool> ErpIdTakenAsync(string erpCustomerId, Guid exceptCustomerId, CancellationToken cancellationToken);

    void AddLog(ErpSyncLog log);

    Task<PagedResult<ErpSyncLogResponse>> ListLogsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// ERP data of customers. <c>NotFoundException</c> 404 (unknown customer), <c>ConflictException</c> 409 (the ERP id belongs
/// to another customer), <c>ValidationException</c> 400 (id too long). An unreachable ERP is not an error: see
/// <see cref="ErpCustomerResponse"/>.
/// </summary>
public interface IErpService
{
    Task<ErpLinkResponse> LinkAsync(Guid customerId, ErpLinkRequest request, CancellationToken cancellationToken);

    Task<ErpCustomerResponse> GetAsync(Guid customerId, CancellationToken cancellationToken);

    Task<PagedResult<ErpSyncLogResponse>> ListLogsAsync(int? page, int? pageSize, CancellationToken cancellationToken);
}

public sealed class ErpService(IErpRepository repository, IErpClient client, TimeProvider timeProvider) : IErpService
{
    /// <summary>How many recent orders and invoices the customer panel shows.</summary>
    public const int RecentCount = 5;

    public async Task<ErpLinkResponse> LinkAsync(Guid customerId, ErpLinkRequest request, CancellationToken cancellationToken)
    {
        var id = request.ErpCustomerId?.Trim();
        if (id is { Length: > Customer.ErpCustomerIdMaxLength })
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["erpCustomerId"] = [ErpText.IdTooLong] });
        }

        var customer = await repository.FindCustomerAsync(customerId, cancellationToken) ?? throw new NotFoundException(ErpText.CustomerNotFound);
        if (!string.IsNullOrEmpty(id) && await repository.ErpIdTakenAsync(id, customerId, cancellationToken))
        {
            throw new ConflictException(ErpText.IdTaken);
        }

        customer.LinkErp(id, timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveChangesAsync(cancellationToken);
        return new ErpLinkResponse(customer.Id, customer.ErpCustomerId);
    }

    public async Task<ErpCustomerResponse> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await repository.FindCustomerAsync(customerId, cancellationToken) ?? throw new NotFoundException(ErpText.CustomerNotFound);
        if (customer.ErpCustomerId is not { } erpId)
        {
            return new ErpCustomerResponse(false, null, true, null, [], [], null);
        }

        ErpCustomerData? data = null;
        var result = ErpSyncResult.Success;
        string? error = null;
        string? message = null;
        try
        {
            data = await client.GetCustomerDataAsync(erpId, RecentCount, cancellationToken);
        }
        catch (ErpNotConfiguredException)
        {
            result = ErpSyncResult.NotConfigured;
            message = ErpText.NotConfigured;
            error = "The ERP connection is not configured.";
        }
        catch (ErpUnavailableException exception)
        {
            result = ErpSyncResult.Failed;
            message = ErpText.Unavailable;
            error = exception.Message;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        repository.AddLog(ErpSyncLog.Create(customerId, erpId, result, error, now));
        await repository.SaveChangesAsync(cancellationToken);

        return data is null
            ? new ErpCustomerResponse(true, erpId, false, message, [], [], now)
            : new ErpCustomerResponse(true, erpId, true, null,
                [.. data.Orders.OrderByDescending(o => o.Date).Take(RecentCount)],
                [.. data.Invoices.OrderByDescending(i => i.Date).Take(RecentCount)], now);
    }

    public Task<PagedResult<ErpSyncLogResponse>> ListLogsAsync(int? page, int? pageSize, CancellationToken cancellationToken) =>
        repository.ListLogsAsync(
            Math.Max(page ?? PagingDefaults.DefaultPage, 1),
            Math.Clamp(pageSize ?? PagingDefaults.DefaultPageSize, 1, PagingDefaults.MaxPageSize),
            cancellationToken);
}

/// <summary>User-facing text of the ERP integration, in the request language.</summary>
public static class ErpText
{
    public static string CustomerNotFound => LocalizedText.Get("The customer was not found.", "العميل غير موجود.");

    public static string IdTooLong => LocalizedText.Get(
        $"The ERP customer id may have at most {Customer.ErpCustomerIdMaxLength} characters.",
        $"يجب ألا يتجاوز معرّف العميل في النظام ERP {Customer.ErpCustomerIdMaxLength} حرفاً.");

    public static string IdTaken => LocalizedText.Get(
        "Another customer is already linked to this ERP customer.", "عميل آخر مرتبط بالفعل بهذا العميل في نظام ERP.");

    public static string Unavailable => LocalizedText.Get(
        "The ERP system is not reachable right now. Order and invoice data cannot be shown; try again later.",
        "نظام ERP غير متاح حالياً. لا يمكن عرض الطلبات والفواتير، حاول مرة أخرى لاحقاً.");

    public static string NotConfigured => LocalizedText.Get(
        "The ERP connection is not configured yet. Ask an administrator to set it up.",
        "لم يتم إعداد الاتصال بنظام ERP بعد. اطلب من المسؤول إعداده.");
}
