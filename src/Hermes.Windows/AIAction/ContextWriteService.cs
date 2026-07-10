using System.IO;
using System.Text;

namespace Hermes.Windows.AIAction;

public sealed class ContextWriteService
{
    public async Task WriteAsync(
        string fileName,
        string content,
        AIActionSaveMode saveMode,
        CancellationToken cancellationToken = default)
    {
        if (saveMode == AIActionSaveMode.None || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var path = ResolveWritePath(fileName);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (saveMode == AIActionSaveMode.Append)
        {
            var prefix = File.Exists(path) && new FileInfo(path).Length > 0
                ? $"{Environment.NewLine}{Environment.NewLine}"
                : string.Empty;
            await File.AppendAllTextAsync(path, $"{prefix}{content}", Encoding.UTF8, cancellationToken);
            return;
        }

        await File.WriteAllTextAsync(path, content, Encoding.UTF8, cancellationToken);
    }

    private static string ResolveWritePath(string fileName)
    {
        if (Path.IsPathRooted(fileName))
        {
            return Path.GetFullPath(fileName);
        }

        AIActionPaths.EnsureCreated();
        return Path.Combine(AIActionPaths.ContextDirectory, Path.GetFileName(fileName));
    }
}
