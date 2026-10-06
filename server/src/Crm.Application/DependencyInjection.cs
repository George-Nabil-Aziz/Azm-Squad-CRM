using Crm.Application.Audit;
using Crm.Application.Channels;
using Crm.Application.Channels.WhatsApp;
using Crm.Application.Customers;
using Crm.Application.Customers.Attachments;
using Crm.Application.Customers.Notes;
using Crm.Application.Customers.Timeline;
using Crm.Application.Reports;
using Crm.Application.Settings;
using Crm.Application.Sla;
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
        services.AddScoped<ISlaPolicyService, SlaPolicyService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<SlaMonitorJob>();
        services.AddScoped<ITicketMessageService, TicketMessageService>();
        services.AddScoped<ITicketHistoryRecorder, TicketHistoryRecorder>();
        services.AddScoped<ITicketAssignmentService, TicketAssignmentService>();
        services.AddScoped<ITicketStatusService, TicketStatusService>();
        services.AddScoped<ITicketHistoryService, TicketHistoryService>();
        services.AddScoped<ITicketCategoryChangeService, TicketCategoryChangeService>();
        services.TryAddScoped<ITicketReplyDispatcher, NoopTicketReplyDispatcher>(); // channel stories register theirs first
        return services;
    }
}
