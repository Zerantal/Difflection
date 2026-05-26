using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Difflection.ViewModels;

namespace Difflection.Infrastructure;

public sealed class Hotkey
{
    public static readonly AttachedProperty<string?> IdProperty =
        AvaloniaProperty.RegisterAttached<Hotkey, Control, string?>("Id");

    private static readonly AttachedProperty<HotkeyBinding?> BindingProperty =
        AvaloniaProperty.RegisterAttached<Hotkey, Control, HotkeyBinding?>("Binding");

    static Hotkey()
    {
        IdProperty.Changed.AddClassHandler<Control>(OnIdChanged);
    }

    private Hotkey()
    {
    }

    public static string? GetId(Control control)
    {
        return control.GetValue(IdProperty);
    }

    public static void SetId(Control control, string? value)
    {
        control.SetValue(IdProperty, value);
    }

    private static void OnIdChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.GetValue(BindingProperty)?.Dispose();

        var newId = e.GetNewValue<string?>();
        if (string.IsNullOrEmpty(newId))
        {
            control.SetValue(BindingProperty, null);
            return;
        }

        control.SetValue(BindingProperty, new HotkeyBinding(control, newId));
    }
}

internal sealed class HotkeyBinding : IDisposable
{
    private readonly Control _control;
    private readonly string _id;
    private MainWindowViewModel? _viewModel;
    private string? _baseTooltip;
    private bool _writingTooltip;

    public HotkeyBinding(Control control, string id)
    {
        _control = control;
        _id = id;
        _baseTooltip = ToolTip.GetTip(control) as string;

        _control.DataContextChanged += OnDataContextChanged;
        _control.PropertyChanged += OnControlPropertyChanged;
        Bind();
    }

    public void Dispose()
    {
        _control.DataContextChanged -= OnDataContextChanged;
        _control.PropertyChanged -= OnControlPropertyChanged;
        Unbind();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unbind();
        Bind();
    }

    private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ToolTip.TipProperty || _writingTooltip)
        {
            return;
        }

        _baseTooltip = e.GetNewValue<object?>() as string;
        Apply();
    }

    private void Bind()
    {
        _viewModel = _control.DataContext as MainWindowViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Shortcuts.Changed += OnShortcutsChanged;
        Apply();
    }

    private void Unbind()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Shortcuts.Changed -= OnShortcutsChanged;
        _viewModel = null;
    }

    private void OnShortcutsChanged(object? sender, EventArgs e)
    {
        Apply();
    }

    private void Apply()
    {
        if (_viewModel is null)
        {
            return;
        }

        var shortcut = _viewModel.Shortcuts.Find(_id);
        if (shortcut is null)
        {
            return;
        }

        var gestureText = HotkeyFormatter.Format(shortcut.Gesture);
        var description = string.IsNullOrWhiteSpace(_baseTooltip) ? shortcut.Description : _baseTooltip;

        _writingTooltip = true;
        try
        {
            ToolTip.SetTip(_control, $"{description} ({gestureText})");
        }
        finally
        {
            _writingTooltip = false;
        }
    }
}

internal static class HotkeyFormatter
{
    public static string Format(KeyGesture gesture)
    {
        var sb = new StringBuilder();
        var mods = gesture.KeyModifiers;
        var isMac = OperatingSystem.IsMacOS();
        if (mods.HasFlag(KeyModifiers.Control)) sb.Append("Ctrl+");
        if (mods.HasFlag(KeyModifiers.Shift)) sb.Append("Shift+");
        if (mods.HasFlag(KeyModifiers.Alt)) sb.Append(isMac ? "Option+" : "Alt+");
        if (mods.HasFlag(KeyModifiers.Meta)) sb.Append(isMac ? "Cmd+" : "Win+");
        sb.Append(FormatKey(gesture.Key));
        return sb.ToString();
    }

    private static string FormatKey(Key key)
    {
        return key switch
        {
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            Key.NumPad0 => "Num0",
            Key.NumPad1 => "Num1",
            Key.NumPad2 => "Num2",
            Key.NumPad3 => "Num3",
            Key.NumPad4 => "Num4",
            Key.NumPad5 => "Num5",
            Key.NumPad6 => "Num6",
            Key.NumPad7 => "Num7",
            Key.NumPad8 => "Num8",
            Key.NumPad9 => "Num9",
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            _ => key.ToString()
        };
    }
}
