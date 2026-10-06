using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-11: a file attached to a customer.</summary>
public class CustomerAttachmentTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsTheFields_AndAStorageKeyWithoutTheFileName()
    {
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var attachment = CustomerAttachment.Create(customerId, "report.pdf", "application/pdf", 1234, userId, Now);

        Assert.Equal(
            (customerId, "report.pdf", "application/pdf", 1234L, (Guid?)userId, Now),
            (attachment.CustomerId, attachment.FileName, attachment.ContentType, attachment.Size, attachment.UploadedById,
                attachment.UploadedAt));
        Assert.Equal($"customers/{customerId:N}/{attachment.Id:N}", attachment.StorageKey);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("report.pdf", 0)]
    [InlineData("report.pdf", -1)]
    public void Create_WithoutNameOrContent_Throws(string fileName, long size) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            CustomerAttachment.Create(Guid.NewGuid(), fileName, "application/pdf", size, null, Now));
}
