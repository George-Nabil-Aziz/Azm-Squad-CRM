using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Localization;

public class AcceptLanguageTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string LoginPath = "/api/auth/login";
    private const string EmailRequiredEnglish = "'Email' must not be empty.";
    private const string EmailRequiredArabic = "'البريد الإلكتروني' لا يجب أن يكون فارغاً.";

    private HttpClient ClientWithLanguage(string? acceptLanguage)
    {
        var client = factory.CreateClient();
        if (acceptLanguage is not null)
        {
            client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        }

        return client;
    }

    private static async Task<HttpValidationProblemDetails> PostEmptyLoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(LoginPath, new { email = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>())!;
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("ar-SA")]
    [InlineData("ar-EG,ar;q=0.9,en;q=0.8")]
    public async Task ValidationErrors_WithArabicAcceptLanguage_AreInArabic(string acceptLanguage)
    {
        var problem = await PostEmptyLoginAsync(ClientWithLanguage(acceptLanguage));

        Assert.Contains(EmailRequiredArabic, problem.Errors["email"]);
        Assert.Equal("حدث خطأ واحد أو أكثر في التحقق من البيانات.", problem.Title);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US,en;q=0.9")]
    [InlineData("fr-FR")]
    [InlineData(null)]
    public async Task ValidationErrors_WithEnglishMissingOrUnsupportedAcceptLanguage_AreInEnglish(string? acceptLanguage)
    {
        var problem = await PostEmptyLoginAsync(ClientWithLanguage(acceptLanguage));

        Assert.Contains(EmailRequiredEnglish, problem.Errors["email"]);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
    }

    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("ar-SA", "ar")]
    [InlineData("fr-FR", "en")]
    public async Task Response_HasContentLanguageOfTheChosenLanguage(string acceptLanguage, string expected)
    {
        var response = await ClientWithLanguage(acceptLanguage).GetAsync("/api/health");

        Assert.Equal([expected], response.Content.Headers.ContentLanguage);
    }

    [Fact]
    public async Task WrongPassword_WithArabicAcceptLanguage_ReturnsArabicDetail()
    {
        var response = await ClientWithLanguage("ar").PostAsJsonAsync(LoginPath,
            new { email = CrmApiFactory.SuperAdminEmail, password = "Wrong#Password1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("البريد الإلكتروني أو كلمة المرور غير صحيحة.", problem!.Detail);
    }

    [Theory]
    [InlineData("/_test/errors/not-found", "العنصر المطلوب غير موجود.")]
    [InlineData("/api/does-not-exist", "العنصر المطلوب غير موجود.")]
    [InlineData("/api/auth/me", "يجب تسجيل الدخول.")]
    public async Task ProblemTitle_WithArabicAcceptLanguage_IsInArabic(string path, string expectedTitle)
    {
        var response = await ClientWithLanguage("ar").GetAsync(path);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(expectedTitle, problem!.Title);
    }
}
