using Crm.Application.Auth;
using Crm.Application.Channels;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Application.Customers;
using Crm.Domain.Channels;
using Crm.Domain.Customers;
using Crm.Domain.Portal;
using FluentValidation;

namespace Crm.Application.Portal;

/// <summary>Sign-in codes and accounts of the portal (EF Core in Crm.Infrastructure).</summary>
public interface IPortalAccountRepository
{
    /// <summary>The newest code issued for the email (tracked), or null.</summary>
    Task<PortalLoginCode?> FindLatestCodeAsync(string email, CancellationToken cancellationToken);

    /// <summary>The codes of the email that were not used up yet (tracked).</summary>
    Task<IReadOnlyList<PortalLoginCode>> ListUnusedCodesAsync(string email, CancellationToken cancellationToken);

    void AddCode(PortalLoginCode code);

    Task<PortalAccount?> FindAccountByEmailAsync(string email, CancellationToken cancellationToken);

    void AddAccount(PortalAccount account);

    /// <summary>True when the customer exists and is not deleted (checked on every portal request).</summary>
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Customer sign-in with an emailed one-time code (CRM-40). Both calls are anonymous. Failures: <c>ValidationException</c>
/// 400 (invalid email, missing code), <c>UnauthorizedException</c> 401 (wrong, expired, used or unknown code).
/// </summary>
public interface IPortalAuthService
{
    /// <summary>
    /// Mails a new 6-digit code (valid for 10 minutes; older codes stop working). Returns normally for unknown emails too,
    /// and silently sends nothing when a code was issued for the email less than a minute ago.
    /// </summary>
    Task RequestCodeAsync(RequestCodeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the code and signs the customer in. The portal account is linked to the existing customer that has this
    /// email (primary or other email contact); unknown emails create a customer.
    /// </summary>
    Task<PortalLoginResponse> VerifyAsync(VerifyCodeRequest request, CancellationToken cancellationToken);
}

public sealed class RequestCodeRequestValidator : AbstractValidator<RequestCodeRequest>
{
    public RequestCodeRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(PortalLoginCode.EmailMaxLength).WithName(_ => AuthText.EmailField);
    }
}

public sealed class VerifyCodeRequestValidator : AbstractValidator<VerifyCodeRequest>
{
    public VerifyCodeRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(PortalLoginCode.EmailMaxLength).WithName(_ => AuthText.EmailField);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16).WithName(_ => PortalText.CodeField);
    }
}

public sealed class PortalAuthService(
    IPortalAccountRepository accounts,
    ICustomerRepository customers,
    IChannelSender sender,
    IAccessTokenGenerator tokens,
    IPortalCodeGenerator codeGenerator,
    TimeProvider timeProvider,
    IValidator<RequestCodeRequest> requestValidator,
    IValidator<VerifyCodeRequest> verifyValidator) : IPortalAuthService
{
    /// <summary>At most one code per email per this time (a new request inside it sends nothing).</summary>
    public static readonly TimeSpan ResendInterval = TimeSpan.FromMinutes(1);

    public async Task RequestCodeAsync(RequestCodeRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var email = Normalize(request.Email!);
        var now = UtcNow();

        if (await accounts.FindLatestCodeAsync(email, cancellationToken) is { ConsumedAt: null } latest
            && now - latest.CreatedAt < ResendInterval && now < latest.ExpiresAt)
        {
            return; // throttled: the code that was just sent still works
        }

        foreach (var old in await accounts.ListUnusedCodesAsync(email, cancellationToken))
        {
            old.Invalidate(now);
        }

        var code = codeGenerator.NewCode();
        accounts.AddCode(PortalLoginCode.Issue(email, PortalCodeHash.Compute(email, code), now));
        await accounts.SaveChangesAsync(cancellationToken);

        await sender.SendAsync(
            new ChannelReply(ChannelKind.Email, email, PortalText.CodeEmailSubject,
                PortalText.CodeEmailBody(code, (int)PortalLoginCode.Lifetime.TotalMinutes), null, null),
            cancellationToken);
    }

    public async Task<PortalLoginResponse> VerifyAsync(VerifyCodeRequest request, CancellationToken cancellationToken)
    {
        await verifyValidator.ValidateOrThrowAsync(request, cancellationToken);
        var email = Normalize(request.Email!);
        var now = UtcNow();

        var code = await accounts.FindLatestCodeAsync(email, cancellationToken)
                   ?? throw new UnauthorizedException(PortalText.InvalidCode);
        var result = code.Verify(PortalCodeHash.Compute(email, request.Code!), now);
        await accounts.SaveChangesAsync(cancellationToken); // keeps the attempt count / the used-up mark
        if (result != PortalCodeResult.Ok)
        {
            throw new UnauthorizedException(PortalText.InvalidCode);
        }

        var (customerId, name) = await LinkCustomerAsync(email, now, cancellationToken);
        var token = tokens.Generate(new AccessTokenSubject(customerId, email, name, [PortalRoles.Customer]));
        return new PortalLoginResponse(token.Token, "Bearer", token.ExpiresAt, new PortalCustomerResponse(customerId, name, email));
    }

    /// <summary>The customer behind the account (account → customer); else the customer with this email; else a new one.</summary>
    private async Task<(Guid Id, string Name)> LinkCustomerAsync(string email, DateTime now, CancellationToken cancellationToken)
    {
        var account = await accounts.FindAccountByEmailAsync(email, cancellationToken);
        if (account is not null && await customers.FindAsync(account.CustomerId, cancellationToken) is { } linked)
        {
            account.RecordLogin(now);
            await accounts.SaveChangesAsync(cancellationToken);
            return (linked.Id, linked.Name);
        }

        var existing = (await customers.FindByContactAsync([ContactType.Email], email, cancellationToken)).FirstOrDefault();
        Guid customerId;
        string name;
        if (existing is not null)
        {
            customerId = existing.Id;
            name = existing.Name;
        }
        else
        {
            var created = Customer.Create(NameFromEmail(email), email, null, now);
            customers.Add(created);
            customerId = created.Id;
            name = created.Name;
        }

        if (account is null)
        {
            account = PortalAccount.Create(customerId, email, now);
            accounts.AddAccount(account);
        }
        else
        {
            account.LinkTo(customerId);
        }

        account.RecordLogin(now);
        await accounts.SaveChangesAsync(cancellationToken);
        return (customerId, name);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>"nour.ali@x.example" → "nour.ali" (a customer needs a name; the agent can edit it later).</summary>
    private static string NameFromEmail(string email)
    {
        var local = email[..email.IndexOf('@')];
        if (local.Length == 0)
        {
            return email[..Math.Min(email.Length, Customer.NameMaxLength)];
        }

        return local.Length <= Customer.NameMaxLength ? local : local[..Customer.NameMaxLength];
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
