using System;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Difflection.Infrastructure;
using Xunit;

namespace Difflection.Tests.Infrastructure;

public sealed class KeyboardShortcutRegistryTests
{
    [AvaloniaFact]
    public void Add_parses_ctrl_gesture_with_platform_modifier()
    {
        var registry = new KeyboardShortcutRegistry();

        registry.Add("open", "Ctrl+O", "Open", () => { });

        var shortcut = registry.Find("open");
        Assert.NotNull(shortcut);
        Assert.Equal(Key.O, shortcut.Gesture.Key);
        var expectedModifiers = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        Assert.Equal(expectedModifiers, shortcut.Gesture.KeyModifiers);
    }

    [AvaloniaFact]
    public void Add_leaves_gestures_without_ctrl_unchanged()
    {
        var registry = new KeyboardShortcutRegistry();

        registry.Add("refresh", "F5", "Refresh", () => { });

        var shortcut = registry.Find("refresh");
        Assert.NotNull(shortcut);
        Assert.Equal(Key.F5, shortcut.Gesture.Key);
        Assert.Equal(KeyModifiers.None, shortcut.Gesture.KeyModifiers);
    }

    [AvaloniaFact]
    public void Add_throws_when_id_already_registered()
    {
        var registry = new KeyboardShortcutRegistry();
        registry.Add("dup", "D1", "first", () => { });

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Add("dup", "D2", "second", () => { }));

        Assert.Contains("dup", exception.Message);
        Assert.Single(registry.Shortcuts);
    }

    [AvaloniaFact]
    public void Remove_returns_true_for_existing_id_and_drops_entry()
    {
        var registry = new KeyboardShortcutRegistry();
        registry.Add("a", "D1", "A", () => { });
        registry.Add("b", "D2", "B", () => { });

        var removed = registry.Remove("a");

        Assert.True(removed);
        Assert.Single(registry.Shortcuts);
        Assert.Null(registry.Find("a"));
        Assert.NotNull(registry.Find("b"));
    }

    [AvaloniaFact]
    public void Remove_returns_false_for_unknown_id()
    {
        var registry = new KeyboardShortcutRegistry();
        registry.Add("a", "D1", "A", () => { });

        Assert.False(registry.Remove("missing"));
        Assert.Single(registry.Shortcuts);
    }

    [AvaloniaFact]
    public void Changed_event_fires_on_add_and_remove()
    {
        var registry = new KeyboardShortcutRegistry();
        var fired = 0;
        registry.Changed += (_, _) => fired++;

        registry.Add("a", "D1", "A", () => { });
        registry.Remove("a");
        registry.Remove("missing");

        Assert.Equal(2, fired);
    }
}
