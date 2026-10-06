using Crm.Infrastructure.Persistence;

namespace Crm.Api.Auth;

/// <summary>
/// Fills the request's data scope (CRM-61) once the user is authenticated, so the EF Core query filters know which tickets the
/// signed-in staff user may see. Anonymous requests and the portal keep the unrestricted default.
/// </summary>
public sealed class DataScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IDataScopeLoader loader)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await loader.LoadAsync(context.RequestAborted);
        }

        await next(context);
    }
}
