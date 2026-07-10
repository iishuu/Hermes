using Hermes.Windows.Infrastructure;
using Hermes.Windows.Overlay;
using Hermes.Windows.Selection;

namespace Hermes.Windows.AIAction;

public sealed class AIActionExecutor
{
    private readonly SelectionOrchestrator _selectionOrchestrator;
    private readonly OverlayManager _overlayManager;
    private readonly AIActionConfigService _configService;
    private readonly IAIActionProvider _provider;
    private readonly PromptTemplateResolver _promptResolver;
    private readonly ContextFileService _contextFileService;
    private readonly ContextWriteService _contextWriteService;
    private readonly AppLogger _logger;

    public AIActionExecutor(
        SelectionOrchestrator selectionOrchestrator,
        OverlayManager overlayManager,
        AIActionConfigService configService,
        IAIActionProvider provider,
        PromptTemplateResolver promptResolver,
        ContextFileService contextFileService,
        ContextWriteService contextWriteService,
        AppLogger logger)
    {
        _selectionOrchestrator = selectionOrchestrator;
        _overlayManager = overlayManager;
        _configService = configService;
        _provider = provider;
        _promptResolver = promptResolver;
        _contextFileService = contextFileService;
        _contextWriteService = contextWriteService;
        _logger = logger;
    }

    public async Task ExecuteAsync(AIActionDefinition action, CancellationToken cancellationToken = default)
    {
        var popup = _overlayManager.ShowPopup(
            SelectionResult.FromText(action.Name, SelectionProviderKind.None, null, null),
            "Running AI Action...",
            action.Name);

        try
        {
            var selection = await _selectionOrchestrator.ReadForExplicitTriggerAsync(cancellationToken);
            var clipboard = await _selectionOrchestrator.ReadClipboardAsync(cancellationToken);
            var fileInput = await _contextFileService.ReadInputAsync(action.InputFile, cancellationToken);

            var selectedText = selection.Validation.IsValid ? selection.Result.Text ?? string.Empty : string.Empty;
            var clipboardText = clipboard.Validation.IsValid ? clipboard.Result.Text ?? string.Empty : string.Empty;
            var resolution = _promptResolver.Resolve(
                action.PromptTemplate,
                selectedText,
                clipboardText,
                fileInput);

            if (string.IsNullOrWhiteSpace(resolution.Prompt))
            {
                popup.SetError("AI Action prompt is empty after variable resolution.", showSettings: true);
                return;
            }

            var config = _configService.Config;
            var result = await _provider.ChatAsync(
                new AIActionRequest(resolution.Prompt, config.Model, config.Temperature),
                cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            {
                popup.SetError(result.UserMessage ?? "AI Action failed.", showSettings: true);
                return;
            }


            await _contextWriteService.WriteAsync(action.InputFile, result.Content, resolution.SaveMode, cancellationToken);
            popup.SetTranslation(result.Content);
        }
        catch (Exception ex)
        {
            _logger.Error($"AI Action '{action.Name}' failed.", ex);
            popup.SetError("AI Action failed. Check logs for details.", showSettings: true);
        }
    }

}


