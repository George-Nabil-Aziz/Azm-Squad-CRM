using Crm.Api.Auth;
using Crm.Api.Channels;
using Crm.Api.Endpoints;
using Crm.Api.ErrorHandling;
using Crm.Api.Localization;
using Crm.Application;
using Crm.Application.Settings;
using Crm.Infrastructure;
using Crm.Infrastructure.Jobs;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCrmLocalization();
builder.Services.AddCrmErrorHandling();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
// Recurring jobs (Hangfire) never run in the Testing host; tests call the job classes directly.
var jobsEnabled = !builder.Environment.IsEnvironment("Testing") && builder.Services.AddCrmJobs(builder.Configuration);
builder.Services.AddCrmAuthentication(builder.Configuration);
// CRM-35: keys that encrypt the stored secrets; set DataProtection:KeysPath (a folder) to keep them across restarts.
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.AddHostedService<ChannelWorker>();

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
app.MapAuditLogsEndpoints();
app.MapSettingsEndpoints();
app.MapReportsEndpoints();
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

if (jobsEnabled)
{
    app.Services.RegisterCrmRecurringJobs();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<CrmDbInitializer>().InitializeAsync(CancellationToken.None);
    // CRM-35: channel credentials saved in the system settings override the configured ones.
    await scope.ServiceProvider.GetRequiredService<IChannelSettingsApplier>().ApplyAsync(CancellationToken.None);
}

app.Run();

// Exposes Program to WebApplicationFactory<Program> in Crm.Api.IntegrationTests.
public partial class Program;
