using Crm.Domain.Sla;
using FluentValidation;

namespace Crm.Application.Sla;

/// <summary>Both times required, 1..<see cref="SlaPolicy.MaxMinutes"/> minutes, resolution &gt;= response (CRM-19 AC 4).</summary>
public sealed class UpdateSlaPolicyRequestValidator : AbstractValidator<UpdateSlaPolicyRequest>
{
    public UpdateSlaPolicyRequestValidator()
    {
        RuleFor(x => x.ResponseMinutes)
            .NotNull().GreaterThan(0).LessThanOrEqualTo(SlaPolicy.MaxMinutes)
            .WithName(_ => SlaText.ResponseField);

        RuleFor(x => x.ResolutionMinutes)
            .NotNull().GreaterThan(0).LessThanOrEqualTo(SlaPolicy.MaxMinutes)
            .WithName(_ => SlaText.ResolutionField);

        RuleFor(x => x.ResolutionMinutes)
            .GreaterThanOrEqualTo(x => x.ResponseMinutes)
            .WithMessage(_ => SlaText.ResolutionBelowResponse)
            .When(x => x.ResponseMinutes > 0 && x.ResolutionMinutes > 0);
    }
}
