using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>Contact rules of the Customer aggregate (CRM-9), without a database.</summary>
public class CustomerContactTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Created.AddHours(3);

    private static CustomerContact Primary(Customer customer, ContactType type) =>
        Assert.Single(customer.Contacts, c => c.Type == type && c.IsPrimary);

    [Fact]
    public void AddContact_PhoneInE164_IsSaved()
    {
        var customer = Customer.Create("Nour", null, null, Created);

        var contact = customer.AddContact(ContactType.Phone, "+966501234567", isPrimary: false, Later);

        Assert.Same(contact, Assert.Single(customer.Contacts));
        Assert.NotEqual(Guid.Empty, contact.Id);
        Assert.Equal(customer.Id, contact.CustomerId);
        Assert.Equal(ContactType.Phone, contact.Type);
        Assert.Equal("+966501234567", contact.Value);
        Assert.Equal(Later, contact.CreatedAt);
        Assert.Equal(Later, customer.UpdatedAt);
    }

    [Fact]
    public void AddContact_StoresManyPhonesEmailsAndWhatsAppNumbers()
    {
        var customer = Customer.Create("Nour", "info@nour.example", "+966501234567", Created);

        customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Later);
        customer.AddContact(ContactType.Email, "sales@nour.example", isPrimary: false, Later);
        customer.AddContact(ContactType.WhatsApp, "+966501234567", isPrimary: false, Later);

        Assert.Equal(5, customer.Contacts.Count);
        Assert.Equal(2, customer.Contacts.Count(c => c.Type == ContactType.Phone));
        Assert.Equal(2, customer.Contacts.Count(c => c.Type == ContactType.Email));
        Assert.Equal("+966501234567", Primary(customer, ContactType.WhatsApp).Value);
    }

    [Fact]
    public void AddContact_FirstOfItsType_BecomesPrimary()
    {
        var customer = Customer.Create("Nour", null, null, Created);

        var phone = customer.AddContact(ContactType.Phone, "+966501234567", isPrimary: false, Later);

        Assert.True(phone.IsPrimary);
        Assert.Equal("+966501234567", customer.Phone);
    }

    [Fact]
    public void AddContact_AsPrimary_UnsetsTheOldPrimary()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var old = Primary(customer, ContactType.Phone);

        var added = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: true, Later);

        Assert.True(added.IsPrimary);
        Assert.False(old.IsPrimary);
        Assert.Equal("+966551234567", customer.Phone);
    }

    [Fact]
    public void AddContact_NotAsPrimary_KeepsTheOldPrimary()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);

        var added = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Later);

        Assert.False(added.IsPrimary);
        Assert.Equal("+966501234567", Primary(customer, ContactType.Phone).Value);
        Assert.Equal("+966501234567", customer.Phone);
    }

    [Fact]
    public void MakeContactPrimary_UnsetsTheOldPrimary_OfThatTypeOnly()
    {
        var customer = Customer.Create("Nour", "info@nour.example", "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created);
        var whatsApp = customer.AddContact(ContactType.WhatsApp, "+966501234567", isPrimary: false, Created);

        customer.MakeContactPrimary(second.Id, Later);

        Assert.Equal(second, Primary(customer, ContactType.Phone));
        Assert.Equal("+966551234567", customer.Phone);
        Assert.True(whatsApp.IsPrimary);
        Assert.Equal("info@nour.example", customer.Email);
        Assert.Equal(Later, customer.UpdatedAt);
    }

    [Fact]
    public void EveryType_HasExactlyOnePrimary_AfterManyChanges()
    {
        var customer = Customer.Create("Nour", "info@nour.example", "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: true, Later);
        customer.AddContact(ContactType.Email, "sales@nour.example", isPrimary: true, Later);
        customer.AddContact(ContactType.WhatsApp, "+966551234567", isPrimary: false, Later);
        customer.MakeContactPrimary(second.Id, Later);
        customer.Update("Nour", "info@nour.example", "+966561234567", Later);

        foreach (var type in customer.Contacts.Select(c => c.Type).Distinct())
        {
            Assert.Single(customer.Contacts, c => c.Type == type && c.IsPrimary);
        }
    }

    [Theory]
    [InlineData("0501234567")] // not E.164: the Application layer normalizes first
    [InlineData("+966 50 123 4567")]
    [InlineData("+0501234567")]
    [InlineData("966501234567")]
    public void AddContact_PhoneNotInE164_Throws(string phone)
    {
        var customer = Customer.Create("Nour", null, null, Created);

        Assert.Throws<ArgumentException>(() => customer.AddContact(ContactType.WhatsApp, phone, isPrimary: false, Later));
        Assert.Empty(customer.Contacts);
    }

    [Fact]
    public void AddContact_Email_IsTrimmedAndLowerCased()
    {
        var customer = Customer.Create("Nour", null, null, Created);

        var email = customer.AddContact(ContactType.Email, "  Sales@Nour.Example ", isPrimary: false, Later);

        Assert.Equal("sales@nour.example", email.Value);
        Assert.Equal("sales@nour.example", customer.Email);
    }

    [Fact]
    public void AddContact_TheSameValueTwice_Throws()
    {
        var customer = Customer.Create("Nour", "info@nour.example", "+966501234567", Created);

        Assert.True(customer.HasContact(ContactType.Email, "INFO@nour.example"));
        Assert.Throws<InvalidOperationException>(() =>
            customer.AddContact(ContactType.Email, "INFO@nour.example", isPrimary: false, Later));
        Assert.Throws<InvalidOperationException>(() =>
            customer.AddContact(ContactType.Phone, "+966501234567", isPrimary: false, Later));
        Assert.Equal(2, customer.Contacts.Count);
    }

    [Fact]
    public void RemoveContact_ThePrimary_PromotesTheOldestRemainingOfThatType()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created.AddMinutes(1));
        customer.AddContact(ContactType.Phone, "+966561234567", isPrimary: false, Created.AddMinutes(2));

        customer.RemoveContact(Primary(customer, ContactType.Phone).Id, Later);

        Assert.Equal(2, customer.Contacts.Count);
        Assert.Equal(second, Primary(customer, ContactType.Phone));
        Assert.Equal("+966551234567", customer.Phone);
        Assert.Equal(Later, customer.UpdatedAt);
    }

    [Fact]
    public void RemoveContact_TheLastOfItsType_ClearsTheCustomerValue()
    {
        var customer = Customer.Create("Nour", "info@nour.example", null, Created);

        customer.RemoveContact(Primary(customer, ContactType.Email).Id, Later);

        Assert.Empty(customer.Contacts);
        Assert.Null(customer.Email);
    }

    [Fact]
    public void MakePrimaryOrRemove_OfAnUnknownContact_Throws()
    {
        var customer = Customer.Create("Nour", null, null, Created);

        Assert.Throws<ArgumentException>(() => customer.MakeContactPrimary(Guid.NewGuid(), Later));
        Assert.Throws<ArgumentException>(() => customer.RemoveContact(Guid.NewGuid(), Later));
    }

    [Fact]
    public void Create_WithEmailAndPhone_AddsThemAsPrimaryContacts()
    {
        var customer = Customer.Create("Nour", " Info@Nour.Example ", "+966501234567", Created);

        Assert.Equal("info@nour.example", Primary(customer, ContactType.Email).Value);
        Assert.Equal("+966501234567", Primary(customer, ContactType.Phone).Value);
        Assert.Equal("info@nour.example", customer.Email);
        Assert.Equal("+966501234567", customer.Phone);
    }

    [Fact]
    public void Update_WithANewPhone_ChangesThePrimaryPhone_AndKeepsTheOtherContacts()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created);
        var primaryId = Primary(customer, ContactType.Phone).Id;

        customer.Update("Nour", null, "+966561234567", Later);

        Assert.Equal(2, customer.Contacts.Count);
        Assert.Equal(primaryId, Primary(customer, ContactType.Phone).Id);
        Assert.Equal("+966561234567", customer.Phone);
        Assert.False(second.IsPrimary);
    }

    [Fact]
    public void Update_WithAnExistingSecondaryPhone_MakesItPrimary()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created);

        customer.Update("Nour", null, "+966551234567", Later);

        Assert.Equal(2, customer.Contacts.Count);
        Assert.Equal(second, Primary(customer, ContactType.Phone));
    }

    [Fact]
    public void Update_WithoutPhone_RemovesThePrimaryPhone_AndPromotesTheNext()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created);

        customer.Update("Nour", null, null, Later);

        Assert.Equal(second, Assert.Single(customer.Contacts));
        Assert.True(second.IsPrimary);
        Assert.Equal("+966551234567", customer.Phone);
    }

    [Fact]
    public void Contacts_OfADeletedCustomer_CannotBeChanged()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var phoneId = Primary(customer, ContactType.Phone).Id;
        customer.Delete(Later);

        Assert.Throws<InvalidOperationException>(() =>
            customer.AddContact(ContactType.Email, "info@nour.example", isPrimary: false, Later));
        Assert.Throws<InvalidOperationException>(() => customer.MakeContactPrimary(phoneId, Later));
        Assert.Throws<InvalidOperationException>(() => customer.RemoveContact(phoneId, Later));
    }
}
