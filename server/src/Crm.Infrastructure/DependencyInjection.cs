using Crm.Application.Audit;
using Crm.Application.Auth;
using Crm.Application.Common.Files;
using Crm.Application.Customers;
using Crm.Application.Customers.Attachments;
using Crm.Application.Customers.Notes;
using Crm.Application.Customers.Timeline;
using Crm.Application.Reports;
using Crm.Application.Settings;
using Crm.Application.Notifications;
using Crm.Application.QuickReplies;
using Crm.Application.Sla;
using Crm.Application.Tasks;
using Crm.Application.Tickets;
using Crm.Application.Users;
using Crm.Infrastructure.Ai;
using Crm.Infrastructure.Audit;
using Crm.Infrastructure.Channels;
using Crm.Infrastructure.Chat;
using Crm.Infrastructure.Customers;
using Crm.Infrastructure.Files;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.KnowledgeBase;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Portal;
using Crm.Infrastructure.Notifications;
using Crm.Infrastructure.Reports;
using Crm.Infrastructure.Settings;
using Crm.Infrastructure.QuickReplies;
using Crm.Infrastructure.Sla;
using Crm.Infrastructure.Tasks;
using Crm.Infrastructure.Tickets;
using Crm.Infrastructure.WebForms;
using Microsoft.AspNetCore.DataProtection;
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
        services.AddKnowledgeBaseStorage();
        services.AddPortalInfrastructure();
        services.AddWebFormsInfrastructure();
        services.AddChatInfrastructure();
        services.AddAiInfrastructure();
        services.AddScoped<ITicketCategoryRepository, TicketCategoryRepository>();
        services.AddScoped<ISlaPolicyRepository, SlaPolicyRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IReportsRepository, ReportsRepository>();
        // CRM-44 (CSAT ratings) is built on another branch: wire to CRM-44 on merge by replacing this with an EF read model.
        services.AddScoped<ICsatReadModel, CsatReadModel>(); // CRM-44: the ratings of the portal surveys
        services.AddDataProtection().SetApplicationName("CustomerSupportCrm");
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<IChannelSettingsApplier, ChannelSettingsApplier>();
        services.AddScoped<ITicketMessageRepository, TicketMessageRepository>();
        services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
        services.AddScoped<ITicketHistoryRepository, TicketHistoryRepository>();
        services.AddScoped<ITicketSlaRepository, TicketSlaRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IMyTicketsRepository, MyTicketsRepository>();
        services.AddScoped<ICustomerContextRepository, CustomerContextRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IQuickReplyRepository, QuickReplyRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IStaffDirectory, StaffDirectory>();
        services.AddScoped<INotificationEmailSender, NotificationEmailSender>();

        // Uploaded files: a local folder (FileStorage:RootPath, default under the user's local app data). Read lazily
        // from the final configuration, so the test host can point it at a temp folder.
        services.AddSingleton<IFileStorage>(provider =>
            new LocalFileStorage(provider.GetRequiredService<IConfiguration>()["FileStorage:RootPath"]));
        services.AddScoped<CrmDbInitializer>();
        services.AddChannels();
        return services;
    }
}
