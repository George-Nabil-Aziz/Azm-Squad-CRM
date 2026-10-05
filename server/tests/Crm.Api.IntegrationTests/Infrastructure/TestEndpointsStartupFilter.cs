using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Crm.Api.IntegrationTests.Infrastructure;

public sealed record SampleRequest(string? Name, string? Email, int Age);

public sealed class SampleRequestValidator : AbstractValidator<SampleRequest>
{
    public SampleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

/// <summary>
/// Test-only endpoints: /_test/errors/* trigger each kind of failure; /_test/permissions/&lt;permission&gt;
/// answers 200 only to users with that permission (one endpoint per permission of the catalogue).
/// </summary>
public sealed class TestEndpointsStartupFilter : IStartupFilter
{
    public const string SecretMessage = "secret-internal-detail-1234";

    public static string PermissionPath(string permission) => $"/_test/permissions/{permission}";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        // This second routing step runs after the app's own pipeline: endpoints with authorization metadata
        // need the authentication + authorization middleware between UseRouting and UseEndpoints.
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            var group = endpoints.MapGroup("/_test/errors");
            group.MapPost("/validate", async (SampleRequest request, IValidator<SampleRequest> validator,
                CancellationToken cancellationToken) =>
            {
                await validator.ValidateOrThrowAsync(request, cancellationToken);
                return Results.NoContent();
            });
            group.MapGet("/unhandled", IResult () => throw new InvalidOperationException(SecretMessage));
            group.MapGet("/not-found", IResult () => throw new NotFoundException("Customer 42 was not found."));
            group.MapGet("/conflict", IResult () => throw new ConflictException("Email already in use."));
            group.MapGet("/forbidden", IResult () => throw new ForbiddenException("Agents cannot delete customers."));

            foreach (var permission in Permissions.All)
            {
                endpoints.MapGet(PermissionPath(permission), () => Results.Ok()).RequireAuthorization(permission);
            }
        });
    };
}
