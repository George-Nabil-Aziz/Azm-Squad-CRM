using Crm.Application.Audit;
using Crm.Application.Branches;
using Crm.Application.Branding;
using Crm.Application.Channels;
using Crm.Application.Channels.WhatsApp;
using Crm.Application.Customers;
using Crm.Application.Customers.Attachments;
using Crm.Application.Customers.Notes;
using Crm.Application.Customers.Timeline;
using Crm.Application.Departments;
using Crm.Application.KnowledgeBase;
using Crm.Application.Portal;
using Crm.Application.Reports;
using Crm.Application.Settings;
using Crm.Application.Notifications;
using Crm.Application.QuickReplies;
using Crm.Application.Sla;
using Crm.Application.Tasks;
using Crm.Application.Tickets;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Application;

public static class DependencyInjection
{
    /// <summary>Registers Application-layer services: every FluentValidation validator in this assembly and the feature services.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly, includeInternalTypes: true);
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ITicketReportService, TicketReportService>();
        services.AddScoped<ISlaReportService, SlaReportService>();
        services.AddScoped<ICsatReportService, CsatReportService>();
        services.AddScoped<IAgentReportService, AgentReportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<ISystemSettingsProvider, SystemSettingsProvider>();
        services.AddScoped<IInteractionRecorder, InteractionRecorder>();
        services.AddScoped<ICustomerTimelineService, CustomerTimelineService>();
        services.AddScoped<ICustomerNoteService, CustomerNoteService>();
        services.AddScoped<ICustomerAttachmentService, CustomerAttachmentService>();
        services.AddScoped<IChannelSender, ChannelSender>();
        services.AddScoped<IChannelDeliveryObserver, TicketDeliveryObserver>();
        services.AddScoped<IChannelTicketService, ChannelTicketService>();
        services.AddScoped<ITicketReplyDispatcher, ChannelTicketReplyDispatcher>();
        services.AddScoped<IInboundMessageProcessor, InboundMessageProcessor>();
        services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
        services.AddScoped<ITicketCategoryService, TicketCategoryService>();
        services.AddKnowledgeBase();
        services.AddPortal();
        services.AddScoped<ISlaPolicyService, SlaPolicyService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddScoped<ITicketDepartmentService, TicketDepartmentService>();
        services.AddScoped<SlaMonitorJob>();
        services.AddScoped<ISlaNotifier, SlaNotifier>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationService, NotificationService>();
        services.TryAddSingleton<INotificationPublisher, NoopNotificationPublisher>(); // Crm.Api registers the SignalR one after it (the last registration wins)
        services.AddScoped<ITicketMessageService, TicketMessageService>();
        services.AddScoped<ITicketHistoryRecorder, TicketHistoryRecorder>();
        services.AddScoped<ITicketAssignmentService, TicketAssignmentService>();
        services.AddScoped<IAutoAssignmentService, AutoAssignmentService>();
        services.AddScoped<IMyTicketsService, MyTicketsService>();
        services.AddScoped<ITicketCustomerContextService, TicketCustomerContextService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IQuickReplyService, QuickReplyService>();
        services.AddScoped<TaskReminderJob>();
        services.AddScoped<IAssignmentSettingsService, AssignmentSettingsService>();
        services.AddScoped<ITicketStatusService, TicketStatusService>();
        services.AddScoped<ITicketHistoryService, TicketHistoryService>();
        services.AddScoped<ITicketCategoryChangeService, TicketCategoryChangeService>();
        services.TryAddScoped<ITicketReplyDispatcher, NoopTicketReplyDispatcher>(); // channel stories register theirs first
        return services;
    }
}
