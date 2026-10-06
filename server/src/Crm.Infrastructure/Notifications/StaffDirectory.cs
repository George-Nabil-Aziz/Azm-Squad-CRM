using Crm.Application.Notifications;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Notifications;

/// <summary>Finds the active staff users notifications go to.</summary>
public sealed class StaffDirectory(CrmDbContext db) : IStaffDirectory
{
    public async Task<IReadOnlyList<Guid>> ListActiveUserIdsInRoleAsync(string role, CancellationToken cancellationToken) =>
        await (from userRole in db.UserRoles
               join r in db.Roles on userRole.RoleId equals r.Id
               join user in db.Users on userRole.UserId equals user.Id
               where r.Name == role && user.IsActive
               select user.Id).Distinct().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StaffContact>> ListActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => u.IsActive && ids.Contains(u.Id))
            .OrderBy(u => u.Id)
            .Select(u => new StaffContact(u.Id, u.FullName, u.Email))
            .ToListAsync(cancellationToken);
}
