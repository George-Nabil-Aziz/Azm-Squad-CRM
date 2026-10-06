using System.Text.RegularExpressions;
using Crm.Application.Common.Files;

namespace Crm.Infrastructure.Files;

/// <summary>
/// Files in a local folder: <c>FileStorage:RootPath</c>, or <c>%LOCALAPPDATA%/AzmSquadCrm/files</c> when not configured
/// (outside the repository). Keys are relative paths of lower-case letters, digits and "/"; anything else is refused,
/// so a key can never leave the root.
/// </summary>
public sealed partial class LocalFileStorage(string? rootPath) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(string.IsNullOrWhiteSpace(rootPath)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzmSquadCrm", "files")
        : rootPath);

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathOf(string key)
    {
        if (!SafeKey().IsMatch(key))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(_root, key));
        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException("Invalid storage key.", nameof(key));
    }

    [GeneratedRegex("^[a-z0-9]+(/[a-z0-9]+)*$")]
    private static partial Regex SafeKey();
}
