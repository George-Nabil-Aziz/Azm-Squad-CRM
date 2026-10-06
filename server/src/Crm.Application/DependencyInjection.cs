using Crm.Application.Customers;
using Crm.Application.Customers.Attachments;
using Crm.Application.Customers.Notes;
using Crm.Application.Customers.Timeline;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

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
        return services;
    }
}
