using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Customers;

/// <summary>CRM-9: several phones, emails and WhatsApp numbers per customer, E.164, one primary per type.</summary>
public class CustomerContactsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private Task<HttpClient> AgentClientAsync() => factory.CreateClientWithRoleAsync(Roles.Agent);

    private static async Task<CustomerBody> CreateCustomerAsync(HttpClient client, string? phone = null, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name = "Nour Trading", email, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!;
    }

    private static Task<HttpResponseMessage> AddContactAsync(
        HttpClient client, Guid customerId, string type, string value, bool? isPrimary = null) =>
        client.PostAsJsonAsync($"/api/customers/{customerId}/contacts", new { type, value, isPrimary });

    private static async Task<CustomerBody> GetCustomerAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<CustomerBody>($"/api/customers/{id}"))!;

    [Fact]
    public async Task AddContact_PhoneInE164_IsSaved_Returns201()
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        var phone = TestPhones.NewMobile();

        var response = await AddContactAsync(agent, customer.Id, "phone", phone);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contact = (await response.Content.ReadFromJsonAsync<ContactBody>())!;
        Assert.Equal($"/api/customers/{customer.Id}/contacts/{contact.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(("phone", phone, true), (contact.Type, contact.Value, contact.IsPrimary));
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal(contact, Assert.Single(saved.Contacts));
        Assert.Equal(phone, saved.Phone);
    }

    [Theory]
    [InlineData("local")] // "0501234567"
    [InlineData("arabic")] // "٠٥٠١٢٣٤٥٦٧"
    [InlineData("spaced")] // "+966 50 123 4567"
    public async Task AddContact_PhoneTypedDifferently_IsSavedInE164(string format)
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        var phone = TestPhones.NewMobile();
        var typed = format switch
        {
            "local" => TestPhones.Local(phone),
            "arabic" => string.Concat(TestPhones.Local(phone).Select(digit => (char)('٠' + (digit - '0')))),
            _ => $"{phone[..4]} {phone[4..6]} {phone[6..9]} {phone[9..]}",
        };

        var response = await AddContactAsync(agent, customer.Id, "whatsapp", typed);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(phone, (await response.Content.ReadFromJsonAsync<ContactBody>())!.Value);
    }

    [Fact]
    public async Task Customer_KeepsManyPhonesEmailsAndWhatsAppNumbers_WithOnePrimaryPerType()
    {
        var agent = await AgentClientAsync();
        var (phone1, phone2) = (TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, phone1, "info@nour.example");

        Assert.Equal(HttpStatusCode.Created, (await AddContactAsync(agent, customer.Id, "phone", phone2)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AddContactAsync(agent, customer.Id, "email", "Sales@Nour.Example")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AddContactAsync(agent, customer.Id, "whatsapp", phone1)).StatusCode);

        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal(
            [("phone", phone1, true), ("phone", phone2, false), ("email", "info@nour.example", true),
             ("email", "sales@nour.example", false), ("whatsapp", phone1, true)],
            saved.Contacts.Select(c => (c.Type, c.Value, c.IsPrimary)));
        Assert.Equal(phone1, saved.Phone);
        Assert.Equal("info@nour.example", saved.Email);
    }

    [Theory]
    [InlineData("phone", "12345", "value")]
    [InlineData("phone", "call me", "value")]
    [InlineData("whatsapp", "+9665012345678", "value")]
    [InlineData("email", "not-an-email", "value")]
    [InlineData("email", "", "value")]
    [InlineData("fax", "+966501234567", "type")]
    public async Task AddContact_InvalidPhoneOrEmail_Returns400WithTheField(string type, string value, string field)
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);

        var response = await AddContactAsync(agent, customer.Id, type, value);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal([field], problem!.Errors.Keys);
        Assert.Empty((await GetCustomerAsync(agent, customer.Id)).Contacts);
    }

    [Fact]
    public async Task AddContact_InvalidPhone_InArabic_ReturnsTheArabicMessage()
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        agent.DefaultRequestHeaders.Add("Accept-Language", "ar");

        var response = await AddContactAsync(agent, customer.Id, "phone", "12345");

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["أدخل رقم هاتف صحيحاً، مثل +966501234567 أو 0501234567."], problem!.Errors["value"]);
    }

    [Fact]
    public async Task AddContact_AsPrimary_UnsetsTheOldPrimary()
    {
        var agent = await AgentClientAsync();
        var (oldPhone, newPhone) = (TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, oldPhone);

        var response = await AddContactAsync(agent, customer.Id, "phone", newPhone, isPrimary: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal([(newPhone, true), (oldPhone, false)], saved.Contacts.Select(c => (c.Value, c.IsPrimary)));
        Assert.Equal(newPhone, saved.Phone);
    }

    [Fact]
    public async Task MakeContactPrimary_UnsetsTheOldPrimary_Returns204()
    {
        var agent = await AgentClientAsync();
        var (oldPhone, newPhone) = (TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, oldPhone, "info@nour.example");
        var added = (await (await AddContactAsync(agent, customer.Id, "phone", newPhone)).Content.ReadFromJsonAsync<ContactBody>())!;
        factory.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await agent.PostAsync($"/api/customers/{customer.Id}/contacts/{added.Id}/primary", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal([newPhone], saved.Contacts.Where(c => c.Type == "phone" && c.IsPrimary).Select(c => c.Value));
        Assert.Equal(newPhone, saved.Phone);
        Assert.Equal("info@nour.example", saved.Email); // other types keep their primary
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, saved.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCustomer_WithANewPhone_ChangesThePrimaryPhone_AndKeepsTheOtherContacts()
    {
        var agent = await AgentClientAsync();
        var (phone, whatsApp, newPhone) = (TestPhones.NewMobile(), TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, phone);
        await AddContactAsync(agent, customer.Id, "whatsapp", whatsApp);

        var response = await agent.PutAsJsonAsync($"/api/customers/{customer.Id}",
            new { name = "Nour Trading", phone = TestPhones.Local(newPhone) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal(newPhone, saved.Phone);
        Assert.Equal([("phone", newPhone, true), ("whatsapp", whatsApp, true)],
            saved.Contacts.Select(c => (c.Type, c.Value, c.IsPrimary)));
    }

    [Fact]
    public async Task AddContact_TheCustomerAlreadyHas_Returns409()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, phone);

        var response = await AddContactAsync(agent, customer.Id, "phone", TestPhones.Local(phone));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("The customer already has this contact.", problem!.Detail);
    }

    [Fact]
    public async Task RemoveContact_ThePrimary_PromotesTheNextOne_Returns204()
    {
        var agent = await AgentClientAsync();
        var (first, second) = (TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, first);
        await AddContactAsync(agent, customer.Id, "phone", second);
        var primaryId = (await GetCustomerAsync(agent, customer.Id)).Contacts.Single(c => c.IsPrimary).Id;

        var response = await agent.DeleteAsync($"/api/customers/{customer.Id}/contacts/{primaryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal((second, true), (Assert.Single(saved.Contacts).Value, saved.Contacts[0].IsPrimary));
        Assert.Equal(second, saved.Phone);
    }

    [Fact]
    public async Task ContactEndpoints_UnknownCustomerOrContact_Return404()
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        var unknown = Guid.NewGuid();

        var add = await AddContactAsync(agent, unknown, "phone", TestPhones.NewMobile());
        var primary = await agent.PostAsync($"/api/customers/{customer.Id}/contacts/{unknown}/primary", null);
        var remove = await agent.DeleteAsync($"/api/customers/{customer.Id}/contacts/{unknown}");

        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, primary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        Assert.Equal("The contact was not found.", (await remove.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task AddContact_ToADeletedCustomer_Returns404()
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        await agent.DeleteAsync($"/api/customers/{customer.Id}");

        var response = await AddContactAsync(agent, customer.Id, "phone", TestPhones.NewMobile());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
