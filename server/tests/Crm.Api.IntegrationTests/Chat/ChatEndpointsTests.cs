using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Chat;

/// <summary>CRM-56: the REST side of the live chat (widget calls, offline form, agent lists).</summary>
public class ChatEndpointsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private static object Visitor(string? name = "Visitor") => new { name, email = $"{Guid.NewGuid():N}@visitor.example", message = "Hello" };

    [Fact]
    public async Task Start_WithoutAnAgentOnline_Returns409()
    {
        var host = ChatTestSupport.FreshHost(factory);

        var response = await host.CreateClient().PostAsJsonAsync("/api/public/chat/sessions", Visitor());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Availability_FollowsTheAgentsConnection()
    {
        var host = ChatTestSupport.FreshHost(factory);
        var client = host.CreateClient();
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/public/chat/availability")).GetProperty("available").GetBoolean());

        await using var agent = await ChatTestSupport.ConnectAgentAsync(factory, host);

        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/public/chat/availability")).GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task Start_WithMissingFields_Returns400()
    {
        var host = ChatTestSupport.FreshHost(factory);
        await using var agent = await ChatTestSupport.ConnectAgentAsync(factory, host);

        var response = await host.CreateClient().PostAsJsonAsync("/api/public/chat/sessions", Visitor(name: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"name\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheOfflineForm_CreatesAChatTicket()
    {
        var host = ChatTestSupport.FreshHost(factory);
        var subject = $"Offline {Guid.NewGuid():N}";

        var response = await host.CreateClient().PostAsJsonAsync(
            "/api/public/chat/offline",
            new { name = "Offline Visitor", email = $"{Guid.NewGuid():N}@visitor.example", subject, message = "Nobody was online", captchaToken = "x" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = host.Services.CreateScope();
        var ticket = await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets.AsNoTracking().SingleAsync(t => t.Subject == subject);
        Assert.Equal(Crm.Domain.Tickets.TicketChannel.Chat, ticket.Channel);
    }

    [Fact]
    public async Task StaffEndpoints_NeedChatHandle()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/chat-sessions")).StatusCode);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var response = await agent.GetAsync("/api/chat-sessions?status=waiting");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheAgentCanReadTheMessagesOfAChat()
    {
        var host = ChatTestSupport.FreshHost(factory);
        await using var agent = await ChatTestSupport.ConnectAgentAsync(factory, host);
        var chat = await ChatTestSupport.StartChatAsync(host, "History Visitor", "First words");
        var staff = host.CreateClient();
        var email = $"hist-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        staff.DefaultRequestHeaders.Authorization = new("Bearer", await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));

        var messages = await staff.GetFromJsonAsync<JsonElement>($"/api/chat-sessions/{chat.Id}/messages");

        Assert.Equal("First words", messages[0].GetProperty("body").GetString());
    }
}
