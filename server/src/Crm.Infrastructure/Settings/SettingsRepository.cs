using Crm.Application.Settings;
using Crm.Domain.Settings;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Settings;

public sealed class SettingsRepository(CrmDbContext db) : ISettingsRepository
{
    public async Task<IReadOnlyList<SystemSetting>> ListAsync(CancellationToken cancellationToken) =>
        await db.SystemSettings.ToListAsync(cancellationToken);

    public void Add(SystemSetting setting) => db.SystemSettings.Add(setting);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
