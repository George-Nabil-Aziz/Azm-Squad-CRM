using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Crm.Application.Customers;
using Crm.Application.Integrations;
using Crm.Application.Tickets;
using Crm.Domain.Integrations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Crm.Api.Endpoints;

/// <summary>Endpoint metadata: the API key scope a public route needs (checked by <see cref="ApiKeyMiddleware"/>).</summary>
public sealed record RequiredApiScope(string Scope);

/// <summary>
/// The public REST API (CRM-58), /api/v1/*, authenticated with the <c>X-Api-Key</c> header instead of a JWT. Rate limited per key
/// (<c>Integrations:Api:RequestsPerMinute</c>, default 60); documented by the OpenAPI document <see cref="DocumentName"/>.
/// </summary>
public static class PublicApiEndpoints
{
    public const string DocumentName = "public-v1";
    public const string KeyHeader = "X-Api-Key";
    public const string RateLimitPolicy = "api-v1";

    public static IServiceCollection AddPublicApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.ShouldInclude = description => description.RelativePath?.StartsWith("api/v1/", StringComparison.Ordinal) == true;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "CRM public API";
                document.Info.Version = "v1";
                document.Info.Description = "Tickets and customers for external systems. Send the key in the X-Api-Key header; " +
                                            "each route lists the scope it needs. Over the rate limit the API answers 429.";
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["ApiKey"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, Name = KeyHeader, In = ParameterLocation.Header },
                };
                document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("ApiKey", document)] = [] }];
                return Task.CompletedTask;
            });
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicy, context =>
            {
                var limit = context.RequestServices.GetRequiredService<IConfiguration>().GetValue("Integrations:Api:RequestsPerMinute", 60);
                var key = context.Request.Headers[KeyHeader].ToString();
                // Per key (a hash, so the key never sits in memory as a dictionary key); without a key per client address.
                var partition = key.Length > 0
                    ? "key:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
                    : "ip:" + context.Connection.RemoteIpAddress;
                return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limit),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests." },
                });
            };
        });
        return services;
    }

    public static IEndpointRouteBuilder MapPublicApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireRateLimiting(RateLimitPolicy);

        var tickets = group.MapGroup("/tickets").WithTags("Tickets");
        tickets.MapGet("", async ([AsParameters] ListTicketsQuery query, ITicketService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(query, cancellationToken)))
            .WithName("PublicListTickets").WithSummary("List tickets (scope tickets:read)")
            .WithMetadata(new RequiredApiScope(ApiKeyScopes.TicketsRead));
        tickets.MapGet("/{id:guid}", async (Guid id, ITicketService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(id, cancellationToken)))
            .WithName("PublicGetTicket").WithSummary("Get a ticket (scope tickets:read)")
            .WithMetadata(new RequiredApiScope(ApiKeyScopes.TicketsRead));
        tickets.MapPost("", async (CreateTicketRequest request, ITicketService service, CancellationToken cancellationToken) =>
            {
                var ticket = await service.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/v1/tickets/{ticket.Id}", ticket);
            })
            .WithName("PublicCreateTicket").WithSummary("Create a ticket (scope tickets:write)")
            .WithMetadata(new RequiredApiScope(ApiKeyScopes.TicketsWrite));

        var customers = group.MapGroup("/customers").WithTags("Customers");
        customers.MapGet("", async ([AsParameters] ListCustomersQuery query, ICustomerService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(query, cancellationToken)))
            .WithName("PublicListCustomers").WithSummary("List customers (scope customers:read)")
            .WithMetadata(new RequiredApiScope(ApiKeyScopes.CustomersRead));
        customers.MapGet("/{id:guid}", async (Guid id, ICustomerService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(id, cancellationToken)))
            .WithName("PublicGetCustomer").WithSummary("Get a customer (scope customers:read)")
            .WithMetadata(new RequiredApiScope(ApiKeyScopes.CustomersRead));
        customers.MapPost("", async (CustomerRequest request, ICustomerService service, CancellationToken cancellationToken) =>
            {
                var customer = await service.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/v1/customers/{customer.Id}", customer);
            })
            .WithName("PublicCreateCustomer").WithSummary("Create a customer (scope customers:write)")
            .WithMetadata(new RequiredApiScope(ApiKeyScopes.CustomersWrite));

        return app;
    }
}

/// <summary>
/// Authenticates public API calls: a route with <see cref="RequiredApiScope"/> metadata needs a valid, not revoked
/// <c>X-Api-Key</c> that has the scope (401 / 403 ProblemDetails through the global handler). Runs before the request body is read.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IApiKeyService keys)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequiredApiScope>() is { } required)
        {
            await keys.AuthenticateAsync(context.Request.Headers[PublicApiEndpoints.KeyHeader], required.Scope, context.RequestAborted);
        }

        await next(context);
    }
}
