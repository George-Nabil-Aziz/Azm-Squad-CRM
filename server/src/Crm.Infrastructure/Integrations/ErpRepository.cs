using Crm.Application.Common.Paging;
using Crm.Application.Integrations;
using Crm.Domain.Customers;
using Crm.Domain.Integrations;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Integrations;

/// <summary>EF Core storage of the ERP link and the sync log (CRM-60).</summary>
public sealed class ErpRepository(CrmDbContext db) : IErpRepository
{
    public Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public Task<bool> ErpIdTakenAsync(string erpCustomerId, Guid exceptCustomerId, CancellationToken cancellationToken) =>
        db.Customers.IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])
            .AnyAsync(c => c.Id != exceptCustomerId && c.ErpCustomerId == erpCustomerId, cancellationToken);

    public void AddLog(ErpSyncLog log) => db.ErpSyncLogs.Add(log);

    public async Task<PagedResult<ErpSyncLogResponse>> ListLogsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = from log in db.ErpSyncLogs.AsNoTracking()
                   join customer in db.Customers.IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter]) on log.CustomerId equals customer.Id into customers
                   from customer in customers.DefaultIfEmpty()
                   select new { Log = log, CustomerName = customer != null ? customer.Name : null };
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(r => r.Log.CreatedAt).ThenByDescending(r => r.Log.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<ErpSyncLogResponse>(
            [.. items.Select(r => new ErpSyncLogResponse(
                r.Log.Id, r.Log.CustomerId, r.CustomerName, r.Log.ErpCustomerId, ResultName(r.Log.Result), r.Log.Error, r.Log.CreatedAt))],
            page, pageSize, total);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static string ResultName(ErpSyncResult result) => result switch
    {
        ErpSyncResult.Success => "success",
        ErpSyncResult.Failed => "failed",
        _ => "not_configured",
    };
}
