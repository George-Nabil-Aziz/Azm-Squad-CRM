using FluentValidation;

namespace Crm.Application.Customers.Attachments;

/// <summary>Upload rules, every error on the field "file": a file is sent, not empty, at most 10 MB, an allowed type.</summary>
public sealed class UploadAttachmentRequestValidator : AbstractValidator<UploadAttachmentRequest>
{
    private const string FileField = "File";

    public UploadAttachmentRequestValidator()
    {
        RuleFor(x => x.Content).NotNull().WithMessage(_ => CustomerText.AttachmentRequired).OverridePropertyName(FileField);

        When(x => x.Content is not null, () =>
        {
            RuleFor(x => x.Length).GreaterThan(0).WithMessage(_ => CustomerText.AttachmentEmpty).OverridePropertyName(FileField);
            RuleFor(x => x.Length).LessThanOrEqualTo(AttachmentRules.MaxSizeBytes)
                .WithMessage(_ => CustomerText.AttachmentTooLarge).OverridePropertyName(FileField);
            RuleFor(x => x.FileName).Must(name => AttachmentRules.TryGetContentType(name, out _))
                .WithMessage(_ => CustomerText.AttachmentTypeNotAllowed).OverridePropertyName(FileField);
        });
    }
}
