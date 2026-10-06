using System.Net;
using System.Net.Http.Json;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>JSON shape of /api/ticket-categories responses, as the client sees them.</summary>
public sealed record TicketCategoryBody(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>JSON shape of a ticket (/api/tickets).</summary>
public sealed record TicketBody(
    Guid Id,
    string Number,
    string Subject,
    string? Description,
    string Status,
    string Priority,
    string Channel,
    Guid CustomerId,
    string CustomerName,
    Guid? CategoryId,
    string? CategoryName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Arranges customers, categories and tickets through the real API.</summary>
public static class TicketArrange
{
    public static async Task<Guid> CustomerAsync(HttpClient client, string name = "Nour Trading")
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdBody>())!.Id;
    }

    /// <summary>Creates a category (the client needs categories.manage) with a unique name.</summary>
    public static async Task<TicketCategoryBody> CategoryAsync(HttpClient admin, string prefix = "Billing")
    {
        var response = await admin.PostAsJsonAsync("/api/ticket-categories", new { name = $"{prefix} {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketCategoryBody>())!;
    }

    public static async Task<TicketBody> TicketAsync(
        HttpClient client, Guid customerId, string subject = "Invoice is wrong", Guid? categoryId = null, string? priority = "high")
    {
        var response = await client.PostAsJsonAsync("/api/tickets",
            new { customerId, subject, description = "Line 3 is charged twice.", categoryId, priority });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketBody>())!;
    }

    private sealed record IdBody(Guid Id);
}
