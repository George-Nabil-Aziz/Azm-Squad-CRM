using Crm.Application.Common.Exceptions;
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;
using Crm.UnitTests.Localization;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.KnowledgeBase;

public class KbFaqTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_KeepsTheTextOrderAndPublishedFlag()
    {
        var faq = KbFaq.Create(" How do I pay? ", " Online. ", "كيف أدفع؟", "عبر الإنترنت.", 3, true, Now);

        Assert.Equal("How do I pay?", faq.QuestionEn);
        Assert.Equal("Online.", faq.AnswerEn);
        Assert.Equal("كيف أدفع؟", faq.QuestionAr);
        Assert.Equal(3, faq.DisplayOrder);
        Assert.True(faq.IsPublished);
    }

    [Fact]
    public void Create_AcceptsOneLanguageOnly() =>
        Assert.Null(KbFaq.Create(null, null, "سؤال", "جواب", 0, false, Now).QuestionEn);

    [Theory]
    [InlineData("q", null)]
    [InlineData(null, "a")]
    [InlineData(" ", "a")]
    public void Create_WithAHalfVersion_Throws(string? question, string? answer) =>
        Assert.Throws<ArgumentException>(() => KbFaq.Create(question, answer, null, null, 0, false, Now));

    [Fact]
    public void Create_WithoutAnyQuestion_Throws() =>
        Assert.Throws<ArgumentException>(() => KbFaq.Create(null, null, null, null, 0, false, Now));

    [Fact]
    public void Create_WithANegativeOrder_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => KbFaq.Create("q", "a", null, null, -1, false, Now));

    [Fact]
    public void Update_ReplacesEverything()
    {
        var faq = KbFaq.Create("q", "a", null, null, 0, false, Now);

        faq.Update("q2", "a2", "س", "ج", 5, true, Now.AddHours(1));

        Assert.Equal("q2", faq.QuestionEn);
        Assert.Equal("ج", faq.AnswerAr);
        Assert.Equal(5, faq.DisplayOrder);
        Assert.True(faq.IsPublished);
        Assert.Equal(Now.AddHours(1), faq.UpdatedAt);
    }

    [Fact]
    public void Delete_IsASoftDelete_AndADeletedFaqCannotChange()
    {
        var faq = KbFaq.Create("q", "a", null, null, 0, false, Now);

        faq.Delete(Now.AddMinutes(1));

        Assert.True(faq.IsDeleted);
        Assert.Throws<InvalidOperationException>(() => faq.Update("q", "a", null, null, 0, false, Now));
    }
}

public class KbFaqServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeKbFaqRepository _faqs = new();

    private KbFaqService Service(KbUser user) =>
        new(_faqs, user, new TestClock(Start), new KbFaqRequestValidator());

    private static KbFaqRequest Request(string? question = "How do I pay?", int? order = null, bool? published = true) =>
        new(question, question is null ? null : "Online.", null, null, order, published);

    [Fact]
    public async Task Create_WithoutAnOrder_PutsTheFaqLast()
    {
        var service = Service(KbUser.Editor);
        await service.CreateAsync(Request("First", order: 4), CancellationToken.None);

        var second = await service.CreateAsync(Request("Second"), CancellationToken.None);

        Assert.Equal(5, second.DisplayOrder);
    }

    [Fact]
    public async Task Create_WithoutAQuestion_ThrowsValidation_OnQuestion()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service(KbUser.Editor).CreateAsync(Request(null), CancellationToken.None));

        Assert.Contains("question", error.Errors.Keys);
    }

    [Fact]
    public async Task Create_WithANegativeOrder_ThrowsValidation_OnDisplayOrder()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service(KbUser.Editor).CreateAsync(Request(order: -1), CancellationToken.None));

        Assert.Contains("displayOrder", error.Errors.Keys);
    }

    [Fact]
    public async Task List_IsOrderedByDisplayOrder()
    {
        var editor = Service(KbUser.Editor);
        await editor.CreateAsync(Request("B", order: 2), CancellationToken.None);
        await editor.CreateAsync(Request("A", order: 1), CancellationToken.None);
        await editor.CreateAsync(Request("C", order: 3), CancellationToken.None);

        var list = await editor.ListAsync(CancellationToken.None);

        Assert.Equal(["A", "B", "C"], list.Select(f => f.QuestionEn));
    }

    [Fact]
    public async Task List_HidesUnpublishedFaqs_FromUsersWithoutManage()
    {
        var editor = Service(KbUser.Editor);
        await editor.CreateAsync(Request("Live", published: true), CancellationToken.None);
        await editor.CreateAsync(Request("Hidden", published: false), CancellationToken.None);

        var asAgent = await Service(KbUser.Agent).ListAsync(CancellationToken.None);
        var asEditor = await editor.ListAsync(CancellationToken.None);

        Assert.Equal(["Live"], asAgent.Select(f => f.QuestionEn));
        Assert.Equal(2, asEditor.Count);
    }

    [Fact]
    public async Task ListPublished_ReturnsOnlyPublished_InTheRequestLanguage_WithFallback()
    {
        var editor = Service(KbUser.Editor);
        await editor.CreateAsync(new KbFaqRequest("English only", "Answer", null, null, 1, true), CancellationToken.None);
        await editor.CreateAsync(new KbFaqRequest("Both", "Both answer", "سؤال", "جواب", 2, true), CancellationToken.None);
        await editor.CreateAsync(new KbFaqRequest("Draft", "Draft answer", null, null, 3, false), CancellationToken.None);

        var arabic = await UiCulture.Use("ar", () => editor.ListPublishedAsync(CancellationToken.None));
        var english = await UiCulture.Use("en", () => editor.ListPublishedAsync(CancellationToken.None));

        Assert.Equal(["English only", "سؤال"], arabic.Select(f => f.Question));
        Assert.Equal(["English only", "Both"], english.Select(f => f.Question));
        Assert.Equal("جواب", arabic[1].Answer);
    }

    [Fact]
    public async Task Update_AndDelete_Work_AndUnknownIdsAreNotFound()
    {
        var editor = Service(KbUser.Editor);
        var created = await editor.CreateAsync(Request(), CancellationToken.None);

        var updated = await editor.UpdateAsync(created.Id, Request("Changed", order: 9, published: false), CancellationToken.None);
        await editor.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Equal("Changed", updated.QuestionEn);
        Assert.Equal(9, updated.DisplayOrder);
        Assert.False(updated.IsPublished);
        Assert.Empty(await editor.ListAsync(CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => editor.UpdateAsync(Guid.NewGuid(), Request(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => editor.DeleteAsync(created.Id, CancellationToken.None));
    }
}
