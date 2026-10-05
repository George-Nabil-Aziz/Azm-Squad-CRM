using Crm.Application.Auth;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Identity;

public sealed class ActiveUserChecker(CrmDbContext db) : IActiveUserChecker
{
    public Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
}
