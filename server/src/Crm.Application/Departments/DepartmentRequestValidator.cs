using Crm.Domain.Departments;
using FluentValidation;

namespace Crm.Application.Departments;

/// <summary>Create and edit rules: name required (max 100). Uniqueness is checked by the service.</summary>
public sealed class DepartmentRequestValidator : AbstractValidator<DepartmentRequest>
{
    public DepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Department.NameMaxLength).WithName(_ => DepartmentText.NameField);
    }
}
