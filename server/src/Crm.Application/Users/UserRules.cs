using Crm.Application.Auth;
using FluentValidation;

namespace Crm.Application.Users;

/// <summary>Field rules shared by the create and update validators.</summary>
internal static class UserRules
{
    public static void ValidEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(256).WithName(_ => UserText.EmailField);

    public static void ValidFullName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().MaximumLength(200).WithName(_ => UserText.FullNameField);

    public static void ValidRoles<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule) =>
        rule.Must(roles => roles is { Count: > 0 }).WithMessage(_ => UserText.RolesRequired)
            .Must(roles => roles is null || roles.All(role => Roles.All.Contains(role)))
            .WithMessage(_ => UserText.UnknownRole);
}
