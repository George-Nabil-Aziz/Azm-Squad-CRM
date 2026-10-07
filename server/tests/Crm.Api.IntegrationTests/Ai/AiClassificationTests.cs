using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Ai;

public class AiClassificationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record ClassificationBody(
        string Status, Guid? SuggestedCategoryId, string? SuggestedCategoryName, string? SuggestedPriority, double? Confidence,
        bool CategoryApplied, bool PriorityApplied, DateTime? CategoryOverriddenAt, DateTime? PriorityOverriddenAt);

    private static string Json(Guid? categoryId, string priority, string confidence) =>
        $"{{\"categoryId\": {(categoryId is null ? "null" : $"\"{categoryId}\"")}, \"priority\": \"{priority}\", \"confidence\": {confidence}}}";

    private static async Task<TicketBody> CreateAsync(HttpClient agent, Guid customerId, string? priority = null, Guid? categoryId = null)
    {
        var response = await agent.PostAsJsonAsync("/api/tickets",
            new { customerId, subject = "Invoice is wrong", description = "Line 3 is charged twice.", categoryId, priority });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketBody>())!;
    }

    private static Task<ClassificationBody?> ClassificationAsync(HttpClient client, Guid ticketId) =>
        client.GetFromJsonAsync<ClassificationBody>($"/api/tickets/{ticketId}/ai-classification");

    [Fact]
    public async Task AboveTheThreshold_TheSuggestionIsApplied_AndShownWithItsConfidence()
    {
        var ai = new AiApp(factory);
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var billing = await TicketArrange.CategoryAsync(admin);
        ai.Fake.Answer = Json(billing.Id, "high", "0.93");
        var customerId = await TicketArrange.CustomerAsync(agent);

        var ticket = await CreateAsync(agent, customerId);
        var classification = await ClassificationAsync(agent, ticket.Id);

        var view = await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}");
        Assert.Equal((billing.Id, "high"), (view!.CategoryId, view.Priority));
        Assert.Equal("applied", classification!.Status);
        Assert.Equal((billing.Id, billing.Name, "high", 0.93), (classification.SuggestedCategoryId, classification.SuggestedCategoryName, classification.SuggestedPriority, classification.Confidence));
        Assert.True(classification.CategoryApplied && classification.PriorityApplied);
    }

    [Fact]
    public async Task BelowTheThreshold_ItIsOnlyASuggestion_TheTicketKeepsItsDefaults()
    {
        var ai = new AiApp(factory);
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var billing = await TicketArrange.CategoryAsync(admin);
        ai.Fake.Answer = Json(billing.Id, "high", "0.55");
        var ticket = await CreateAsync(agent, await TicketArrange.CustomerAsync(agent));

        var classification = await ClassificationAsync(agent, ticket.Id);
        var view = await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}");

        Assert.Null(view!.CategoryId);
        Assert.Equal("mid", view.Priority);
        Assert.Equal("suggested", classification!.Status);
        Assert.Equal(0.55, classification.Confidence);
        Assert.False(classification.CategoryApplied || classification.PriorityApplied);
    }

    [Fact]
    public async Task WhatTheCreatorChose_IsKept()
    {
        var ai = new AiApp(factory);
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var billing = await TicketArrange.CategoryAsync(admin);
        var support = await TicketArrange.CategoryAsync(admin, "Support");
        ai.Fake.Answer = Json(billing.Id, "high", "0.99");

        var ticket = await CreateAsync(agent, await TicketArrange.CustomerAsync(agent), "low", support.Id);

        var view = await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}");
        Assert.Equal((support.Id, "low"), (view!.CategoryId, view.Priority));
    }

    [Fact]
    public async Task AnOverrideByAnAgent_IsRecorded()
    {
        var ai = new AiApp(factory);
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var billing = await TicketArrange.CategoryAsync(admin);
        var support = await TicketArrange.CategoryAsync(admin, "Support");
        ai.Fake.Answer = Json(billing.Id, "high", "0.93");
        var ticket = await CreateAsync(agent, await TicketArrange.CustomerAsync(agent));
        factory.Time.Advance(TimeSpan.FromMinutes(10));

        Assert.True((await agent.PutAsJsonAsync($"/api/tickets/{ticket.Id}/category", new { categoryId = support.Id })).IsSuccessStatusCode);
        Assert.True((await agent.PutAsJsonAsync($"/api/tickets/{ticket.Id}/priority", new { priority = "low" })).IsSuccessStatusCode);
        var overridden = await ClassificationAsync(agent, ticket.Id);

        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, overridden!.CategoryOverriddenAt);
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, overridden.PriorityOverriddenAt);

        Assert.True((await agent.PutAsJsonAsync($"/api/tickets/{ticket.Id}/category", new { categoryId = billing.Id })).IsSuccessStatusCode);
        Assert.Null((await ClassificationAsync(agent, ticket.Id))!.CategoryOverriddenAt);
    }

    [Theory]
    [InlineData("fail")]
    [InlineData("garbage")]
    [InlineData("nokey")]
    public async Task WhenAiIsUnavailable_TheTicketIsCreatedNormally(string mode)
    {
        var ai = new AiApp(factory, new FakeAiTextService
        {
            Fail = mode == "fail",
            IsConfigured = mode != "nokey",
            Answer = mode == "garbage" ? "I am not sure." : "unused",
        });
        var agent = await ai.StaffAsync(Roles.Agent);

        var ticket = await CreateAsync(agent, await TicketArrange.CustomerAsync(agent));

        Assert.Equal("mid", ticket.Priority);
        Assert.Equal("none", (await ClassificationAsync(agent, ticket.Id))!.Status);
    }

    [Fact]
    public async Task UnknownTicket_Is404_AndAnonymousIs401()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);

        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/tickets/{Guid.NewGuid()}/ai-classification")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ai.Anonymous().GetAsync($"/api/tickets/{Guid.NewGuid()}/ai-classification")).StatusCode);
    }
}
