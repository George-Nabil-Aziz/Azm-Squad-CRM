using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.KnowledgeBase;

public class KbFaqsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    internal sealed record FaqBody(
        Guid Id, string? QuestionEn, string? AnswerEn, string? QuestionAr, string? AnswerAr, string Question, string Answer,
        int DisplayOrder, bool IsPublished);

    internal sealed record PortalFaqBody(Guid Id, string Question, string Answer);

    private static async Task<FaqBody> CreateAsync(
        HttpClient editor, string question, int? displayOrder = null, bool isPublished = true, string? questionAr = null)
    {
        var response = await editor.PostAsJsonAsync("/api/kb/faqs", new
        {
            questionEn = question,
            answerEn = $"Answer to {question}",
            questionAr,
            answerAr = questionAr is null ? null : "جواب",
            displayOrder,
            isPublished,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FaqBody>())!;
    }

    [Fact]
    public async Task AdminCreatesEditsAndDeletesAFaq()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var name = $"How {Guid.NewGuid():N}";

        var created = await CreateAsync(admin, name, 7, isPublished: false);
        var updated = await (await admin.PutAsJsonAsync($"/api/kb/faqs/{created.Id}", new
        {
            questionEn = $"{name} edited",
            answerEn = "New answer",
            displayOrder = 8,
            isPublished = true,
        })).Content.ReadFromJsonAsync<FaqBody>();
        var deleted = await admin.DeleteAsync($"/api/kb/faqs/{created.Id}");
        var list = await admin.GetFromJsonAsync<FaqBody[]>("/api/kb/faqs");

        Assert.False(created.IsPublished);
        Assert.Equal(7, created.DisplayOrder);
        Assert.Equal($"{name} edited", updated!.QuestionEn);
        Assert.Equal(8, updated.DisplayOrder);
        Assert.True(updated.IsPublished);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.DoesNotContain(list!, f => f.Id == created.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/kb/faqs/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task TheDisplayOrder_ControlsTheOrderOfTheList()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var tag = Guid.NewGuid().ToString("N");
        var c = await CreateAsync(admin, $"{tag} c", 30003);
        var a = await CreateAsync(admin, $"{tag} a", 30001);
        var b = await CreateAsync(admin, $"{tag} b", 30002);

        var list = await admin.GetFromJsonAsync<FaqBody[]>("/api/kb/faqs");

        Assert.Equal([a.Id, b.Id, c.Id], list!.Where(f => f.Question.StartsWith(tag, StringComparison.Ordinal)).Select(f => f.Id));
    }

    [Fact]
    public async Task AFaqCanHaveArabicAndEnglishVersions()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var created = await CreateAsync(admin, $"Pay {Guid.NewGuid():N}", questionAr: "كيف أدفع؟");

        Assert.Equal("كيف أدفع؟", created.QuestionAr);
        Assert.Equal("جواب", created.AnswerAr);
    }

    [Fact]
    public async Task MissingQuestion_Returns400_WithTheQuestionField()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var response = await admin.PostAsJsonAsync("/api/kb/faqs", new { answerEn = "No question" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("question", out _));
    }

    [Fact]
    public async Task Agents_ReadOnlyPublishedFaqs_AndCannotWrite()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var live = await CreateAsync(admin, $"Live {Guid.NewGuid():N}", isPublished: true);
        var hidden = await CreateAsync(admin, $"Hidden {Guid.NewGuid():N}", isPublished: false);

        var list = await agent.GetFromJsonAsync<FaqBody[]>("/api/kb/faqs");
        var create = await agent.PostAsJsonAsync("/api/kb/faqs", new { questionEn = "q", answerEn = "a" });
        var delete = await agent.DeleteAsync($"/api/kb/faqs/{live.Id}");

        Assert.Contains(list!, f => f.Id == live.Id);
        Assert.DoesNotContain(list!, f => f.Id == hidden.Id);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Anonymous_StaffList_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/kb/faqs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ThePortalReceivesOnlyPublishedFaqs_InOrder_WithoutSigningIn()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var tag = Guid.NewGuid().ToString("N");
        var second = await CreateAsync(admin, $"{tag} second", 40002);
        var first = await CreateAsync(admin, $"{tag} first", 40001);
        var hidden = await CreateAsync(admin, $"{tag} hidden", 40000, isPublished: false);

        var list = await factory.CreateClient().GetFromJsonAsync<PortalFaqBody[]>("/api/portal/kb/faqs");

        var mine = list!.Where(f => f.Question.StartsWith(tag, StringComparison.Ordinal)).ToList();
        Assert.Equal([first.Id, second.Id], mine.Select(f => f.Id));
        Assert.DoesNotContain(list!, f => f.Id == hidden.Id);
        Assert.Equal($"Answer to {tag} first", mine[0].Answer);
    }

    [Fact]
    public async Task ThePortalAnswersInTheRequestLanguage()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var created = await CreateAsync(admin, $"Pay {Guid.NewGuid():N}", questionAr: "كيف أدفع؟");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/portal/kb/faqs");
        request.Headers.AcceptLanguage.ParseAdd("ar");

        var arabic = await (await factory.CreateClient().SendAsync(request)).Content.ReadFromJsonAsync<PortalFaqBody[]>();
        var english = await factory.CreateClient().GetFromJsonAsync<PortalFaqBody[]>("/api/portal/kb/faqs");

        Assert.Equal("كيف أدفع؟", arabic!.Single(f => f.Id == created.Id).Question);
        Assert.Equal(created.QuestionEn, english!.Single(f => f.Id == created.Id).Question);
    }
}
