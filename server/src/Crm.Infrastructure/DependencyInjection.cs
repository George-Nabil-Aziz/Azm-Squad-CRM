using Crm.Application.Auth;
using Crm.Application.Common.Files;
using Crm.Application.Customers;
using Crm.Application.Customers.Attachments;
using Crm.Application.Customers.Notes;
using Crm.Application.Customers.Timeline;
using Crm.Application.Sla;
using Crm.Application.Tickets;
using Crm.Application.Users;
using Crm.Infrastructure.Customers;
using Crm.Infrastructure.Files;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Sla;
using Crm.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    /// <summary>EF Core (SQL Server), ASP.NET Identity stores, the auth service, the clock and the DB initializer.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Read lazily from the final configuration, so the test host can swap the provider (SQLite).
        services.AddDbContext<CrmDbContext>((provider, options) =>
            options.UseSqlServer(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Crm")
                ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Crm' is not configured.")));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<CrmDbContext>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IActiveUserChecker, ActiveUserChecker>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerTimelineRepository, CustomerTimelineRepository>();
        services.AddScoped<ICustomerNoteRepository, CustomerNoteRepository>();
        services.AddScoped<ICustomerAttachmentRepository, CustomerAttachmentRepository>();
        services.AddScoped<ITicketCategoryRepository, TicketCategoryRepository>();
        services.AddScoped<ISlaPolicyRepository, SlaPolicyRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketMessageRepository, TicketMessageRepository>();
        services.AddScoped<ITicketSlaRepository, TicketSlaRepository>();
        services.TryAddScoped<ISlaNotifier, LoggingSlaNotifier>();

        // Uploaded files: a local folder (FileStorage:RootPath, default under the user's local app data). Read lazily
        // from the final configuration, so the test host can point it at a temp folder.
        services.AddSingleton<IFileStorage>(provider =>
            new LocalFileStorage(provider.GetRequiredService<IConfiguration>()["FileStorage:RootPath"]));
        services.AddScoped<CrmDbInitializer>();
        return services;
    }
}
