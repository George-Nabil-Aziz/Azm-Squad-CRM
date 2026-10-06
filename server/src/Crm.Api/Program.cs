using Crm.Api.Auth;
using Crm.Api.Channels;
using Crm.Api.Endpoints;
using Crm.Api.ErrorHandling;
using Crm.Api.Localization;
using Crm.Api.Notifications;
using Crm.Application;
using Crm.Application.Auth;
using Crm.Application.Notifications;
using Microsoft.AspNetCore.SignalR;
using Crm.Infrastructure;
using Crm.Infrastructure.Jobs;
using Crm.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCrmLocalization();
builder.Services.AddCrmErrorHandling();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
// Recurring jobs (Hangfire) never run in the Testing host; tests call the job classes directly.
var jobsEnabled = !builder.Environment.IsEnvironment("Testing") && builder.Services.AddCrmJobs(builder.Configuration);
builder.Services.AddCrmAuthentication(builder.Configuration);
builder.Services.AddHostedService<ChannelWorker>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();
builder.Services.AddSingleton<INotificationPublisher, SignalRNotificationPublisher>(); // replaces the no-op default

var app = builder.Build();

// Localization first: the error handler and status-code pages write ProblemDetails in the request language.
app.UseCrmLocalization();
app.UseCrmErrorHandling();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapCustomersEndpoints();
app.MapChannelsEndpoints();
app.MapWhatsAppWebhookEndpoints();
app.MapTicketCategoriesEndpoints();
app.MapSlaPoliciesEndpoints();
app.MapTicketsEndpoints();
app.MapTicketSlaEndpoints();
app.MapTicketMessagesEndpoints();
app.MapTicketAssignmentEndpoints();
app.MapTicketStatusEndpoints();
app.MapTicketHistoryEndpoints();
app.MapSettingsEndpoints();
app.MapNotificationsEndpoints();
app.MapTasksEndpoints();
app.MapHub<NotificationsHub>(NotificationsHub.Path).RequireAuthorization(Permissions.NotificationsView);

if (jobsEnabled)
{
    app.Services.RegisterCrmRecurringJobs();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<CrmDbInitializer>().InitializeAsync(CancellationToken.None);
}

app.Run();

// Exposes Program to WebApplicationFactory<Program> in Crm.Api.IntegrationTests.
public partial class Program;
