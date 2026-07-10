using System.IO;
using System.Text.Json;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.AIAction;

public sealed class AIActionConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppLogger _logger;

    public AIActionConfigService(AppLogger logger)
    {
        _logger = logger;
    }

    public AIActionConfiguration Config { get; private set; } = new();

    public IReadOnlyList<AIActionDefinition> Actions { get; private set; } = [];

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        AIActionPaths.EnsureCreated();
        Config = await LoadConfigAsync(cancellationToken);
        Actions = await LoadActionsAsync(cancellationToken);
    }

    public async Task SaveConfigAsync(AIActionConfiguration config, CancellationToken cancellationToken = default)
    {
        AIActionPaths.EnsureCreated();
        Normalize(config);
        await using var stream = File.Create(AIActionPaths.ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
        Config = config;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveActionsAsync(IEnumerable<AIActionDefinition> actions, CancellationToken cancellationToken = default)
    {
        AIActionPaths.EnsureCreated();
        var normalized = actions
            .Select(Normalize)
            .OrderBy(action => action.Order)
            .ThenBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < normalized.Count; index++)
        {
            normalized[index].Order = index;
        }

        await using var stream = File.Create(AIActionPaths.ActionsPath);
        await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
        Actions = normalized;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<AIActionConfiguration> LoadConfigAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(AIActionPaths.ConfigPath))
        {
            var config = new AIActionConfiguration();
            await SaveConfigAsync(config, cancellationToken);
            return config;
        }

        try
        {
            var json = await File.ReadAllTextAsync(AIActionPaths.ConfigPath, cancellationToken);
            var config = JsonSerializer.Deserialize<AIActionConfiguration>(json, JsonOptions) ?? new AIActionConfiguration();
            var isPreChooseFileHotkeySchema = !json.Contains("ChooseFileHotkey", StringComparison.OrdinalIgnoreCase);
            if (isPreChooseFileHotkeySchema
                && string.Equals(config.Model, "deepseek-chat", StringComparison.OrdinalIgnoreCase))
            {
                config.Model = "deepseek-v4-flash";
            }

            Normalize(config);
            return config;
        }
        catch (Exception ex)
        {
            _logger.Warning($"AI Action config could not be loaded; defaults will be used. {ex.Message}");
            return new AIActionConfiguration();
        }
    }

    private async Task<IReadOnlyList<AIActionDefinition>> LoadActionsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(AIActionPaths.ActionsPath))
        {
            await SaveActionsAsync([], cancellationToken);
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(AIActionPaths.ActionsPath, cancellationToken);
            var actions = JsonSerializer.Deserialize<List<AIActionDefinition>>(json, JsonOptions) ?? [];
            return actions.Select(Normalize)
                .OrderBy(action => action.Order)
                .ThenBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.Warning($"AI Action list could not be loaded; no actions will be registered. {ex.Message}");
            return [];
        }
    }

    private static void Normalize(AIActionConfiguration config)
    {
        config.BaseUrl = string.IsNullOrWhiteSpace(config.BaseUrl)
            ? "https://api.deepseek.com"
            : config.BaseUrl.Trim().TrimEnd('/');
        config.Model = string.IsNullOrWhiteSpace(config.Model) ? "deepseek-v4-flash" : config.Model.Trim();
        config.Temperature = Math.Clamp(config.Temperature, 0, 2);
        config.ChooseFileHotkey = config.ChooseFileHotkey.Trim();
    }

    private static AIActionDefinition Normalize(AIActionDefinition action)
    {
        if (action.Id == Guid.Empty)
        {
            action.Id = Guid.NewGuid();
        }

        action.Name = string.IsNullOrWhiteSpace(action.Name) ? "AI Action" : action.Name.Trim();
        action.Hotkey = action.Hotkey.Trim();
        action.PromptTemplate = action.PromptTemplate ?? string.Empty;
        action.InputFile = action.InputFile?.Trim() ?? string.Empty;
        return action;
    }
}



