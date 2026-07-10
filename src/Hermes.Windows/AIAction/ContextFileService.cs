using System.IO;
using System.Text;

namespace Hermes.Windows.AIAction;

public sealed class ContextFileService
{
    public Task<string> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return Task.FromResult(string.Empty);
        }

        var extension = Path.GetExtension(sourcePath);
        if (!IsSupportedExtension(extension))
        {
            throw new InvalidOperationException("Only .txt and .md files are supported.");
        }

        return Task.FromResult(Path.GetFullPath(sourcePath));
    }

    public async Task<string> ReadInputAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var path = ResolveInputPath(fileName);
        if (path is null || !File.Exists(path))
        {
            return string.Empty;
        }

        return await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
    }

    public string? ResolveContextPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return Path.Combine(AIActionPaths.ContextDirectory, Path.GetFileName(fileName));
    }

    public string? ResolveInputPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return Path.IsPathRooted(fileName)
            ? Path.GetFullPath(fileName)
            : ResolveContextPath(fileName);
    }

    private static bool IsSupportedExtension(string extension)
    {
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase);
    }
}
