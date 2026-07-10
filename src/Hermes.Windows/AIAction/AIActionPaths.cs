using System.IO;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.AIAction;

public static class AIActionPaths
{
    private const string AppDataFolderName = "Hermes";
    private const string AIActionFolderName = "AIAction";

    private static string RoamingAppDataDirectory { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string LegacyRootDirectory { get; } = Path.Combine(
        RoamingAppDataDirectory,
        AppDataFolderName,
        AIActionFolderName);

    public static string RootDirectory { get; } = Path.Combine(
        AppPaths.AppDataDirectory,
        AIActionFolderName);

    public static string ContextDirectory => Path.Combine(RootDirectory, "Context");

    public static string ActionsPath => Path.Combine(RootDirectory, "actions.json");

    public static string ConfigPath => Path.Combine(RootDirectory, "ai_config.json");

    public static string SecretPath => Path.Combine(RootDirectory, "ai_key.dat");

    public static void EnsureCreated()
    {
        AppPaths.EnsureCreated();
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ContextDirectory);
        MigrateLegacyRoamingDirectory();
    }

    private static void MigrateLegacyRoamingDirectory()
    {
        if (string.Equals(LegacyRootDirectory, RootDirectory, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(LegacyRootDirectory))
        {
            return;
        }

        CopyMissingFiles(LegacyRootDirectory, RootDirectory);
    }

    private static void CopyMissingFiles(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(file));
            if (!File.Exists(targetPath))
            {
                File.Copy(file, targetPath, overwrite: false);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyMissingFiles(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }
}
