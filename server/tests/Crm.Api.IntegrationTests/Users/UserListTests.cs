using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Users;

public class UserListTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<HttpClient> AdminClientAsync() => factory.CreateAuthenticatedClient(await factory.LoginAsync());

    /// <summary>Creates users "&lt;tag&gt; 1", "&lt;tag&gt; 2", … with emails "&lt;tag&gt;-n@example.test" (unique per test).</summary>
    private async Task<string> CreateUsersAsync(int count)
    {
        var tag = $"t{Guid.NewGuid():N}"[..12];
        var admin = await AdminClientAsync();
        for (var n = 1; n <= count; n++)
        {
            var response = await admin.PostAsJsonAsync("/api/users", new
            {
                email = $"{tag}-{n}@example.test",
                fullName = $"{tag} {n}",
                password = "List#Pass123",
                roles = new[] { "Agent" },
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        return tag;
    }

    [Fact]
    public async Task List_WithoutParameters_ReturnsFirstPageOf20WithTotalCount()
    {
        var admin = await AdminClientAsync();

        var page = await admin.GetFromJsonAsync<UserPageBody>("/api/users");

        Assert.Equal(1, page!.Page);
        Assert.Equal(20, page.PageSize);
        Assert.True(page.TotalCount >= 1);
        var superAdmin = Assert.Single(page.Items, u => u.Email == CrmApiFactory.SuperAdminEmail);
        Assert.Equal(["SuperAdmin"], superAdmin.Roles);
        Assert.True(superAdmin.IsActive);
    }

    [Fact]
    public async Task List_SearchByName_ReturnsOnlyMatchingUsers()
    {
        var tag = await CreateUsersAsync(3);
        var admin = await AdminClientAsync();

        var page = await admin.GetFromJsonAsync<UserPageBody>($"/api/users?search={tag} 2");

        var user = Assert.Single(page!.Items);
        Assert.Equal($"{tag} 2", user.FullName);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task List_SearchByEmail_IsCaseInsensitive()
    {
        var tag = await CreateUsersAsync(2);
        var admin = await AdminClientAsync();

        var page = await admin.GetFromJsonAsync<UserPageBody>($"/api/users?search={tag.ToUpperInvariant()}-1@EXAMPLE");

        Assert.Equal($"{tag}-1@example.test", Assert.Single(page!.Items).Email);
    }

    [Fact]
    public async Task List_SearchTreatsWildcardsAsText()
    {
        await CreateUsersAsync(1);
        var admin = await AdminClientAsync();

        var page = await admin.GetFromJsonAsync<UserPageBody>("/api/users?search=%25");

        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task List_Paginates_WithTotalCountOfAllMatches()
    {
        var tag = await CreateUsersAsync(3);
        var admin = await AdminClientAsync();

        var first = await admin.GetFromJsonAsync<UserPageBody>($"/api/users?search={tag}&page=1&pageSize=2");
        var second = await admin.GetFromJsonAsync<UserPageBody>($"/api/users?search={tag}&page=2&pageSize=2");

        Assert.Equal([$"{tag} 1", $"{tag} 2"], first!.Items.Select(u => u.FullName));
        Assert.Equal([$"{tag} 3"], second!.Items.Select(u => u.FullName));
        Assert.All([first, second], page => Assert.Equal(3, page.TotalCount));
        Assert.Equal(2, second.Page);
        Assert.Equal(2, second.PageSize);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    public async Task List_WithInvalidPaging_Returns400(string queryString, string field)
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync($"/api/users?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }
}
