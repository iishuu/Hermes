using System.Windows.Interop;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Input;

namespace Hermes.Windows.AIAction;

public sealed class AIActionHotkeyManager : IDisposable
{
    private const int ChooseFileHotkeyId = 2099;
    private const int HotkeyIdBase = 2100;

    private readonly AIActionConfigService _configService;
    private readonly AppLogger _logger;
    private readonly Dictionary<int, AIActionDefinition> _registeredActions = [];
    private HwndSource? _source;
    private bool _enabled;

    public AIActionHotkeyManager(AIActionConfigService configService, AppLogger logger)
    {
        _configService = configService;
        _logger = logger;
        _configService.Changed += ConfigService_Changed;
    }

    public event EventHandler<AIActionDefinition>? ActionHotkeyPressed;

    public event EventHandler? ChooseFileHotkeyPressed;

    public void Start()
    {
        _enabled = true;
        Refresh();
    }

    public void Stop()
    {
        _enabled = false;
        UnregisterAll();
    }

    public void Refresh()
    {
        UnregisterAll();
        if (!_enabled)
        {
            return;
        }

        EnsureSource();
        if (_source?.Handle is not { } handle || handle == IntPtr.Zero)
        {
            return;
        }

        RegisterChooseFileHotkey(handle);

        var id = HotkeyIdBase;
        foreach (var action in _configService.Actions.OrderBy(action => action.Order))
        {
            if (string.IsNullOrWhiteSpace(action.Hotkey))
            {
                continue;
            }

            if (!HotkeyGesture.TryParse(action.Hotkey, out var gesture))
            {
                _logger.Warning($"AI Action '{action.Name}' has invalid hotkey '{action.Hotkey}'.");
                continue;
            }

            if (NativeMethods.RegisterHotKey(handle, id, gesture.Modifiers, gesture.VirtualKey))
            {
                _registeredActions[id] = action;
                id++;
            }
            else
            {
                _logger.Warning($"Failed to register AI Action hotkey {gesture} for '{action.Name}'.");
            }
        }
    }

    public void Dispose()
    {
        _configService.Changed -= ConfigService_Changed;
        Stop();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    private void ConfigService_Changed(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void EnsureSource()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("HermesAIActionHotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private void UnregisterAll()
    {
        if (_source?.Handle is { } handle && handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(handle, ChooseFileHotkeyId);
            foreach (var id in _registeredActions.Keys.ToArray())
            {
                NativeMethods.UnregisterHotKey(handle, id);
            }
        }

        _registeredActions.Clear();
    }

    private void RegisterChooseFileHotkey(IntPtr handle)
    {
        var hotkey = _configService.Config.ChooseFileHotkey;
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return;
        }

        if (!HotkeyGesture.TryParse(hotkey, out var gesture))
        {
            _logger.Warning($"AI Action choose-file hotkey '{hotkey}' is invalid.");
            return;
        }

        if (!NativeMethods.RegisterHotKey(handle, ChooseFileHotkeyId, gesture.Modifiers, gesture.VirtualKey))
        {
            _logger.Warning($"Failed to register AI Action choose-file hotkey {gesture}.");
        }
    }
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.HotkeyMessage)
        {
            return IntPtr.Zero;
        }

        var id = wParam.ToInt32();
        if (id == ChooseFileHotkeyId)
        {
            handled = true;
            ChooseFileHotkeyPressed?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }

        if (_registeredActions.TryGetValue(id, out var action))
        {
            handled = true;
            ActionHotkeyPressed?.Invoke(this, action);
        }

        return IntPtr.Zero;
    }
}

