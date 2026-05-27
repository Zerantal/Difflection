using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;

namespace Difflection.Infrastructure;

public sealed record KeyboardShortcut(string Id, KeyGesture Gesture, string Description, Action Invoke);

public sealed class KeyboardShortcutRegistry
{
    private readonly List<KeyboardShortcut> _shortcuts = [];

    public IReadOnlyList<KeyboardShortcut> Shortcuts => _shortcuts;

    public event EventHandler? Changed;

    public KeyboardShortcutRegistry Add(string id, string gesture, string description, Action invoke)
    {
        if (_shortcuts.Any(s => s.Id == id))
        {
            throw new InvalidOperationException($"Keyboard shortcut '{id}' is already registered.");
        }

        _shortcuts.Add(new KeyboardShortcut(id, ParseGesture(gesture), description, invoke));
        Changed?.Invoke(this, EventArgs.Empty);
        return this;
    }

    public bool Remove(string id)
    {
        var removed = _shortcuts.RemoveAll(s => s.Id == id);
        if (removed > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    public KeyboardShortcut? Find(string id)
    {
        return _shortcuts.FirstOrDefault(s => s.Id == id);
    }

    public bool TryInvoke(KeyEventArgs e)
    {
        foreach (var shortcut in _shortcuts)
        {
            if (shortcut.Gesture.Matches(e))
            {
                shortcut.Invoke();
                return true;
            }
        }

        return false;
    }

    private static KeyGesture ParseGesture(string gesture)
    {
        var resolved = OperatingSystem.IsMacOS()
            ? gesture.Replace("Ctrl+", "Meta+", StringComparison.OrdinalIgnoreCase)
            : gesture;
        return KeyGesture.Parse(resolved);
    }
}
