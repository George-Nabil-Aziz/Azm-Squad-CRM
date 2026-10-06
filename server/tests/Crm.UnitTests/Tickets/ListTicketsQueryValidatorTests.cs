using Crm.Application.Tickets;

namespace Crm.UnitTests.Tickets;

public class ListTicketsQueryValidatorTests
{
    private readonly ListTicketsQueryValidator _validator = new();

    private static ListTicketsQuery Query(
        string? status = null, string? priority = null, Guid? assigneeId = null, bool? unassigned = null,
        DateOnly? from = null, DateOnly? to = null, int? page = null, int? pageSize = null) =>
        new(status, priority, null, assigneeId, unassigned, from, to, null, page, pageSize);

    private string[] InvalidFields(ListTicketsQuery query) =>
        [.. _validator.Validate(query).Errors.Select(e => e.PropertyName).Distinct()];

    [Fact]
    public void EmptyQuery_IsValid()
    {
        Assert.Empty(InvalidFields(Query()));
    }

    [Fact]
    public void KnownStatusAndPriority_InAnyCase_AreValid()
    {
        Assert.Empty(InvalidFields(Query(status: "Open", priority: "HIGH")));
    }

    [Theory]
    [InlineData("done", null, "Status")]
    [InlineData("1", null, "Status")]
    [InlineData(null, "urgent", "Priority")]
    public void UnknownStatusOrPriority_IsInvalid(string? status, string? priority, string field)
    {
        Assert.Equal([field], InvalidFields(Query(status: status, priority: priority)));
    }

    [Fact]
    public void CreatedFrom_AfterCreatedTo_IsInvalid_OnCreatedTo()
    {
        Assert.Equal(["CreatedTo"], InvalidFields(Query(from: new DateOnly(2026, 10, 5), to: new DateOnly(2026, 10, 1))));
        Assert.Empty(InvalidFields(Query(from: new DateOnly(2026, 10, 1), to: new DateOnly(2026, 10, 1))));
    }

    [Fact]
    public void AssigneeAndUnassigned_Together_AreInvalid()
    {
        Assert.Equal(["AssigneeId"], InvalidFields(Query(assigneeId: Guid.NewGuid(), unassigned: true)));
        Assert.Empty(InvalidFields(Query(assigneeId: Guid.NewGuid(), unassigned: false)));
    }

    [Theory]
    [InlineData(0, null, "Page")]
    [InlineData(null, 0, "PageSize")]
    [InlineData(null, 101, "PageSize")]
    public void Paging_FollowsTheSharedRules(int? page, int? pageSize, string field)
    {
        Assert.Equal([field], InvalidFields(Query(page: page, pageSize: pageSize)));
    }
}
