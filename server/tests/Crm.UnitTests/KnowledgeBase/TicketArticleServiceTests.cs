using Crm.Application.Common.Exceptions;
using Crm.Application.KnowledgeBase;
using Crm.Application.Portal;
using Crm.Domain.KnowledgeBase;
using Crm.UnitTests.Localization;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.KnowledgeBase;

public class KbArticleLinkTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RecordLinked_RaisesTheCounter()
    {
        var article = KbArticle.Create(Guid.NewGuid(), "t", "b", null, null, Now);
        article.Publish(Now);

        article.RecordLinked();
        article.RecordLinked();

        Assert.Equal(2, article.LinkedCount);
    }

    [Fact]
    public void RecordLinked_OnADraft_Throws()
    {
        var article = KbArticle.Create(Guid.NewGuid(), "t", "b", null, null, Now);

        Assert.Throws<InvalidOperationException>(() => article.RecordLinked());
        Assert.Equal(0, article.LinkedCount);
    }

    [Fact]
    public void TheLink_KeepsTicketArticleAgentAndTime()
    {
        var ticket = Guid.NewGuid();
        var article = Guid.NewGuid();
        var agent = Guid.NewGuid();

        var link = TicketArticleLink.Create(ticket, article, agent, Now);

        Assert.Equal(ticket, link.TicketId);
        Assert.Equal(article, link.ArticleId);
        Assert.Equal(agent, link.LinkedById);
        Assert.Equal(Now, link.LinkedAt);
    }
}

public class TicketArticleServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeKbArticleRepository _articles = new();
    private readonly FakeTicketArticleRepository _links = new();
    private readonly Guid _ticketId = Guid.NewGuid();

    public TicketArticleServiceTests()
    {
        _links.Tickets.Add(_ticketId);
    }

    private TicketArticleService Service(string baseUrl = "https://help.example.com") => new(
        _links, _articles, KbUser.Editor, new TestClock(Start), new PortalOptions { BaseUrl = baseUrl });

    private KbArticle Article(bool publish = true, string title = "Reset your password", string body = "Use the reset link on the sign-in page.", string? titleAr = null, string? bodyAr = null)
    {
        var article = KbArticle.Create(Guid.NewGuid(), title, body, titleAr, bodyAr, Start.UtcDateTime);
        if (publish)
        {
            article.Publish(Start.UtcDateTime);
        }

        _articles.Articles.Add(article);
        return article;
    }

    [Fact]
    public async Task Linking_ReturnsTheLinkAndSummary_ToInsertIntoTheReply()
    {
        var article = Article();

        var response = await Service().LinkAsync(_ticketId, new LinkArticleRequest(article.Id, null), CancellationToken.None);

        Assert.Equal(article.Id, response.ArticleId);
        Assert.Equal("Reset your password", response.Title);
        Assert.Equal("Use the reset link on the sign-in page.", response.Summary);
        Assert.Equal($"https://help.example.com/portal/kb/articles/{article.Id}", response.Url);
        Assert.Contains("Reset your password", response.InsertText);
        Assert.Contains("Use the reset link on the sign-in page.", response.InsertText);
        Assert.Contains(response.Url, response.InsertText);
    }

    [Fact]
    public async Task Linking_RecordsTheLinkOnTheTicket_AndCountsTheUse()
    {
        var article = Article();
        var service = Service();

        await service.LinkAsync(_ticketId, new LinkArticleRequest(article.Id, null), CancellationToken.None);
        await service.LinkAsync(_ticketId, new LinkArticleRequest(article.Id, null), CancellationToken.None);

        Assert.Equal(2, article.LinkedCount);
        Assert.Equal(2, _links.Links.Count);
        Assert.All(_links.Links, link => Assert.Equal(_ticketId, link.TicketId));
        var listed = await service.ListAsync(_ticketId, CancellationToken.None);
        Assert.Equal(2, listed.Count);
        Assert.Equal("Reset your password", listed[0].Title);
    }

    [Fact]
    public async Task Linking_ADraft_ThrowsValidation_OnArticleId_AndChangesNothing()
    {
        var draft = Article(publish: false);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service().LinkAsync(_ticketId, new LinkArticleRequest(draft.Id, null), CancellationToken.None));

        Assert.Contains("articleId", error.Errors.Keys);
        Assert.Equal(0, draft.LinkedCount);
        Assert.Empty(_links.Links);
    }

    [Fact]
    public async Task Linking_WithoutAnArticleId_ThrowsValidation()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service().LinkAsync(_ticketId, new LinkArticleRequest(null, null), CancellationToken.None));

        Assert.Contains("articleId", error.Errors.Keys);
    }

    [Fact]
    public async Task Linking_AnUnknownArticleOrTicket_IsNotFound()
    {
        var article = Article();

        await Assert.ThrowsAsync<NotFoundException>(
            () => Service().LinkAsync(_ticketId, new LinkArticleRequest(Guid.NewGuid(), null), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => Service().LinkAsync(Guid.NewGuid(), new LinkArticleRequest(article.Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task TheLanguage_CanBeChosen_AndFallsBackToTheOtherVersion()
    {
        var both = Article(titleAr: "إعادة تعيين كلمة المرور", bodyAr: "استخدم رابط إعادة التعيين.");
        var englishOnly = Article(title: "Invoices", body: "Find them in billing.");
        var service = Service();

        var arabic = await service.LinkAsync(_ticketId, new LinkArticleRequest(both.Id, "ar"), CancellationToken.None);
        var english = await service.LinkAsync(_ticketId, new LinkArticleRequest(both.Id, "en"), CancellationToken.None);
        var fallback = await service.LinkAsync(_ticketId, new LinkArticleRequest(englishOnly.Id, "ar"), CancellationToken.None);
        var byCulture = await UiCulture.Use("ar", () => service.LinkAsync(_ticketId, new LinkArticleRequest(both.Id, null), CancellationToken.None));

        Assert.Equal("إعادة تعيين كلمة المرور", arabic.Title);
        Assert.Equal("Reset your password", english.Title);
        Assert.Equal("Invoices", fallback.Title);
        Assert.Equal("إعادة تعيين كلمة المرور", byCulture.Title);
    }

    [Fact]
    public async Task TheSummary_IsCutAt200Characters()
    {
        var article = Article(body: new string('x', 500));

        var response = await Service().LinkAsync(_ticketId, new LinkArticleRequest(article.Id, null), CancellationToken.None);

        Assert.Equal(200, response.Summary.Length);
    }

    [Fact]
    public async Task WithoutABaseAddress_TheLinkIsRelative()
    {
        var article = Article();

        var response = await Service(baseUrl: "").LinkAsync(_ticketId, new LinkArticleRequest(article.Id, null), CancellationToken.None);

        Assert.Equal($"/portal/kb/articles/{article.Id}", response.Url);
    }

    [Fact]
    public async Task AnInvalidLanguage_ThrowsValidation()
    {
        var article = Article();

        await Assert.ThrowsAsync<ValidationException>(
            () => Service().LinkAsync(_ticketId, new LinkArticleRequest(article.Id, "fr"), CancellationToken.None));
    }
}
