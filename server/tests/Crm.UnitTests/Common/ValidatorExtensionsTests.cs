using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.UnitTests.Common;

public class ValidatorExtensionsTests
{
    private sealed record Address(string? City);

    private sealed record Person(string? FirstName, string? Email, Address Address);

    private sealed class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            RuleFor(p => p.FirstName).NotEmpty().MaximumLength(3).Matches("^[A-Za-z]*$");
            RuleFor(p => p.Email).NotEmpty().EmailAddress();
            RuleFor(p => p.Address.City).NotEmpty();
        }
    }

    private readonly PersonValidator _validator = new();

    [Fact]
    public async Task ValidateOrThrowAsync_ValidInstance_DoesNotThrow()
    {
        var person = new Person("Ali", "ali@example.com", new Address("Riyadh"));

        await _validator.ValidateOrThrowAsync(person, CancellationToken.None);
    }

    [Fact]
    public async Task ValidateOrThrowAsync_InvalidInstance_ThrowsWithCamelCaseFieldErrors()
    {
        var person = new Person("", "not-an-email", new Address(""));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _validator.ValidateOrThrowAsync(person, CancellationToken.None));

        Assert.Equal(["address.city", "email", "firstName"], exception.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.All(exception.Errors.Values, messages => Assert.NotEmpty(messages));
    }

    [Fact]
    public async Task ValidateOrThrowAsync_SeveralFailuresOnOneField_GroupsThemUnderOneKey()
    {
        var person = new Person("1234", "ali@example.com", new Address("Riyadh"));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _validator.ValidateOrThrowAsync(person, CancellationToken.None));

        var messages = Assert.Single(exception.Errors).Value;
        Assert.Equal(2, messages.Length);
        Assert.True(exception.Errors.ContainsKey("firstName"));
    }

    [Fact]
    public void ApplicationExceptions_KeepTheirMessage()
    {
        Assert.Equal("x", new NotFoundException("x").Message);
        Assert.Equal("y", new ConflictException("y").Message);
        Assert.Equal("z", new ForbiddenException("z").Message);
    }
}
