using Crm.Api.Endpoints;
using Crm.Api.ErrorHandling;
using Crm.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCrmErrorHandling();
builder.Services.AddApplication();

var app = builder.Build();

app.UseCrmErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();

app.Run();

// Exposes Program to WebApplicationFactory<Program> in Crm.Api.IntegrationTests.
public partial class Program;
