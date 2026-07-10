namespace Hermes.Windows.AIAction;

public sealed class PromptTemplateResolver
{
    public const string TextVariable = "$text$";
    public const string ClipboardVariable = "$clipboard$";
    public const string FileInputVariable = "$file_input$";
    public const string FileAppendVariable = "$file_append$";
    public const string FileOverwriteVariable = "$file_overwrite$";

    public PromptResolution Resolve(
        string template,
        string selectedText,
        string clipboardText,
        string fileInput)
    {
        var prompt = template ?? string.Empty;
        var saveMode = AIActionSaveMode.None;

        if (prompt.Contains(FileOverwriteVariable, StringComparison.Ordinal))
        {
            saveMode = AIActionSaveMode.Overwrite;
            prompt = prompt.Replace(FileOverwriteVariable, string.Empty, StringComparison.Ordinal);
        }

        if (prompt.Contains(FileAppendVariable, StringComparison.Ordinal))
        {
            if (saveMode == AIActionSaveMode.None)
            {
                saveMode = AIActionSaveMode.Append;
            }

            prompt = prompt.Replace(FileAppendVariable, string.Empty, StringComparison.Ordinal);
        }

        prompt = prompt
            .Replace(TextVariable, selectedText ?? string.Empty, StringComparison.Ordinal)
            .Replace(ClipboardVariable, clipboardText ?? string.Empty, StringComparison.Ordinal)
            .Replace(FileInputVariable, fileInput ?? string.Empty, StringComparison.Ordinal)
            .Trim();

        return new PromptResolution(prompt, saveMode);
    }
}
