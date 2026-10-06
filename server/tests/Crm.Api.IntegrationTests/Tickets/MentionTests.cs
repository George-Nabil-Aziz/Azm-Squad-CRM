using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Notifications;
using Crm.Application.Auth;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>CRM-33: @mentions in internal notes against the real API and database.</summary>
public class MentionTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<(Guid Id, HttpClient Client)> AgentAsync(bool active = true)
    {
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));
        if (!active)
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(id.ToString()))!;
            user.IsActive = false;
            await users.UpdateAsync(user);
        }

        return (id, client);
    }

    private static Task<NotificationsApiTests.PageBody?> NotificationsAsync(HttpClient client) =>
        client.GetFromJsonAsync<NotificationsApiTests.PageBody>("/api/notifications");

    [Fact]
    public async Task AMentionInAnInternalNote_NotifiesTheActiveUser_AndOpensTheTicket_ButNotAnInactiveOne()
    {
        var author = await AgentAsync();
        var colleague = await AgentAsync();
        var inactive = await AgentAsync(active: false);
        var ticket = await TicketArrange.TicketAsync(author.Client, await TicketArrange.CustomerAsync(author.Client));

        var response = await author.Client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages",
            new { body = "@Omar can you check the invoice?", @internal = true, mentionedUserIds = new[] { colleague.Id, inactive.Id, author.Id } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var mention = Assert.Single((await NotificationsAsync(colleague.Client))!.Items); // AC 1
        Assert.Equal(("mention", (Guid?)ticket.Id, ticket.Number), (mention.Type, mention.TicketId, mention.TicketNumber)); // AC 2
        Assert.Equal("@Omar can you check the invoice?", mention.Text);
        Assert.Empty((await NotificationsAsync(author.Client))!.Items);
    }

    [Fact]
    public async Task AMentionInAPublicReply_NotifiesNobody()
    {
        var author = await AgentAsync();
        var colleague = await AgentAsync();
        var ticket = await TicketArrange.TicketAsync(author.Client, await TicketArrange.CustomerAsync(author.Client));

        var response = await author.Client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages",
            new { body = "Hello @Omar", @internal = false, mentionedUserIds = new[] { colleague.Id } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty((await NotificationsAsync(colleague.Client))!.Items); // AC 4
    }
}
