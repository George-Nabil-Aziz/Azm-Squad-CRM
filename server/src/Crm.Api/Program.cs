using Crm.Api.Auth;
using Crm.Api.Endpoints;
using Crm.Api.ErrorHandling;
using Crm.Application;
using Crm.Infrastructure;
using Crm.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCrmErrorHandling();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddCrmAuthentication(builder.Configuration);

var app = builder.Build();

app.UseCrmErrorHandling();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<CrmDbInitializer>().InitializeAsync(CancellationToken.None);
}

app.Run();

// Exposes Program to WebApplicationFactory<Program> in Crm.Api.IntegrationTests.
public partial class Program;
