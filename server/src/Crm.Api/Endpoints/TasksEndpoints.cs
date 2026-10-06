using Crm.Application.Auth;
using Crm.Application.Tasks;

namespace Crm.Api.Endpoints;

public static class TasksEndpoints
{
    public static IEndpointRouteBuilder MapTasksEndpoints(this IEndpointRouteBuilder app)
    {
        // Every user only reaches their own tasks (the service filters by the current user).
        var group = app.MapGroup("/api/tasks").RequireAuthorization(Permissions.TasksManage);

        group.MapGet("", async ([AsParameters] ListTasksQuery query, ITaskService tasks, CancellationToken cancellationToken) =>
                Results.Ok(await tasks.ListAsync(query, cancellationToken)))
            .WithName("ListTasks");

        group.MapPost("", async (CreateTaskRequest request, ITaskService tasks, CancellationToken cancellationToken) =>
            {
                var task = await tasks.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/tasks/{task.Id}", task);
            })
            .WithName("CreateTask");

        group.MapPost("/{id:guid}/done", async (Guid id, ITaskService tasks, CancellationToken cancellationToken) =>
                Results.Ok(await tasks.MarkDoneAsync(id, cancellationToken)))
            .WithName("MarkTaskDone");

        return app;
    }
}
