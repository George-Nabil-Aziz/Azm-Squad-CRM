namespace Crm.Application.Common.Files;

/// <summary>
/// Where uploaded files are kept (Crm.Infrastructure: a local folder; later possibly cloud storage). Keys are built by
/// the application from ids ("customers/{customerId:N}/{attachmentId:N}"), never from user input.
/// </summary>
public interface IFileStorage
{
    /// <summary>Stores the content under the key (a key is written once).</summary>
    Task SaveAsync(string key, Stream content, CancellationToken cancellationToken);

    /// <summary>The stored content, or null when nothing is stored under the key. The caller disposes the stream.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Removes the content; does nothing when nothing is stored under the key.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
