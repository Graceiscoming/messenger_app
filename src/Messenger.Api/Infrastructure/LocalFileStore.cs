using Messenger.Domain.Abstractions;
using Messenger.Domain.Exceptions;
using Messenger.Domain.Options;
using Microsoft.Extensions.Options;

namespace Messenger.Api.Infrastructure;

/// <summary>Stores files flat under Storage:UploadPath. Stored names must be plain file names (no separators).</summary>
public sealed class LocalFileStore : IFileStore
{
    private readonly string _root;

    public LocalFileStore(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.UploadPath);
        Directory.CreateDirectory(_root);
    }

    private string Resolve(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName) || storedName != Path.GetFileName(storedName)
            || storedName.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Invalid stored file name.", nameof(storedName));

        var full = Path.GetFullPath(Path.Combine(_root, storedName));
        var prefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException("Invalid stored file name.", nameof(storedName));
        return full;
    }

    public async Task SaveAsync(Stream content, string storedName, CancellationToken ct = default)
    {
        var path = Resolve(storedName);
        await using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream> OpenReadAsync(string storedName, CancellationToken ct = default)
    {
        var path = Resolve(storedName);
        if (!File.Exists(path)) throw new NotFoundException("File not found.");
        Stream s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(s);
    }

    public Task DeleteAsync(string storedName, CancellationToken ct = default)
    {
        try { File.Delete(Resolve(storedName)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* best effort */ }
        return Task.CompletedTask;
    }
}
