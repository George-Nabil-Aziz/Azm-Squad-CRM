using Crm.Application.Channels;
using Crm.Application.Channels.WhatsApp;
using Crm.Application.Customers;
using Crm.Application.Customers.Attachments;
using Crm.Application.Customers.Notes;
using Crm.Application.Customers.Timeline;
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
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ITicketMessageService, TicketMessageService>();
        services.TryAddScoped<ITicketReplyDispatcher, NoopTicketReplyDispatcher>(); // channel stories register theirs first
        return services;
    }
}
