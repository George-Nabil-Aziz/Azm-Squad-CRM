using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-11: a note about a customer.</summary>
public class CustomerNoteTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_TrimsTheText_AndSetsAuthorAndTime()
    {
        var customerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var note = CustomerNote.Create(customerId, "  Prefers WhatsApp.  ", authorId, Now);

        Assert.NotEqual(Guid.Empty, note.Id);
        Assert.Equal((customerId, "Prefers WhatsApp.", (Guid?)authorId, Now), (note.CustomerId, note.Text, note.AuthorId, note.CreatedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutText_Throws(string text) =>
        Assert.ThrowsAny<ArgumentException>(() => CustomerNote.Create(Guid.NewGuid(), text, null, Now));

    [Fact]
    public void Create_WithTooLongText_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            CustomerNote.Create(Guid.NewGuid(), new string('x', CustomerNote.TextMaxLength + 1), null, Now));

    [Fact]
    public void Create_WithNonUtcTime_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            CustomerNote.Create(Guid.NewGuid(), "Note", null, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
}
