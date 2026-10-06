using Crm.Domain.KnowledgeBase;

namespace Crm.UnitTests.KnowledgeBase;

public class KbArticleTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Fact]
    public void Create_SavesADraft_WithTrimmedText()
    {
        var article = KbArticle.Create(CategoryId, "  Reset password ", " Click the link. ", null, null, Now);

        Assert.Equal(KbArticleStatus.Draft, article.Status);
        Assert.False(article.IsPublished);
        Assert.Null(article.PublishedAt);
        Assert.Equal("Reset password", article.TitleEn);
        Assert.Equal("Click the link.", article.BodyEn);
        Assert.Null(article.TitleAr);
        Assert.Equal(0, article.LinkedCount);
    }

    [Fact]
    public void Create_AcceptsBothLanguages()
    {
        var article = KbArticle.Create(CategoryId, "Reset password", "Body", "إعادة تعيين كلمة المرور", "المحتوى", Now);

        Assert.Equal("إعادة تعيين كلمة المرور", article.TitleAr);
        Assert.Equal("المحتوى", article.BodyAr);
    }

    [Fact]
    public void Create_AcceptsAnArabicOnlyArticle()
    {
        var article = KbArticle.Create(CategoryId, null, null, "عنوان", "محتوى", Now);

        Assert.Null(article.TitleEn);
        Assert.Equal("عنوان", article.TitleAr);
    }

    [Theory]
    [InlineData(null, "body")]
    [InlineData("  ", "body")]
    [InlineData("title", null)]
    [InlineData("title", " ")]
    public void Create_WithAHalfVersion_Throws(string? title, string? body) =>
        Assert.Throws<ArgumentException>(() => KbArticle.Create(CategoryId, title, body, null, null, Now));

    [Fact]
    public void Create_WithoutAnyTitle_Throws() =>
        Assert.Throws<ArgumentException>(() => KbArticle.Create(CategoryId, null, null, null, null, Now));

    [Fact]
    public void Create_WithoutACategory_Throws() =>
        Assert.Throws<ArgumentException>(() => KbArticle.Create(Guid.Empty, "t", "b", null, null, Now));

    [Fact]
    public void Create_WithANonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() => KbArticle.Create(CategoryId, "t", "b", null, null, DateTime.Now));

    [Fact]
    public void Publish_MakesItVisible_AndRecordsTheTime()
    {
        var article = KbArticle.Create(CategoryId, "t", "b", null, null, Now);

        article.Publish(Now.AddHours(1));

        Assert.True(article.IsPublished);
        Assert.Equal(Now.AddHours(1), article.PublishedAt);
    }

    [Fact]
    public void Publish_Twice_KeepsTheFirstPublishTime()
    {
        var article = KbArticle.Create(CategoryId, "t", "b", null, null, Now);
        article.Publish(Now.AddHours(1));

        article.Publish(Now.AddHours(2));

        Assert.Equal(Now.AddHours(1), article.PublishedAt);
    }

    [Fact]
    public void Unpublish_ReturnsToDraft()
    {
        var article = KbArticle.Create(CategoryId, "t", "b", null, null, Now);
        article.Publish(Now);

        article.Unpublish(Now.AddHours(1));

        Assert.Equal(KbArticleStatus.Draft, article.Status);
    }

    [Fact]
    public void Update_ReplacesTheContent()
    {
        var article = KbArticle.Create(CategoryId, "t", "b", null, null, Now);
        var other = Guid.NewGuid();

        article.Update(other, "new", "new body", "جديد", "محتوى جديد", Now.AddHours(1));

        Assert.Equal(other, article.CategoryId);
        Assert.Equal("new", article.TitleEn);
        Assert.Equal("جديد", article.TitleAr);
        Assert.Equal(Now.AddHours(1), article.UpdatedAt);
    }

    [Fact]
    public void Delete_IsASoftDelete_AndACeasedArticleCannotChange()
    {
        var article = KbArticle.Create(CategoryId, "t", "b", null, null, Now);

        article.Delete(Now.AddHours(1));

        Assert.True(article.IsDeleted);
        Assert.Equal(Now.AddHours(1), article.DeletedAt);
        Assert.Throws<InvalidOperationException>(() => article.Publish(Now.AddHours(2)));
    }
}

public class KbCategoryTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_NeedsAName_InEnglishOrArabic()
    {
        Assert.Equal("Billing", KbCategory.Create(" Billing ", null, Now).NameEn);
        Assert.Equal("الفواتير", KbCategory.Create(null, "الفواتير", Now).NameAr);
        Assert.Throws<ArgumentException>(() => KbCategory.Create(" ", null, Now));
    }

    [Fact]
    public void Delete_IsASoftDelete()
    {
        var category = KbCategory.Create("Billing", null, Now);

        category.Delete(Now.AddMinutes(1));

        Assert.True(category.IsDeleted);
        Assert.Equal(Now.AddMinutes(1), category.DeletedAt);
    }
}
