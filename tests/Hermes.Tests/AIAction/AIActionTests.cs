using System.IO;
using Hermes.Windows.AIAction;

namespace Hermes.Tests.AIAction;

public static class AIActionTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("PromptTemplateResolver removes append control variable", ResolveAppendPrompt);
        suite.Add("PromptTemplateResolver prefers overwrite over append", ResolveOverwritePrompt);
        suite.Add("DeepSeekProvider builds chat completions URI", BuildChatCompletionsUri);
        suite.Add("DeepSeekProvider extracts message content", ExtractMessageContent);
        suite.Add("AIAction defaults to deepseek v4 flash", ConfigDefaultsToDeepSeekV4Flash);
        suite.Add("AIAction window records hotkeys from buttons", WindowRecordsHotkeysFromButtons);
        suite.Add("AIAction choose file hotkey is globally wired", ChooseFileHotkeyIsGloballyWired);
        suite.Add("AIAction provider can reuse hermes api key", ProviderCanReuseHermesApiKey);
        suite.Add("AIAction input file keeps source path", InputFileKeepsSourcePath);
        suite.Add("AIAction overwrite does not prompt before writing", OverwriteDoesNotPromptBeforeWriting);
        suite.Add("AIAction saved action preserves source file path", SavedActionPreservesSourceFilePath);
        suite.Add("AIAction paths use local appdata with roaming migration", PathsUseLocalAppDataWithRoamingMigration);
    }

    private static void ResolveAppendPrompt()
    {
        var resolver = new PromptTemplateResolver();
        var result = resolver.Resolve(
            "Summarize:\n$text$\n$file_input$\n$file_append$",
            "selected",
            "clip",
            "file");

        TestAssert.Equal(AIActionSaveMode.Append, result.SaveMode);
        TestAssert.Equal("Summarize:\nselected\nfile", result.Prompt);
        TestAssert.False(result.Prompt.Contains(PromptTemplateResolver.FileAppendVariable, StringComparison.Ordinal));
    }

    private static void ResolveOverwritePrompt()
    {
        var resolver = new PromptTemplateResolver();
        var result = resolver.Resolve(
            "$file_append$\nRewrite:\n$clipboard$\n$file_overwrite$",
            "selected",
            "clip",
            "file");

        TestAssert.Equal(AIActionSaveMode.Overwrite, result.SaveMode);
        TestAssert.Equal("Rewrite:\nclip", result.Prompt);
    }

    private static void BuildChatCompletionsUri()
    {
        TestAssert.Equal(
            "https://api.deepseek.com/chat/completions",
            DeepSeekProvider.BuildChatCompletionsUri("https://api.deepseek.com").ToString());
        TestAssert.Equal(
            "https://example.test/v1/chat/completions",
            DeepSeekProvider.BuildChatCompletionsUri("https://example.test/v1/chat/completions").ToString());
    }

    private static void ConfigDefaultsToDeepSeekV4Flash()
    {
        var config = new AIActionConfiguration();

        TestAssert.Equal("deepseek-v4-flash", config.Model);
        TestAssert.Equal("Ctrl+Alt+O", config.ChooseFileHotkey);
    }

    private static void WindowRecordsHotkeysFromButtons()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/AIActionSettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/AIActionSettingsWindow.xaml.cs"));

        TestAssert.False(xaml.Contains("x:Name=\"ActionHotkeyText\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"ActionHotkeyButton\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"ChooseFileHotkeyButton\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("PreviewKeyDown=\"HotkeyRecorder_PreviewKeyDown\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("BeginHotkeyRecording(HotkeyRecordingTarget.Action)", StringComparison.Ordinal));
        TestAssert.True(code.Contains("BeginHotkeyRecording(HotkeyRecordingTarget.ChooseFile)", StringComparison.Ordinal));
        TestAssert.True(code.Contains("key is WpfInput.Key.Back or WpfInput.Key.Delete", StringComparison.Ordinal));
        TestAssert.True(code.Contains("key == WpfInput.Key.Escape", StringComparison.Ordinal));
    }

    private static void ChooseFileHotkeyIsGloballyWired()
    {
        var manager = File.ReadAllText(FindRepoFile("src/Hermes.Windows/AIAction/AIActionHotkeyManager.cs"));
        var app = File.ReadAllText(FindRepoFile("src/Hermes.Windows/App.xaml.cs"));

        TestAssert.True(manager.Contains("ChooseFileHotkeyId", StringComparison.Ordinal));
        TestAssert.True(manager.Contains("ChooseFileHotkeyPressed", StringComparison.Ordinal));
        TestAssert.True(manager.Contains("RegisterChooseFileHotkey(handle)", StringComparison.Ordinal));
        TestAssert.True(app.Contains("ShowAIActionSettingsWindow(chooseFile: true)", StringComparison.Ordinal));
        TestAssert.True(app.Contains("ChooseInputFileFromHotkeyAsync", StringComparison.Ordinal));
    }
    private static void PathsUseLocalAppDataWithRoamingMigration()
    {
        var paths = File.ReadAllText(FindRepoFile("src/Hermes.Windows/AIAction/AIActionPaths.cs"));
        var settingsXaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.True(paths.Contains("AppPaths.AppDataDirectory", StringComparison.Ordinal));
        TestAssert.True(paths.Contains("MigrateLegacyRoamingDirectory", StringComparison.Ordinal));
        TestAssert.False(paths.Contains("public static string RootDirectory { get; } = Path.Combine(\r\n        RoamingAppDataDirectory", StringComparison.Ordinal));
        TestAssert.True(settingsXaml.Contains("%LOCALAPPDATA%\\Hermes\\AIAction\\", StringComparison.Ordinal));
        TestAssert.False(settingsXaml.Contains("%APPDATA%\\Hermes\\AIAction\\", StringComparison.Ordinal));
    }
    private static void SavedActionPreservesSourceFilePath()
    {
        var configService = File.ReadAllText(FindRepoFile("src/Hermes.Windows/AIAction/AIActionConfigService.cs"));

        TestAssert.False(configService.Contains("Path.GetFileName(action.InputFile", StringComparison.Ordinal));
        TestAssert.True(configService.Contains("action.InputFile = action.InputFile?.Trim() ?? string.Empty;", StringComparison.Ordinal));
    }
    private static void OverwriteDoesNotPromptBeforeWriting()
    {
        var executor = File.ReadAllText(FindRepoFile("src/Hermes.Windows/AIAction/AIActionExecutor.cs"));

        TestAssert.False(executor.Contains("ConfirmOverwrite", StringComparison.Ordinal));
        TestAssert.False(executor.Contains("MessageBox.Show", StringComparison.Ordinal));
        TestAssert.True(executor.Contains("_contextWriteService.WriteAsync", StringComparison.Ordinal));
    }
    private static void InputFileKeepsSourcePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HermesAIActionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "context.md");
        var duplicate = Path.Combine(directory, "context-1.md");
        File.WriteAllText(source, "source");
        File.WriteAllText(duplicate, "duplicate");

        try
        {
            var service = new ContextFileService();
            var imported = service.ImportAsync(source).GetAwaiter().GetResult();
            var input = service.ReadInputAsync(imported).GetAwaiter().GetResult();

            TestAssert.Equal(Path.GetFullPath(source), imported);
            TestAssert.Equal("source", input);
            TestAssert.False(imported.EndsWith("-1.md", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
    private static void ProviderCanReuseHermesApiKey()
    {
        var provider = File.ReadAllText(FindRepoFile("src/Hermes.Windows/AIAction/DeepSeekProvider.cs"));
        var app = File.ReadAllText(FindRepoFile("src/Hermes.Windows/App.xaml.cs"));

        TestAssert.True(provider.Contains("ISecretStorageService? _fallbackSecretStorage", StringComparison.Ordinal));
        TestAssert.True(provider.Contains("ResolveApiKeyAsync", StringComparison.Ordinal));
        TestAssert.False(provider.Contains("Set the AI Action API key first.", StringComparison.Ordinal));
        TestAssert.True(app.Contains("_logger, _secretStorage", StringComparison.Ordinal));
    }
    private static void ExtractMessageContent()
    {
        const string json = """
            {
              "choices": [
                {
                  "message": {
                    "content": "done"
                  }
                }
              ]
            }
            """;

        TestAssert.Equal("done", DeepSeekProvider.ExtractMessageContent(json));
    }
    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file '{relativePath}'.");
    }
}








