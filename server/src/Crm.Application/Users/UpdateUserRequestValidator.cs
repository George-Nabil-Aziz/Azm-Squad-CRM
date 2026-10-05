using FluentValidation;

namespace Crm.Application.Users;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.Roles).ValidRoles();
    }
}
