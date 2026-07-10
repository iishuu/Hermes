using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfInput = System.Windows.Input;
using Hermes.Windows.AIAction;
using Hermes.Windows.Input;
using Microsoft.Win32;

namespace Hermes.Windows.Shell;

public partial class AIActionSettingsWindow : Window
{
    private const string ApiKeyMask = "********";
    private static readonly string[] PromptVariables =
    [
        PromptTemplateResolver.TextVariable,
        PromptTemplateResolver.ClipboardVariable,
        PromptTemplateResolver.FileInputVariable,
        PromptTemplateResolver.FileAppendVariable,
        PromptTemplateResolver.FileOverwriteVariable
    ];

    private readonly AIActionConfigService _configService;
    private readonly AIActionSecretStorageService _secretStorage;
    private readonly ContextFileService _contextFileService;
    private readonly List<AIActionDefinition> _actions = [];
    private Guid? _editingActionId;
    private HotkeyRecordingTarget _recordingHotkeyTarget = HotkeyRecordingTarget.None;
    private string? _hotkeyBeforeRecording;
    private bool _highlightingPrompt;

    public AIActionSettingsWindow(
        AIActionConfigService configService,
        AIActionSecretStorageService secretStorage,
        ContextFileService contextFileService)
    {
        InitializeComponent();
        _configService = configService;
        _secretStorage = secretStorage;
        _contextFileService = contextFileService;
        LoadState();
    }

    private void LoadState()
    {
        BaseUrlText.Text = _configService.Config.BaseUrl;
        ModelText.Text = _configService.Config.Model;
        TemperatureText.Text = _configService.Config.Temperature.ToString("0.##");
        UpdateChooseFileHotkey(_configService.Config.ChooseFileHotkey);
        ApiKeyBox.Password = _secretStorage.HasApiKey() ? ApiKeyMask : string.Empty;
        _actions.Clear();
        _actions.AddRange(_configService.Actions.Select(Clone));
        RefreshActionsList();
        NewAction();
    }

    private async void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateConfig(out var message))
        {
            StatusText.Text = message;
            return;
        }

        var config = new AIActionConfiguration
        {
            BaseUrl = BaseUrlText.Text.Trim(),
            Model = ModelText.Text.Trim(),
            Temperature = double.Parse(TemperatureText.Text.Trim()),
            ChooseFileHotkey = GetChooseFileHotkeyText()
        };

        if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password) && ApiKeyBox.Password != ApiKeyMask)
        {
            await _secretStorage.SaveApiKeyAsync(ApiKeyBox.Password.Trim());
            ApiKeyBox.Password = ApiKeyMask;
        }

        await _configService.SaveConfigAsync(config);
        StatusText.Text = "AI configuration saved.";
    }

    private void NewAction_Click(object sender, RoutedEventArgs e)
    {
        NewAction();
    }

    private void EditAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as WpfButton)?.Tag is not Guid id)
        {
            return;
        }

        var action = _actions.FirstOrDefault(item => item.Id == id);
        if (action is null)
        {
            return;
        }

        _editingActionId = action.Id;
        ActionNameText.Text = action.Name;
        UpdateActionHotkey(action.Hotkey);
        InputFileText.Text = action.InputFile;
        SetPromptText(action.PromptTemplate);
        EditorStatusText.Text = "Editing existing action.";
    }

    private async void SaveAction_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateAction(out var message))
        {
            EditorStatusText.Text = message;
            return;
        }

        var action = _editingActionId is { } id
            ? _actions.FirstOrDefault(item => item.Id == id)
            : null;
        if (action is null)
        {
            action = new AIActionDefinition
            {
                Id = Guid.NewGuid(),
                Order = _actions.Count
            };
            _actions.Add(action);
            _editingActionId = action.Id;
        }

        action.Name = ActionNameText.Text.Trim();
        action.Hotkey = GetActionHotkeyText();
        action.InputFile = InputFileText.Text.Trim();
        action.PromptTemplate = GetPromptText();

        await _configService.SaveActionsAsync(_actions);
        RefreshActionsList();
        EditorStatusText.Text = "Action saved.";
    }

    private async void DeleteAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as WpfButton)?.Tag is not Guid id)
        {
            return;
        }

        var action = _actions.FirstOrDefault(item => item.Id == id);
        if (action is null)
        {
            return;
        }

        if (WpfMessageBox.Show($"Delete AI Action '{action.Name}'?", "Hermes AI Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _actions.Remove(action);
        await _configService.SaveActionsAsync(_actions);
        RefreshActionsList();
        NewAction();
    }

    private async void MoveActionUp_Click(object sender, RoutedEventArgs e)
    {
        await MoveActionAsync((sender as WpfButton)?.Tag, -1);
    }

    private async void MoveActionDown_Click(object sender, RoutedEventArgs e)
    {
        await MoveActionAsync((sender as WpfButton)?.Tag, 1);
    }

    private async Task MoveActionAsync(object? tag, int direction)
    {
        if (tag is not Guid id)
        {
            return;
        }

        var ordered = _actions.OrderBy(action => action.Order).ToList();
        var index = ordered.FindIndex(action => action.Id == id);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i;
        }

        _actions.Clear();
        _actions.AddRange(ordered);
        await _configService.SaveActionsAsync(_actions);
        RefreshActionsList();
    }

    private async void ChooseInputFile_Click(object sender, RoutedEventArgs e)
    {
        await ChooseInputFileAsync();
    }

    public async Task ChooseInputFileFromHotkeyAsync()
    {
        Activate();
        await ChooseInputFileAsync();
    }

    private async Task ChooseInputFileAsync()
    {
        var dialog = new WpfOpenFileDialog
        {
            Filter = "Text and Markdown (*.txt;*.md)|*.txt;*.md",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            InputFileText.Text = await _contextFileService.ImportAsync(dialog.FileName);
            EditorStatusText.Text = "Input file path saved.";
        }
        catch (Exception ex)
        {
            EditorStatusText.Text = ex.Message;
        }
    }

    private void ClearEditor_Click(object sender, RoutedEventArgs e)
    {
        NewAction();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ActionHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        BeginHotkeyRecording(HotkeyRecordingTarget.Action);
    }

    private void ChooseFileHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        BeginHotkeyRecording(HotkeyRecordingTarget.ChooseFile);
    }

    private void HotkeyRecorder_LostKeyboardFocus(object sender, WpfInput.KeyboardFocusChangedEventArgs e)
    {
        CancelHotkeyRecording();
    }

    private void HotkeyRecorder_PreviewKeyDown(object sender, WpfInput.KeyEventArgs e)
    {
        if (_recordingHotkeyTarget == HotkeyRecordingTarget.None)
        {
            return;
        }

        var key = e.Key == WpfInput.Key.System ? e.SystemKey : e.Key;
        if (key == WpfInput.Key.ImeProcessed)
        {
            key = e.ImeProcessedKey;
        }

        if (key == WpfInput.Key.Escape)
        {
            CancelHotkeyRecording();
            e.Handled = true;
            return;
        }

        if (key is WpfInput.Key.Back or WpfInput.Key.Delete)
        {
            UpdateRecordedHotkey(_recordingHotkeyTarget, string.Empty);
            FinishHotkeyRecording();
            e.Handled = true;
            return;
        }

        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var modifiers = WpfInput.Keyboard.Modifiers;
        var parts = new List<string>();
        if (modifiers.HasFlag(WpfInput.ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(WpfInput.ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(WpfInput.ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(WpfInput.ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        if (parts.Count == 0)
        {
            SetHotkeyRecorderStatus("Hotkey needs Ctrl, Alt, Shift, or Win.");
            e.Handled = true;
            return;
        }

        parts.Add(FormatKey(key));
        UpdateRecordedHotkey(_recordingHotkeyTarget, string.Join("+", parts));
        FinishHotkeyRecording();
        e.Handled = true;
    }

    private void PromptEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_highlightingPrompt)
        {
            return;
        }

        HighlightPromptVariables();
    }

    private void NewAction()
    {
        _editingActionId = null;
        ActionNameText.Text = string.Empty;
        UpdateActionHotkey(string.Empty);
        InputFileText.Text = string.Empty;
        SetPromptText("Use $text$ as input and produce a concise result.");
        EditorStatusText.Text = "Creating new action.";
    }

    private bool ValidateConfig(out string message)
    {
        message = string.Empty;
        if (!Uri.TryCreate(BaseUrlText.Text.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            message = "Base URL must be a valid http or https URL.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ModelText.Text))
        {
            message = "Model is required.";
            return false;
        }

        if (!double.TryParse(TemperatureText.Text.Trim(), out var temperature) || temperature is < 0 or > 2)
        {
            message = "Temperature must be between 0 and 2.";
            return false;
        }

        var chooseFileHotkey = GetChooseFileHotkeyText();
        if (!string.IsNullOrWhiteSpace(chooseFileHotkey) && !HotkeyGesture.TryParse(chooseFileHotkey, out _))
        {
            message = "Choose File hotkey must look like Ctrl+Alt+O, or be empty.";
            return false;
        }

        return true;
    }

    private bool ValidateAction(out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(ActionNameText.Text))
        {
            message = "Name is required.";
            return false;
        }

        var actionHotkey = GetActionHotkeyText();
        if (!string.IsNullOrWhiteSpace(actionHotkey)
            && !HotkeyGesture.TryParse(actionHotkey, out _))
        {
            message = "Hotkey format must look like Ctrl+Alt+P.";
            return false;
        }

        var duplicate = _actions.FirstOrDefault(action =>
            action.Id != _editingActionId
            && !string.IsNullOrWhiteSpace(action.Hotkey)
            && string.Equals(action.Hotkey, actionHotkey, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            message = $"Hotkey conflicts with '{duplicate.Name}'.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(actionHotkey)
            && string.Equals(actionHotkey, GetChooseFileHotkeyText(), StringComparison.OrdinalIgnoreCase))
        {
            message = "Action hotkey conflicts with Choose File hotkey.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(GetPromptText()))
        {
            message = "Prompt template is required.";
            return false;
        }

        return true;
    }

    private void BeginHotkeyRecording(HotkeyRecordingTarget target)
    {
        if (_recordingHotkeyTarget == target)
        {
            CancelHotkeyRecording();
            return;
        }

        CancelHotkeyRecording();
        _recordingHotkeyTarget = target;
        _hotkeyBeforeRecording = GetRecordedHotkey(target);
        var button = GetHotkeyButton(target);
        button.Content = "Press hotkey...";
        button.Focus();
        SetHotkeyRecorderStatus("Press a key combination. Esc cancels, Backspace clears.");
    }

    private void CancelHotkeyRecording()
    {
        if (_recordingHotkeyTarget == HotkeyRecordingTarget.None)
        {
            return;
        }

        UpdateRecordedHotkey(_recordingHotkeyTarget, _hotkeyBeforeRecording ?? string.Empty);
        FinishHotkeyRecording();
    }

    private void FinishHotkeyRecording()
    {
        _recordingHotkeyTarget = HotkeyRecordingTarget.None;
        _hotkeyBeforeRecording = null;
        WpfInput.Keyboard.ClearFocus();
    }

    private void UpdateRecordedHotkey(HotkeyRecordingTarget target, string hotkey)
    {
        if (target == HotkeyRecordingTarget.Action)
        {
            UpdateActionHotkey(hotkey);
            return;
        }

        if (target == HotkeyRecordingTarget.ChooseFile)
        {
            UpdateChooseFileHotkey(hotkey);
        }
    }

    private string GetRecordedHotkey(HotkeyRecordingTarget target)
    {
        return target switch
        {
            HotkeyRecordingTarget.Action => GetActionHotkeyText(),
            HotkeyRecordingTarget.ChooseFile => GetChooseFileHotkeyText(),
            _ => string.Empty
        };
    }

    private WpfButton GetHotkeyButton(HotkeyRecordingTarget target)
    {
        return target == HotkeyRecordingTarget.ChooseFile ? ChooseFileHotkeyButton : ActionHotkeyButton;
    }

    private void UpdateActionHotkey(string hotkey)
    {
        SetHotkeyButton(ActionHotkeyButton, hotkey, "Click to record action hotkey");
    }

    private void UpdateChooseFileHotkey(string hotkey)
    {
        SetHotkeyButton(ChooseFileHotkeyButton, hotkey, "Click to record choose-file hotkey");
    }

    private string GetActionHotkeyText()
    {
        return ActionHotkeyButton.Tag as string ?? string.Empty;
    }

    private string GetChooseFileHotkeyText()
    {
        return ChooseFileHotkeyButton.Tag as string ?? string.Empty;
    }

    private static void SetHotkeyButton(WpfButton button, string hotkey, string emptyText)
    {
        var normalized = hotkey.Trim();
        button.Tag = normalized;
        button.Content = string.IsNullOrWhiteSpace(normalized) ? emptyText : normalized;
    }

    private void SetHotkeyRecorderStatus(string message)
    {
        if (_recordingHotkeyTarget == HotkeyRecordingTarget.ChooseFile)
        {
            StatusText.Text = message;
            return;
        }

        EditorStatusText.Text = message;
    }

    private static bool IsModifierKey(WpfInput.Key key)
    {
        return key is WpfInput.Key.LeftCtrl
            or WpfInput.Key.RightCtrl
            or WpfInput.Key.LeftAlt
            or WpfInput.Key.RightAlt
            or WpfInput.Key.LeftShift
            or WpfInput.Key.RightShift
            or WpfInput.Key.LWin
            or WpfInput.Key.RWin;
    }

    private static string FormatKey(WpfInput.Key key)
    {
        return key.ToString();
    }
    private void RefreshActionsList()
    {
        ActionsList.ItemsSource = null;
        ActionsList.ItemsSource = _actions.OrderBy(action => action.Order).ToList();
    }

    private void SetPromptText(string text)
    {
        PromptEditor.Document.Blocks.Clear();
        PromptEditor.Document.Blocks.Add(new Paragraph(new Run(text ?? string.Empty)));
        HighlightPromptVariables();
    }

    private string GetPromptText()
    {
        return new TextRange(PromptEditor.Document.ContentStart, PromptEditor.Document.ContentEnd).Text.TrimEnd('\r', '\n');
    }

    private void HighlightPromptVariables()
    {
        _highlightingPrompt = true;
        try
        {
            var textRange = new TextRange(PromptEditor.Document.ContentStart, PromptEditor.Document.ContentEnd);
            textRange.ClearAllProperties();
            textRange.ApplyPropertyValue(TextElement.ForegroundProperty, Foreground);
            textRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);

            foreach (var variable in PromptVariables)
            {
                var start = PromptEditor.Document.ContentStart;
                while (start is not null)
                {
                    var match = FindText(start, variable);
                    if (match is null)
                    {
                        break;
                    }

                    match.ApplyPropertyValue(TextElement.ForegroundProperty, WpfBrushes.DeepSkyBlue);
                    match.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Medium);
                    start = match.End;
                }
            }
        }
        finally
        {
            _highlightingPrompt = false;
        }
    }

    private static TextRange? FindText(TextPointer start, string text)
    {
        var navigator = start;
        while (navigator.CompareTo(start.DocumentEnd) < 0)
        {
            if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var runText = navigator.GetTextInRun(LogicalDirection.Forward);
                var index = runText.IndexOf(text, StringComparison.Ordinal);
                if (index >= 0)
                {
                    var matchStart = navigator.GetPositionAtOffset(index);
                    var matchEnd = matchStart?.GetPositionAtOffset(text.Length);
                    if (matchStart is not null && matchEnd is not null)
                    {
                        return new TextRange(matchStart, matchEnd);
                    }
                }
            }

            navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
            if (navigator is null)
            {
                break;
            }
        }

        return null;
    }

    private static AIActionDefinition Clone(AIActionDefinition action)
    {
        return new AIActionDefinition
        {
            Id = action.Id,
            Name = action.Name,
            Hotkey = action.Hotkey,
            Order = action.Order,
            PromptTemplate = action.PromptTemplate,
            InputFile = action.InputFile
        };
    }
    private enum HotkeyRecordingTarget
    {
        None,
        Action,
        ChooseFile
    }
}







