using Crm.Domain.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class KbCategoryConfiguration : IEntityTypeConfiguration<KbCategory>
{
    public void Configure(EntityTypeBuilder<KbCategory> category)
    {
        category.ToTable("KbCategories");
        category.HasKey(c => c.Id);
        category.Property(c => c.Id).ValueGeneratedNever(); // set by KbCategory.Create
        category.Property(c => c.NameEn).HasMaxLength(KbCategory.NameMaxLength);
        category.Property(c => c.NameAr).HasMaxLength(KbCategory.NameMaxLength);
        category.HasQueryFilter(CrmDbContext.SoftDeleteFilter, c => !c.IsDeleted);
    }
}

public sealed class KbFaqConfiguration : IEntityTypeConfiguration<KbFaq>
{
    public void Configure(EntityTypeBuilder<KbFaq> faq)
    {
        faq.ToTable("KbFaqs");
        faq.HasKey(f => f.Id);
        faq.Property(f => f.Id).ValueGeneratedNever(); // set by KbFaq.Create
        faq.Property(f => f.QuestionEn).HasMaxLength(KbFaq.QuestionMaxLength);
        faq.Property(f => f.QuestionAr).HasMaxLength(KbFaq.QuestionMaxLength);
        faq.Property(f => f.AnswerEn).HasMaxLength(KbFaq.AnswerMaxLength);
        faq.Property(f => f.AnswerAr).HasMaxLength(KbFaq.AnswerMaxLength);
        faq.Property(f => f.SearchText).IsRequired(); // normalized text of both versions (CRM-38)
        faq.HasIndex(f => new { f.IsPublished, f.DisplayOrder }); // the portal list
        faq.HasQueryFilter(CrmDbContext.SoftDeleteFilter, f => !f.IsDeleted);
    }
}

public sealed class KbArticleConfiguration : IEntityTypeConfiguration<KbArticle>
{
    private const int EnumMaxLength = 16;

    public void Configure(EntityTypeBuilder<KbArticle> article)
    {
        article.ToTable("KbArticles");
        article.HasKey(a => a.Id);
        article.Property(a => a.Id).ValueGeneratedNever(); // set by KbArticle.Create
        article.Ignore(a => a.IsPublished);
        article.Property(a => a.TitleEn).HasMaxLength(KbArticle.TitleMaxLength);
        article.Property(a => a.TitleAr).HasMaxLength(KbArticle.TitleMaxLength);
        article.Property(a => a.BodyEn).HasMaxLength(KbArticle.BodyMaxLength);
        article.Property(a => a.BodyAr).HasMaxLength(KbArticle.BodyMaxLength);
        article.Property(a => a.SearchText).IsRequired(); // normalized text of both versions (CRM-38)
        article.Property(a => a.Status).HasConversion<string>().HasMaxLength(EnumMaxLength);
        article.HasIndex(a => new { a.Status, a.CreatedAt });
        article.HasIndex(a => a.CategoryId);

        // Categories are soft-deleted, never removed: articles keep a valid reference.
        article.HasOne<KbCategory>().WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);
        article.HasQueryFilter(CrmDbContext.SoftDeleteFilter, a => !a.IsDeleted);
    }
}
