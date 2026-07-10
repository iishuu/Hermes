namespace Hermes.Windows.AIAction;

public sealed class AIActionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Hotkey { get; set; } = string.Empty;

    public int Order { get; set; }

    public string PromptTemplate { get; set; } = string.Empty;

    public string InputFile { get; set; } = string.Empty;
}

public sealed class AIActionConfiguration
{
    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string Model { get; set; } = "deepseek-v4-flash";

    public double Temperature { get; set; } = 0.7;

    public string ChooseFileHotkey { get; set; } = "Ctrl+Alt+O";
}

public enum AIActionSaveMode
{
    None,
    Append,
    Overwrite
}

public sealed record AIActionRequest(string Prompt, string Model, double Temperature);

public sealed record PromptResolution(string Prompt, AIActionSaveMode SaveMode);

