using Crm.Domain.Ai;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Ai;

public class TicketAiClassificationTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TicketId = Guid.NewGuid();
    private static readonly Guid Billing = Guid.NewGuid();
    private static readonly Guid Support = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();

    private static TicketAiClassification Create(bool categoryApplied = true, bool priorityApplied = true) =>
        TicketAiClassification.Create(TicketId, Billing, TicketPriority.High, 0.92, categoryApplied, priorityApplied, T0);

    [Fact]
    public void Create_KeepsTheSuggestionAndConfidence()
    {
        var classification = Create();

        Assert.Equal((TicketId, Billing, TicketPriority.High, 0.92), (classification.TicketId, classification.SuggestedCategoryId, classification.SuggestedPriority, classification.Confidence));
        Assert.Equal(T0, classification.CreatedAt);
        Assert.True(classification.IsApplied);
        Assert.Null(classification.CategoryOverriddenAt);
        Assert.Null(classification.PriorityOverriddenAt);
    }

    [Fact]
    public void ASuggestionThatWasNotApplied_IsNotApplied()
    {
        Assert.False(Create(categoryApplied: false, priorityApplied: false).IsApplied);
        Assert.True(Create(categoryApplied: false, priorityApplied: true).IsApplied);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void AConfidenceOutsideZeroToOne_IsRejected(double confidence) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TicketAiClassification.Create(TicketId, null, TicketPriority.Mid, confidence, false, false, T0));

    [Fact]
    public void ChangingTheCategoryAwayFromTheSuggestion_IsAnOverride()
    {
        var classification = Create();

        var recorded = classification.RecordCategoryChange(Support, AgentId, T0.AddMinutes(5));

        Assert.True(recorded);
        Assert.Equal((T0.AddMinutes(5), Support, AgentId), (classification.CategoryOverriddenAt, classification.CategoryOverriddenTo, classification.OverriddenById));
    }

    [Fact]
    public void RemovingTheCategory_IsAnOverrideToNothing()
    {
        var classification = Create();

        Assert.True(classification.RecordCategoryChange(null, AgentId, T0.AddMinutes(5)));

        Assert.NotNull(classification.CategoryOverriddenAt);
        Assert.Null(classification.CategoryOverriddenTo);
    }

    [Fact]
    public void ChangingBackToTheSuggestion_ClearsTheOverride()
    {
        var classification = Create();
        classification.RecordCategoryChange(Support, AgentId, T0.AddMinutes(5));

        var recorded = classification.RecordCategoryChange(Billing, AgentId, T0.AddMinutes(9));

        Assert.False(recorded);
        Assert.Null(classification.CategoryOverriddenAt);
        Assert.Null(classification.CategoryOverriddenTo);
    }

    [Fact]
    public void ThePriority_IsTrackedTheSameWay()
    {
        var classification = Create();

        Assert.True(classification.RecordPriorityChange(TicketPriority.Low, AgentId, T0.AddMinutes(5)));
        Assert.Equal((TicketPriority.Low, T0.AddMinutes(5)), (classification.PriorityOverriddenTo, classification.PriorityOverriddenAt));
        Assert.False(classification.RecordPriorityChange(TicketPriority.High, AgentId, T0.AddMinutes(6)));
        Assert.Null(classification.PriorityOverriddenAt);
    }

    [Fact]
    public void ACategoryOverride_DoesNotTouchThePriority()
    {
        var classification = Create();

        classification.RecordCategoryChange(Support, AgentId, T0.AddMinutes(5));

        Assert.Null(classification.PriorityOverriddenAt);
    }

    [Fact]
    public void ANonUtcTime_IsRejected() =>
        Assert.Throws<ArgumentException>(() => Create().RecordCategoryChange(Support, AgentId, DateTime.SpecifyKind(T0, DateTimeKind.Unspecified)));
}
